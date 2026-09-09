using System.Text.Json.Serialization;

namespace SessionSetupMicroService.Dtos
{
    public sealed record SignedPreKeyDto
    {
        [JsonPropertyName("keyId")]
        public required long KeyId { get; set; }

        [JsonPropertyName("publicKey")]
        public required PublicKeyDto PublicKey { get; set; }

        [JsonPropertyName("signature")]
        public required byte[] Signature { get; set; }
    }
}
