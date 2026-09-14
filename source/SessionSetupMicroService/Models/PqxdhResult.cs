namespace SessionSetupMicroService.Models
{
    public class PqxdhResult
    {
        public required byte[] RootKey { get; set; }

        public required byte[] InitialMessage { get; set; }
    }
}
