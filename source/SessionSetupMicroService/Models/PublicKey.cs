namespace SessionSetupMicroService.Models
{
    public sealed record PublicKey
    {
        public required string Algorithm { get; set; }

        public required byte[] Value { get; set; }

        public int Length => Value.Length;
    }
}
