using System.Data;

namespace BitcoinWalletMicroService.DBSqlite
{
    public class DbInitializer
    {
        private readonly IDbConnectionFactory _connectionFactory;

        private readonly ILogger<DbInitializer> _logger;

        public DbInitializer(IDbConnectionFactory connectionFactory, ILogger<DbInitializer> logger)
        {
            _connectionFactory = connectionFactory;
            _logger = logger;
        }

        public void Initialize(string schemaPath)
        {
            if (!File.Exists(schemaPath))
            {
                throw new FileNotFoundException($"Schema file not found at '{schemaPath}'. Confirm SQL/Schema.sql is " +
                    "marked as Content with CopyToOutputDirectory in the .csproj.",
                    schemaPath);
            }

            string? directory = Path.GetDirectoryName(Path.GetFullPath(schemaPath));
            _logger.LogInformation("Applying schema from {schemaPath}.", schemaPath);

            using IDbConnection connection = _connectionFactory.CreateOpenConnection();
            using IDbCommand command = connection.CreateCommand();
            command.CommandText = File.ReadAllText(schemaPath);
            command.ExecuteNonQuery();

            _logger.LogInformation("Schema applied successfully.");
        }
    }
}
