using SessionSetupMicroService.Enums;

namespace SessionSetupMicroService.Models
{
    public sealed record OneTimePreKey
    {
        public required PreKeyKind Kind { get; set; }

        public required PreKeyId Id { get; set; }

        public required PublicKey PublicKey { get; set; }

        public byte[]? Signature { get; set; }

        public bool IsSigned => Signature is { Length: > 0 };
        
    }
}
