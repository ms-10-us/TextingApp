using Microsoft.AspNetCore.Identity;

namespace UserMicroService.Entities
{
    public class UserTokenEntity : IdentityUserToken<Guid>
    {
        public virtual UserEntity? User { get; set; }
    }
}
