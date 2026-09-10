using System.ComponentModel.DataAnnotations;

namespace SessionSetupMicroService.Options
{
    public class PreKeyPolicyOptions
    {
        public const string SectionName = "SessionSetup:PreKeys";

        [Range(1, 10_000)]
        public int LowWaterMark { get; set; } = 20;

        [Range(1, 100_000)]
        public int MaxPoolSize { get; set; } = 500;

        [Range(1, 10_000)]
        public int MaxKeysPerUpload { get; set; } = 200;

        public TimeSpan SignedPreKeyMaxAge { get; set; } = TimeSpan.FromDays(30);
    }
}
