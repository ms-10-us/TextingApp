namespace SessionSetupMicroService.Entities
{
    public class DeviceEntity
    {
        public Guid AccountId { get; init; }

        public int DeviceId { get; init; }

        public required string DisplayName { get; init; }

        public int RegistrationId { get; init; }

        public required string IdentityAlgorithm { get; init; }

        public required byte[] IdentityKey { get; init; }

        public required byte[] CredentialHash { get; init; }

        public DateTimeOffset RegisteredAt { get; init; }

        public DateTimeOffset LastSeenAt { get; init; }
    }
}
