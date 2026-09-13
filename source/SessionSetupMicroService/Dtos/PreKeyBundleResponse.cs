namespace SessionSetupMicroService.Dtos
{
    public class PreKeyBundleResponse
    {
        public required string Address { get; set; }

        public required int RegistrationId { get; set; }

        public required PublicKeyDto IdentityKey { get; set; }

        public required SignedPreKeyDto SignedPreKey { get; set; }

        public OneTimePreKeyDto? OneTimePreKey { get; set; }

        public required SignedPreKeyDto KyberPreKey { get; set; }

        public required bool ServedLastResportKyberPreKey { get; set; }
    }
}
