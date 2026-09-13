namespace SessionSetupMicroService.Models
{
    public class PreKeyInventory
    {
        public required int Curve { get; set; }

        public required int Kyber { get; set; }

        public static readonly PreKeyInventory Empty = new PreKeyInventory
        {
            Curve = 0,
            Kyber = 0
        };

        public bool IsBelow(int lowWaterMark) => Curve < lowWaterMark || Kyber < lowWaterMark;
    }
}
