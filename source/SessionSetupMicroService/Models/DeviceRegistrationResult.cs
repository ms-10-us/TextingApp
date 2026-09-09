namespace SessionSetupMicroService.Models
{
    public sealed record DeviceRegistrationResult
    {
        public required Device Device {  get; set; }

        public required string DeviceCredentials { get; set; }
    }
}
