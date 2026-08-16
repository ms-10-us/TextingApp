namespace UserMicroService.Models
{
    public sealed class LoginModel
    {
        public required string Identifier { get; init; }

        public required string Password { get; init; }
    }
}
