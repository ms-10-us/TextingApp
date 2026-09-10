using System.Text.Json.Serialization;

namespace SessionSetupMicroService.Dtos
{
    public record DeviceResponse
    {
        [JsonPropertyName("deviceId")]
        public required int DeviceId { get; set; }

        [JsonPropertyName("address")]
        public required string Address { get; set; }

        [JsonPropertyName("deviceName")]
        public required string DeviceName { get; set; }

        [JsonPropertyName("registrationId")]
        public required int RegistrationId { get; set; }

        [JsonPropertyName("identityKey")]
        public required PublicKeyDto IdentityDto { get; set; }

        [JsonPropertyName("registeredAt")]
        public required DateTimeOffset RegisteredAt { get; set; }

        [JsonPropertyName("lastSeenAt")]
        public required DateTimeOffset LastSeenAt { get; set; }
    }
}
