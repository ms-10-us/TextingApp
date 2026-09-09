namespace SessionSetupMicroService.Models
{
    public sealed record RegisterDeviceModel
    {
        public required string DisplayName { get; set; }

        public required RegistrationId RegistrationId { get; set; }

        public required PublicKey IdentityKey { get; set; }

        public required SignedPreKey SignedPreKey { get; set; }

        public required SignedPreKey LastResortKyberPreKey { get; set; }

        public required IEnumerable<OneTimePreKey> OneTimePreKeys { get; set; }

        public required IEnumerable<OneTimePreKey> OneTimeKyberPreKeys { get; set; }
    }
}
