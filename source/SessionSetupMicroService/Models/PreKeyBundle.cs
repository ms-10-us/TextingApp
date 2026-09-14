namespace SessionSetupMicroService.Models
{
    public class PreKeyBundle
    {
        public required ProtocolAddress Address { get; set; }

        public required RegistrationId RegistrationId { get; set; }

        public required PublicKey IdentityKey { get; set; }

        public required SignedPreKey SignedPreKey { get; set; }

        public required OneTimePreKey? OneTimePreKey { get; set; }

        public required SignedPreKey KyberPreKey { get; set; }

        public required bool ServedLastResortKyberPreKey { get; set; }

        public bool IsFullStrength()
        {
            return (OneTimePreKey != null && !ServedLastResortKyberPreKey);
        }

    }
}
