namespace SessionSetupMicroService.Models
{
    public sealed record DeviceRegistrationResult
    {
        public Device Device {  get; set; }

        public string DeviceCredentials { get; set; }

        public DeviceRegistrationResult(Device device, string deviceCredentials)
        {
            Device = device;
            DeviceCredentials = deviceCredentials;
        }


    }
}
