using Microsoft.AspNetCore.Identity;

namespace UserMicroService.Entities
{
    public class UserLoginEntity : IdentityUserLogin<Guid>
    {
        public virtual UserEntity? User { get; set; }
    }
}
