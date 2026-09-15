using System.Collections;
using System.Data;
using System.Data.Common;

namespace SessionSetupMicroService.Tests.TestDoubles
{
    /// <summary>
    /// A minimal ADO.NET fake, enough to drive the Postgres repositories without a database.
    ///
    /// The repositories are written against System.Data.Common — NpgsqlDataSourceFactory is the only
    /// file in the service that names Npgsql — which is exactly what makes this possible. These
    /// tests do not check that the SQL is valid PostgreSQL; they check the wiring: which statement
    /// was sent, which parameters were bound to it, and how a row is mapped back. Whether the SQL
    /// itself is correct is a question for a real database, and no in-process fake can answer it.
    /// </summary>
    public sealed class FakeDb
    {
        public List<RecordedCommand> Executed { get; } = new();

        /// <summary>Result for ExecuteScalar, keyed by the SQL that was sent.</summary>
        public Func<string, object?> ScalarFor { get; set; } = _ => null;

        /// <summary>Rows for ExecuteReader, keyed by the SQL that was sent.</summary>
        public Func<string, IReadOnlyList<FakeRow>> RowsFor { get; set; } = _ => Array.Empty<FakeRow>();

        /// <summary>Rows affected for ExecuteNonQuery, keyed by the SQL that was sent.</summary>
        public Func<string, int> NonQueryFor { get; set; } = _ => 1;

        private DbDataSource? _dataSource;

        public DbDataSource DataSource => _dataSource ??= new FakeDbDataSource(this);

        public RecordedCommand Single() => Executed.Single();

        public RecordedCommand LastCommand => Executed[^1];

        public IEnumerable<RecordedCommand> For(string sql) =>
            Executed.Where(command => command.Sql == sql);
    }

    public sealed record RecordedCommand(string Sql, IReadOnlyDictionary<string, object?> Parameters)
    {
        public object? Parameter(string name) => Parameters.TryGetValue(name, out var value) ? value : null;

        public bool Bound(string name) => Parameters.ContainsKey(name);
    }

    /// <summary>An ordered row. Ordinal access matters: PreKeyRepository.CountAsync reads by index.</summary>
    public sealed class FakeRow
    {
        private readonly List<(string Name, object? Value)> _columns = new();

        public FakeRow Set(string name, object? value)
        {
            _columns.Add((name, value));
            return this;
        }

        public int Count => _columns.Count;

        public string Name(int ordinal) => _columns[ordinal].Name;

        public object? Value(int ordinal) => _columns[ordinal].Value;

        public int Ordinal(string name)
        {
            var index = _columns.FindIndex(column => string.Equals(column.Name, name, StringComparison.OrdinalIgnoreCase));
            if (index < 0)
            {
                throw new IndexOutOfRangeException($"No column named '{name}' in this row.");
            }

            return index;
        }
    }

    internal sealed class FakeDbDataSource : DbDataSource
    {
        private readonly FakeDb _db;

        public FakeDbDataSource(FakeDb db) => _db = db;

        public override string ConnectionString => "fake";

        protected override DbConnection CreateDbConnection() => new FakeDbConnection(_db);
    }

    internal sealed class FakeDbConnection : DbConnection
    {
        private readonly FakeDb _db;
        private ConnectionState _state = ConnectionState.Closed;

        public FakeDbConnection(FakeDb db) => _db = db;

        public override string ConnectionString { get; set; } = "fake";

        public override string Database => "fake";

        public override string DataSource => "fake";

        public override string ServerVersion => "0.0";

        public override ConnectionState State => _state;

        public override void ChangeDatabase(string databaseName) { }

        public override void Close() => _state = ConnectionState.Closed;

        public override void Open() => _state = ConnectionState.Open;

        protected override DbCommand CreateDbCommand() => new FakeDbCommand(_db) { Connection = this };

        protected override DbTransaction BeginDbTransaction(IsolationLevel isolationLevel) =>
            new FakeDbTransaction(this, isolationLevel);
    }

    internal sealed class FakeDbTransaction : DbTransaction
    {
        private readonly DbConnection _connection;

        public FakeDbTransaction(DbConnection connection, IsolationLevel isolationLevel)
        {
            _connection = connection;
            IsolationLevel = isolationLevel;
        }

        public bool Committed { get; private set; }

        public bool RolledBack { get; private set; }

        public override IsolationLevel IsolationLevel { get; }

        protected override DbConnection DbConnection => _connection;

        public override void Commit() => Committed = true;

        public override void Rollback() => RolledBack = true;
    }

    internal sealed class FakeDbCommand : DbCommand
    {
        private readonly FakeDb _db;
        private readonly FakeDbParameterCollection _parameters = new();

        public FakeDbCommand(FakeDb db) => _db = db;

        public override string CommandText { get; set; } = string.Empty;

        public override int CommandTimeout { get; set; }

        public override CommandType CommandType { get; set; } = CommandType.Text;

        public override bool DesignTimeVisible { get; set; }

        public override UpdateRowSource UpdatedRowSource { get; set; }

        protected override DbConnection? DbConnection { get; set; }

        protected override DbParameterCollection DbParameterCollection => _parameters;

        protected override DbTransaction? DbTransaction { get; set; }

        public override void Cancel() { }

        public override void Prepare() { }

        protected override DbParameter CreateDbParameter() => new FakeDbParameter();

        public override int ExecuteNonQuery()
        {
            Record();
            return _db.NonQueryFor(CommandText);
        }

        public override object? ExecuteScalar()
        {
            Record();
            return _db.ScalarFor(CommandText);
        }

