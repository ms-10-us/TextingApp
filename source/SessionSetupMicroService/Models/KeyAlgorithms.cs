namespace SessionSetupMicroService.Models
{
    public static class KeyAlgorithms
    {
        public const string Ed25519 = "ed25519";

        public const string X25519 = "x25519";

        public const string Kyber1024 = "kyber1024";

        public static bool IsKnown(string algorithm) =>
            algorithm is Ed25519 or X25519 or Kyber1024;
    }
}
