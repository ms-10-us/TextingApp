namespace UserMicroService.Dtos
{
    public class LoginDtoResponse
    {
        public string TokenType { get; set; } = "Bearer";

        public string AccessToken {  get; set; } = string.Empty;

        public DateTime ExpiresAtUtc { get; set; }

        public UserDto User { get; set; } = new UserDto();

    }
}
