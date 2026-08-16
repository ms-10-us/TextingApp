namespace UserMicroService.Models
{
    public sealed class UserModel
    {
        public required Guid Id { get; init; }

        public required string Email { get; init; }

        public required string UserName { get; init; }

        public required string DisplayName { get; init; }

        public DateTime? LastSeen { get; init; }
    }
}
