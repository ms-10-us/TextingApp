using UserMicroService.Dtos;
using UserMicroService.Models;

namespace UserMicroService.Mappings
{
    public static class AuthMappings
    {

        public static LoginModel ToModel(this LoginDto dto)
        {
            ArgumentNullException.ThrowIfNull(dto);

            return new LoginModel
            {
                Identifier = dto.Identifier.Trim(),
                Password = dto.Password
            };
        }

        public static LoginDtoResponse ToDto(this LoginResultModel model)
        {
            ArgumentNullException.ThrowIfNull(model);

            return new LoginDtoResponse
            {
                TokenType = "Bearer",
                AccessToken = model.AccessToken.Token,
                ExpiresAtUtc = model.AccessToken.ExpiresAtUtc,
                User = model.User.ToDto()
            };
        }


    }

}
