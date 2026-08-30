using Microsoft.Data.Sqlite;
using System.Data;

namespace BitcoinWalletMicroService.DBSqlite
{
    public class DbConnectionFactory : IDbConnectionFactory
    {
        private readonly string _connectionString;
        private readonly int _busyTimeoutMs;

        public DbConnectionFactory(WalletDbOptions options)
        {
            var builder = new SqliteConnectionStringBuilder
            {
                DataSource = options.DbPath,
                Mode = SqliteOpenMode.ReadWriteCreate,
                Cache = SqliteCacheMode.Shared,
                Pooling = true
            };

            _connectionString = builder.ToString();
            _busyTimeoutMs = options.BusyTimeoutMs;
        }

        public IDbConnection CreateOpenConnection()
        {
            var connection = new SqliteConnection(_connectionString);
            connection.Open();

            using var pragma = connection.CreateCommand();

            pragma.CommandText =
                "PRAGMA foreign_keys = ON;" +
                "PRAGMA journal_mode = WAL;" +
                $"PRAGMA busy_timeout = {_busyTimeoutMs};";
            pragma.ExecuteNonQuery();

            return connection;
        }


    }
}
