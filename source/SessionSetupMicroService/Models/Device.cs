namespace SessionSetupMicroService.Models
{
    public sealed record Device
    {
        public required ProtocolAddress Address { get; set; }

        public required string DisplayName { get; set; }

        public required RegistrationId RegistrationId { get; set; }

        public required PublicKey IdentityKey { get; set; }

        public required DateTimeOffset RegisteredAt { get; set; }

        public required DateTimeOffset LastSeen { get; set; }
    }
}
