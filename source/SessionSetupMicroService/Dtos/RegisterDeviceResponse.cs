using System.Text.Json.Serialization;

namespace SessionSetupMicroService.Dtos
{
    public sealed record RegisterDeviceResponse
    {
        [JsonPropertyName("accountId")]
        public Guid AccountId { get; set; }

        [JsonPropertyName("deviceId")]
        public required int DeviceId { get; set; }

        [JsonPropertyName("address")]
        public required string Address { get; set; }

        [JsonPropertyName("deviceCredential")]
        public required string DeviceCredential { get; set; }

        [JsonPropertyName("registeredAt")]
        public required DateTimeOffset RegisteredAt { get; set; }
    }
}
