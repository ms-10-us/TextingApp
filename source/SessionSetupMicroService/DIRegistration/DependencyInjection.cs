using SessionSetupMicroService.Options;
using SessionSetupMicroService.Orchestrators;
using SessionSetupMicroService.PostgresDB;
using SessionSetupMicroService.Repositories;
using SessionSetupMicroService.Security;
using System.Data.Common;

namespace SessionSetupMicroService.DIRegistration
{
    public static class DependencyInjection
    {
        public static IServiceCollection AddSessionSetup(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddOptions<PreKeyPolicyOptions>()
                .Bind(configuration.GetSection(PreKeyPolicyOptions.SectionName))
                .ValidateDataAnnotations()
                .ValidateOnStart();

            services.AddOptions<DBOptions>()
                .Bind(configuration.GetSection(DBOptions.SectionName))
                .ValidateDataAnnotations()
                .ValidateOnStart();

            var database = configuration.GetSection(DBOptions.SectionName).Get<DBOptions>() ?? new DBOptions();
            services.AddSingleton(database);

            services.TryAddTimeProvider();

            if (string.Equals(database.Provider, "InMemory", StringComparison.OrdinalIgnoreCase))
            {
                services.AddInMemoryPersistence();
            }
            else
            {
                services.AddPostgresPersistence(database);
            }

            services.AddSingleton<IDeviceCredentialHasher, Sha256DeviceCredentialHasher>();

            services.AddScoped<IDeviceRegistrationOrchestrator, DeviceRegistrationOrchestrator>();
            services.AddScoped<IPreKeyOrchestrator, PreKeyOrchestrator>();
            services.AddScoped<IDeviceAuthenticator, DeviceAuthenticator>();

            return services;
        }

        public static async Task MigrateDatabaseAsync(this IServiceProvider services, CancellationToken ct = default)
        {
            var options = services.GetService<DBOptions>();
            if (options is not { MigrateOnStartup: true})
            {
                return;
            }

            if (string.Equals(options.Provider, "InMemory", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            using var scope = services.CreateScope();
            {
                await scope.ServiceProvider.GetRequiredService<DatabaseMigrator>().MigrateAsync(ct);
            }
        }

        private static void AddPostgresPersistence(this IServiceCollection services, DBOptions database)
        {
            services.AddSingleton<DbDataSource>(provider =>
                NpgsqlDataSourceFactory.Create(database.ConnectionString, provider.GetRequiredService<ILoggerFactory>()));

            services.AddSingleton<DatabaseMigrator>();

            services.AddScoped<PostgresConnectionScope>();
            services.AddScoped<IUnitOfWork, PostgresUnitOfWork>();
            services.AddScoped<IDeviceRepository, DeviceRepository>();
            services.AddScoped<IPreKeyRepository, PreKeyRepository>();
        }

        private static void AddInMemoryPersistence(this IServiceCollection services)
        {
            services.AddSingleton<InMemoryStore>();
            services.AddScoped<IUnitOfWork, InMemoryUnitOfWork>();
            services.AddScoped<IDeviceRepository, InMemoryDeviceRepository>();
            services.AddScoped<IPreKeyRepository, InMemoryPreKeyRepository>();
        }

        private static void TryAddTimeProvider(this IServiceCollection services)
        {
            if (services.Any(descriptor => descriptor.ServiceType == typeof(TimeProvider)))
            {
                return;
            }

            services.AddSingleton(TimeProvider.System);
        }
    }
}
