namespace SessionSetupMicroService.Models
{
    public class GeneratedSignedPreKey
    {
        public required long KeyId { get; set; }

        public required KeyPair KeyPair { get; set; }

        public required byte[] Signature { get; set; }
    }
}
