namespace SessionSetupMicroService.Models
{
    public class PublishPreKeys
    {
        public required SignedPreKey? SignedPreKey { get; set; }

        public required SignedPreKey? LastResortKyberPreKey { get; set; }

        public required IEnumerable<OneTimePreKey> OneTimePreKeys { get; set; }

        public required IEnumerable<OneTimePreKey> OneTimeKyberPreKeys { get; set; }

        public bool IsEmpty =>
        SignedPreKey is null && LastResortKyberPreKey is null &&
        OneTimePreKeys.Count() == 0 && OneTimeKyberPreKeys.Count() == 0;
    }
}
