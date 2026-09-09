using System.Data;
using System.Data.Common;

namespace SessionSetupMicroService.PostgresDB
{
    public sealed class PostgresConnectionScope : IAsyncDisposable
    {
        private DbConnection? _connection;
        private DbTransaction? _transaction;

        public readonly DbDataSource _dataSource;

        public PostgresConnectionScope(DbDataSource dataSource)
        {
            _dataSource = dataSource;
        }

        public bool InTransaction => _transaction != null;

        public async ValueTask<DbCommand> CreateCommandAsync(string sql, CancellationToken ct)
        {
            var connection = await GetConnectionAsync(ct).ConfigureAwait(false);
            var command = connection.CreateCommand();
            command.CommandText = sql;
            command.Transaction = _transaction;
            return command;
        }

        public async ValueTask BeginAsync(CancellationToken ct)
        {
            if (_transaction != null)
            {
                throw new InvalidOperationException("A transaction is alread open on this scope."); 
            }

            var connection = await GetConnectionAsync(ct).ConfigureAwait(false);

            _transaction = await connection
                .BeginTransactionAsync(IsolationLevel.ReadCommitted, ct).ConfigureAwait(false);
        }

        public async ValueTask CommitAsync(CancellationToken ct)
        {
            if (_transaction == null)
            {
                return;
            }

            await _transaction.CommitAsync(ct).ConfigureAwait(false);
            await DisposeTransactionAsync().ConfigureAwait(false);
        }

        public async ValueTask RollbackAsync(CancellationToken ct)
        {
            if (_transaction == null)
            {
                return;
            }
            try
            {
                await _transaction.RollbackAsync(ct).ConfigureAwait(false);
            }
            finally
            {
                await DisposeTransactionAsync().ConfigureAwait(false);
            }
        }

        public async ValueTask DisposeAsync()
        {
            await RollbackAsync(CancellationToken.None).ConfigureAwait(false);
            if (_connection != null)
            {
                await _connection.DisposeAsync().ConfigureAwait(false);
            }

            _connection = null;
        }

        private async ValueTask<DbConnection> GetConnectionAsync(CancellationToken ct)
        {
            return _connection ??= await _dataSource.OpenConnectionAsync(ct).ConfigureAwait(false);
        }

        private async ValueTask DisposeTransactionAsync()
        {
            if (_transaction == null)
            {
                return;
            }

            await _transaction.DisposeAsync().ConfigureAwait(false);
            _transaction = null;
        }
    }
}
