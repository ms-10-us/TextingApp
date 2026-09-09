using System.Data.Common;
using System.Reflection;
using System.Xml.Linq;

namespace SessionSetupMicroService.PostgresDB
{
    public class DatabaseMigrator
    {
        private const string EnsureHistoryTable = """
        create table if not exists public.schema_migrations (
            script      text        primary key,
            applied_at  timestamptz not null default now()
        )
        """;

        public readonly DbDataSource _dataSource;
        public readonly ILogger<DatabaseMigrator> _logger; 

        public DatabaseMigrator(DbDataSource dataSource, ILogger<DatabaseMigrator> logger)
        {
            _dataSource = dataSource;
            _logger = logger;
        }

        public async Task MigrateAsync(CancellationToken ct = default)
        {
            await using var connection = await _dataSource.OpenConnectionAsync(ct);
            {
                await ExecuteAsync(connection, null, EnsureHistoryTable, ct);

                foreach (var (name, sql) in LoadScripts())
                {
                    if (await AlreadyApplied(connection, name, ct))
                    {
                        _logger.LogDebug("Migration {Script} already applied", name);
                        continue;
                    }

                    _logger.LogInformation("Applying migration {Script}", name);
                    
                    await using var transaction = await connection.BeginTransactionAsync(ct);
                    try
                    {
                        await ExecuteAsync(connection, transaction, sql, ct);
                        await ExecuteAsync(connection, transaction, "insert into public.schema_migrations (script) values (@script)",
                            ct, ("@script", name));

                        await transaction.CommitAsync(ct);
                    }
                    catch (Exception)
                    {
                        await transaction.RollbackAsync(ct);
                        throw new Exception($"Error occured when committing script {name}");
                    }
                }
            }
        }

        private static async Task ExecuteAsync(
            DbConnection connection, DbTransaction? transaction, string sql, CancellationToken ct,
            params (string Name, object Value)[] parameters)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = sql;
            command.Transaction = transaction;

            foreach (var (name, value) in parameters)
            {
                var parameter = command.CreateParameter();
                parameter.ParameterName = name;
                parameter.Value = value;
                command.Parameters.Add(parameter);
            }

            await command.ExecuteNonQueryAsync(ct);
        }

        private static IEnumerable<(string Name, string Sql)> LoadScripts()
        {

            var assembly = Assembly.GetExecutingAssembly();
            const string marker = ".PostgresSQLScript.";

            var resources = assembly.GetManifestResourceNames()
                .Where(name => name.Contains(marker, StringComparison.Ordinal) && name.EndsWith(".sql", StringComparison.Ordinal))
                .OrderBy(name => name, StringComparer.Ordinal);

            foreach(var resource in resources)
            {
                using var stream = assembly.GetManifestResourceStream(resource);
                {
                    using var reader = new StreamReader(stream);
                    {
                        yield return (resource[(resource.LastIndexOf(marker, StringComparison.Ordinal) + marker.Length)..], reader.ReadToEnd());
                    }
                }
            }
        }

        private static async Task<bool> AlreadyApplied(DbConnection connection, string script, CancellationToken ct)
        {
            await using var command = connection.CreateCommand();
            {
                command.CommandText = "select exists (select 1 from public.schema_migrations where script = @script)";
                var parameter = command.CreateParameter();
                parameter.ParameterName = "@script";
                parameter.Value = script;
                command.Parameters.Add(parameter);
                return await command.ExecuteScalarAsync(ct) is true;
            }
        }
    }
}
