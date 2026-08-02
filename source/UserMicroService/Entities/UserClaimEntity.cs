using Microsoft.AspNetCore.Identity;

namespace UserMicroService.Entities
{
    public class UserClaimEntity : IdentityUserClaim<Guid>
    {
        public virtual UserEntity? User { get; set; }
    }
}
