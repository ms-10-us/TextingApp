using System.Text.Json.Serialization;

namespace SessionSetupMicroService.Dtos
{
    public sealed record RegisterDeviceResponse
    {
        [JsonPropertyName("accountId")]
        public Guid AccountId;

        [JsonPropertyName("deviceId")]
        public required int DeviceId;

        [JsonPropertyName("address")]
        public required string Address;

        [JsonPropertyName("deviceCredential")]
        public required string DeviceCredential;

        [JsonPropertyName("registeredAt")]
        public required DateTimeOffset RegisteredAt;
    }
}
