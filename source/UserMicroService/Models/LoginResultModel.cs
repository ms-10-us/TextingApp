namespace UserMicroService.Models
{
    public class LoginResultModel
    {
        public required AccessTokenModel AccessToken {  get; init; }

        public required UserModel User { get; init; }
    }
}
