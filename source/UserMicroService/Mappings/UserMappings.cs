using Microsoft.Identity.Client;
using UserMicroService.Dtos;
using UserMicroService.Entities;
using UserMicroService.Models;

namespace UserMicroService.Mappings
{
    public static class UserMappings
    {
        public static AddUserModel ToModel(this AddUserDto dto)
        {
            ArgumentNullException.ThrowIfNull(dto);

            return new AddUserModel
            {
                Email = dto.Email.Trim(),
                UserName = dto.Username.Trim(),
                DisplayName = dto.DisplayName.Trim(),
                Password = dto.Password
            };
        }

        public static UserDto ToDto(this UserModel model)
        {
            ArgumentNullException.ThrowIfNull(model);

            return new UserDto
            {
                Id = model.Id,
                Email = model.Email,
                UserName = model.UserName,
                DisplayName = model.DisplayName,
                LastSeen = model.LastSeen
            };
        }

        public static UserEntity ToEntity(this AddUserModel model)
        {
            ArgumentNullException.ThrowIfNull(model);

            return new UserEntity
            {
                Id = Guid.CreateVersion7(),
                Email = model.Email,
                UserName = model.UserName,
                DisplayName = model.DisplayName,
                EmailConfirmed = false
            };
        }

        public static UserModel ToModel(this UserEntity entity)
        {
            ArgumentNullException.ThrowIfNull(entity);

            return new UserModel
            {
                Id = entity.Id,
                Email = entity.Email ?? string.Empty,
                UserName = entity.UserName ?? string.Empty,
                DisplayName = entity.DisplayName,
                LastSeen = entity.LastSeen
            };
        }

    }
}
