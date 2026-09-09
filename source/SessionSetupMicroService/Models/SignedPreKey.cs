using SessionSetupMicroService.Enums;

namespace SessionSetupMicroService.Models
{
    public sealed record SignedPreKey
    {
        public required PreKeyKind Kind { get; set; }

        public required PreKeyId Id { get; set; }

        public required PublicKey PublicKey { get; set; }

        public required byte[] Signature { get; set; }

        public required DateTimeOffset CreatedAt { get; set; }

        public bool IsStale(TimeSpan maxAge, TimeProvider clock) => clock.GetUtcNow() - CreatedAt > maxAge;
    }
}
