using Microsoft.AspNetCore.Identity;

namespace UserMicroService.Entities
{
    public class RoleClaimEntity : IdentityRoleClaim<Guid>
    {
        public virtual RoleEntity? Role {  get; set; }
    }
}
