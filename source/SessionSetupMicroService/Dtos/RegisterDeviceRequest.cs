using System.Text.Json.Serialization;

namespace SessionSetupMicroService.Dtos
{
    public sealed record RegisterDeviceRequest
    {
        [JsonPropertyName("deviceName")]
        public required string DeviceName { get; set; }

        [JsonPropertyName("registrationId")]
        public required int RegistrationId { get; set; }

        [JsonPropertyName("identityKey")]
        public required PublicKeyDto IdentityKey { get; set; }

        [JsonPropertyName("signedPreKey")]
        public required SignedPreKeyDto SignedPreKey { get; set; }

        [JsonPropertyName("lastResortKyberPreKey")]
        public required SignedPreKeyDto LastResortKyberPreKey { get; set; }

        [JsonPropertyName("oneTimePreKeys")]
        public required IEnumerable<OneTimePreKeyDto> OneTimePreKeys { get; set; }

        [JsonPropertyName("oneTimeKyberPreKeys")]
        public required IEnumerable<SignedPreKeyDto> OneTimeKyberPreKeys { get; set; }
    }
}
