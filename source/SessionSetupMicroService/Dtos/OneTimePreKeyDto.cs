using System.Text.Json.Serialization;

namespace SessionSetupMicroService.Dtos
{
    public sealed record OneTimePreKeyDto
    {
        [JsonPropertyName("keyId")]
        public required long KeyId { get; set; }

        [JsonPropertyName("publicKey")]
        public required PublicKeyDto PublicKey { get; set; }

    }
}
