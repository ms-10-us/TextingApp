namespace SessionSetupMicroService.Models
{
    public class PreKeyPublication
    {
        public SignedPreKey? SignedPreKey { get; set; }

        public SignedPreKey? LastResortKyberPreKey { get; set; }

        public required IEnumerable<OneTimePreKey> OneTimePreKeys { get; set; }

        public required IEnumerable<OneTimePreKey> OneTimeKyberPreKeys { get; set; }

        public PreKeyPublication(
            SignedPreKey? signedPreKey,
            SignedPreKey lastResortKyberPreKey,
            IEnumerable<OneTimePreKey> oneTimePreKeys,
            IEnumerable<OneTimePreKey> oneTimeKyberPreKeys)
        {
            SignedPreKey = signedPreKey;
            LastResortKyberPreKey = lastResortKyberPreKey;
            OneTimePreKeys = oneTimePreKeys;
            OneTimeKyberPreKeys = oneTimeKyberPreKeys;
        }

        public bool IsEmpty => 
            SignedPreKey == null && LastResortKyberPreKey == null 
            && OneTimePreKeys.Count() == 0 && OneTimeKyberPreKeys.Count() == 0;
    }
}
