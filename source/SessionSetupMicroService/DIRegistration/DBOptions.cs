using System.ComponentModel.DataAnnotations;

namespace SessionSetupMicroService.DIRegistration
{
    public class DBOptions
    {
        public const string SectionName = "Database";

        public string Provider { get; set; } = "Postgres";

        [Required]
        public string ConnectionString { get; set; } =
            "Host=localhost;Port=5432;Database=sessionsetup;Username=postgres;Password=postgres";

        public bool MigrateOnStartup { get; set; } = true;
    }
}
