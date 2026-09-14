namespace SessionSetupMicroService.Dtos
{
    public class PublishPreKeysDto
    {
        public SignedPreKeyDto? SignedPreKey { get; set; }

        public SignedPreKeyDto? LastResortKyberPreKey { get; set; }

        public IEnumerable<OneTimePreKeyDto>? OneTimePreKeys { get; set; }

        public IEnumerable<SignedPreKeyDto>? OneTimeKyberPreKeys { get; set; }
    }
}
