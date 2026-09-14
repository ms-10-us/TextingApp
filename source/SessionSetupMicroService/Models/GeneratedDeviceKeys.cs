namespace SessionSetupMicroService.Models
{
    public class GeneratedDeviceKeys
    {
        public required KeyPair IdentityKeyPair { get; set; }

        public required int RegistrationId { get; set; }

        public required GeneratedSignedPreKey SignedPreKey { get; set; }

        public required GeneratedSignedPreKey LastResortKyberPreKey { get; set; }

        public required IEnumerable<GeneratedOneTimePreKey> OneTimePreKeys { get; set; }

        public required IEnumerable<GeneratedSignedPreKey> OneTimeKyberPreKeys { get; set; }
    }
}
