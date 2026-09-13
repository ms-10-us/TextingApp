namespace SessionSetupMicroService.Dtos
{
    public class PreKeyInventoryResponse
    {
        public required int OneTimePreKeys {  get; set; }

        public required int OneTimeKyberPreKeys { get; set; }

        public required bool NeedsReplenishment { get; set; }

        public required int LowWaterMark { get; set; }

        public required int MaxPoolSize { get; set; }
    }
}
