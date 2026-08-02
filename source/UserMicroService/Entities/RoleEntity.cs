using Microsoft.AspNetCore.Identity;

namespace UserMicroService.Entities
{
    public class RoleEntity : IdentityRole<Guid>
    {
        public string? Description { get; set; }

        public virtual ICollection<UserRoleEntity> UserRoles { get; set; } = new List<UserRoleEntity>();

        public virtual ICollection<RoleClaimEntity> RoleClaims { get; set; } = new List<RoleClaimEntity>();
    }
}
