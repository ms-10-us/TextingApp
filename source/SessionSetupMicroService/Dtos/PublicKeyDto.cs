using System.Text.Json.Serialization;

namespace SessionSetupMicroService.Dtos
{
    public sealed record PublicKeyDto
    {
        [JsonPropertyName("algorithm")]
        public required string Algorithm { get; set; }

        [JsonPropertyName("key")]
        public required byte[] Key { get; set; }
    }
}
