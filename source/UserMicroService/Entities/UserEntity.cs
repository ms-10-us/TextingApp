using Microsoft.AspNetCore.Identity;

namespace UserMicroService.Entities
{
    public class UserEntity : IdentityUser<Guid>
    {
        public required string DisplayName { get; set; }

        public DateTime? LastSeen { get; set; }

        public virtual ICollection<UserClaimEntity> Claims { get; set; } = new List<UserClaimEntity>();

        public virtual ICollection<UserLoginEntity> Logins { get; set; } = new List<UserLoginEntity>();

        public virtual ICollection<UserTokenEntity> Tokens { get; set; } = new List<UserTokenEntity>();

        public virtual ICollection<UserRoleEntity> UserRoles { get; set; } = new List<UserRoleEntity>();

    }
}
