namespace UserMicroService.Models
{
    public sealed class AddUserModel
    {
        public required string Email { get; init; }

        public required string UserName { get; init; }

        public required string DisplayName { get; init; }

        public required string Password { get; init; }
    }
}
