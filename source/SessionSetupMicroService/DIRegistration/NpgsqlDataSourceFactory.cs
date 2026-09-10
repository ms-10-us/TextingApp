using Npgsql;
using System.Data.Common;

namespace SessionSetupMicroService.DIRegistration
{
    public static class NpgsqlDataSourceFactory
    {
        public static DbDataSource Create(string connectionString, ILoggerFactory loggerFactory)
        {
            var settings = new NpgsqlConnectionStringBuilder(connectionString)
            {
                MaxPoolSize = 64,
                Timeout = 10,
                CommandTimeout = 15,
                ApplicationName = "session-setup"
            };

            var builder = new NpgsqlDataSourceBuilder(settings.ConnectionString);
            builder.UseLoggerFactory(loggerFactory);
            return builder.Build();
        }
    }
}
