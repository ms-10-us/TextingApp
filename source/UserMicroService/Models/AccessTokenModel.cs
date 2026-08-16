namespace UserMicroService.Models
{
    public class AccessTokenModel
    {
        public required string Token { get; init; }

        public required DateTime ExpiresAtUtc { get; init; }
    }
}