        protected override DbDataReader ExecuteDbDataReader(CommandBehavior behavior)
        {
            Record();
            return new FakeDbDataReader(_db.RowsFor(CommandText));
        }

        private void Record()
        {
            var snapshot = _parameters
                .Cast<DbParameter>()
                .ToDictionary(parameter => parameter.ParameterName, parameter => Unwrap(parameter.Value));

            _db.Executed.Add(new RecordedCommand(CommandText, snapshot));
        }

        private static object? Unwrap(object? value) => value == DBNull.Value ? null : value;
    }

    internal sealed class FakeDbParameter : DbParameter
    {
        public override DbType DbType { get; set; }

        public override ParameterDirection Direction { get; set; } = ParameterDirection.Input;

        public override bool IsNullable { get; set; }

        public override string ParameterName { get; set; } = string.Empty;

        public override int Size { get; set; }

        public override string SourceColumn { get; set; } = string.Empty;

        public override bool SourceColumnNullMapping { get; set; }

        public override object? Value { get; set; }

        public override void ResetDbType() { }
    }

    internal sealed class FakeDbParameterCollection : DbParameterCollection
    {
        private readonly List<DbParameter> _items = new();

        public override int Count => _items.Count;

        public override object SyncRoot { get; } = new();

        public override int Add(object value)
        {
            _items.Add((DbParameter)value);
            return _items.Count - 1;
        }

        public override void AddRange(Array values)
        {
            foreach (var value in values)
            {
                Add(value);
            }
        }

        public override void Clear() => _items.Clear();

        public override bool Contains(object value) => _items.Contains((DbParameter)value);

        public override bool Contains(string value) => IndexOf(value) >= 0;

        public override void CopyTo(Array array, int index) => ((ICollection)_items).CopyTo(array, index);

        public override IEnumerator GetEnumerator() => _items.GetEnumerator();

        public override int IndexOf(object value) => _items.IndexOf((DbParameter)value);

        public override int IndexOf(string parameterName) =>
            _items.FindIndex(parameter => parameter.ParameterName == parameterName);

        public override void Insert(int index, object value) => _items.Insert(index, (DbParameter)value);

        public override void Remove(object value) => _items.Remove((DbParameter)value);

        public override void RemoveAt(int index) => _items.RemoveAt(index);

        public override void RemoveAt(string parameterName) => RemoveAt(IndexOf(parameterName));

        protected override DbParameter GetParameter(int index) => _items[index];

        protected override DbParameter GetParameter(string parameterName) => _items[IndexOf(parameterName)];

        protected override void SetParameter(int index, DbParameter value) => _items[index] = value;

        protected override void SetParameter(string parameterName, DbParameter value) =>
            _items[IndexOf(parameterName)] = value;
    }

    internal sealed class FakeDbDataReader : DbDataReader
    {
        private readonly IReadOnlyList<FakeRow> _rows;
        private int _index = -1;

        public FakeDbDataReader(IReadOnlyList<FakeRow> rows) => _rows = rows;

        private FakeRow Current => _rows[_index];

        public override int Depth => 0;

        public override int FieldCount => _rows.Count == 0 ? 0 : _rows[0].Count;

        public override bool HasRows => _rows.Count > 0;

        public override bool IsClosed { get; }

        public override int RecordsAffected => 0;

        public override object this[int ordinal] => GetValue(ordinal);

        public override object this[string name] => GetValue(GetOrdinal(name));

        public override bool Read() => ++_index < _rows.Count;

        public override Task<bool> ReadAsync(CancellationToken cancellationToken) => Task.FromResult(Read());

        public override bool NextResult() => false;

        public override int GetOrdinal(string name) => Current.Ordinal(name);

        public override string GetName(int ordinal) => Current.Name(ordinal);

        public override object GetValue(int ordinal) => Current.Value(ordinal)!;

        public override T GetFieldValue<T>(int ordinal) => (T)GetValue(ordinal);

        public override bool IsDBNull(int ordinal) => Current.Value(ordinal) is null;

        public override int GetValues(object[] values)
        {
            var count = Math.Min(values.Length, FieldCount);
            for (var i = 0; i < count; i++)
            {
                values[i] = GetValue(i);
            }

            return count;
        }

        public override bool GetBoolean(int ordinal) => (bool)GetValue(ordinal);

        public override byte GetByte(int ordinal) => (byte)GetValue(ordinal);

        public override long GetBytes(int ordinal, long dataOffset, byte[]? buffer, int bufferOffset, int length) =>
            throw new NotSupportedException();

        public override char GetChar(int ordinal) => (char)GetValue(ordinal);

        public override long GetChars(int ordinal, long dataOffset, char[]? buffer, int bufferOffset, int length) =>
            throw new NotSupportedException();

        public override string GetDataTypeName(int ordinal) => GetFieldType(ordinal).Name;

        public override DateTime GetDateTime(int ordinal) => (DateTime)GetValue(ordinal);

        public override decimal GetDecimal(int ordinal) => (decimal)GetValue(ordinal);

        public override double GetDouble(int ordinal) => (double)GetValue(ordinal);

        public override Type GetFieldType(int ordinal) => GetValue(ordinal).GetType();

        public override float GetFloat(int ordinal) => (float)GetValue(ordinal);

        public override Guid GetGuid(int ordinal) => (Guid)GetValue(ordinal);

        public override short GetInt16(int ordinal) => (short)GetValue(ordinal);

        public override int GetInt32(int ordinal) => (int)GetValue(ordinal);

        public override long GetInt64(int ordinal) => (long)GetValue(ordinal);

        public override string GetString(int ordinal) => (string)GetValue(ordinal);

        public override IEnumerator GetEnumerator() => _rows.GetEnumerator();
    }
}
