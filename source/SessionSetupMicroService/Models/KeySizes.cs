namespace SessionSetupMicroService.Models
{
    public static class KeySizes
    {
        public const int Ed25519PublicKey = 32;

        public const int Ed25519Signature = 64;

        public const int X25519PublicKey = 32;

        public const int Kyber1024PublicKey = 1568;

        public static int? ExpectedPublicKeySize(string algorithm) => algorithm switch
        {
            KeyAlgorithms.Ed25519 => Ed25519PublicKey,
            KeyAlgorithms.X25519 => X25519PublicKey,
            KeyAlgorithms.Kyber1024 => Kyber1024PublicKey,
            _ => null
        };
    }
}
