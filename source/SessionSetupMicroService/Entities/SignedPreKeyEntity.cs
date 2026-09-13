namespace SessionSetupMicroService.Entities
{
    public class SignedPreKeyEntity
    {
        public required Guid AccountId { get; init; }

        public required int DeviceId { get; init; }

        public required string Kind { get; init; }

        public required long KeyId { get; init; }

        public required byte[] PublicKey { get; init; }

        public required byte[] Signature { get; init; }

        public required DateTimeOffset CreatedAt { get; init; }
    }
}
