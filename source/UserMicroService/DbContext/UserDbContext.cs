using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using UserMicroService.Entities;

namespace UserMicroService.DbContext
{
    public class UserDbContext : IdentityDbContext<UserEntity, RoleEntity, Guid,
    UserClaimEntity, UserRoleEntity, UserLoginEntity,
    RoleClaimEntity, UserTokenEntity>
    {
        public UserDbContext(DbContextOptions<UserDbContext> options) : base(options)
        {

        }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);

            builder.Entity<UserEntity>(b =>
            {
                b.ToTable("Users");
                b.Property(u => u.DisplayName).HasMaxLength(64).IsRequired();
                b.HasMany(u => u.Claims).WithOne(c => c.User).HasForeignKey(c => c.UserId).IsRequired();
                b.HasMany(u => u.Logins).WithOne(l => l.User).HasForeignKey(l => l.UserId).IsRequired();
                b.HasMany(u => u.Tokens).WithOne(t => t.User).HasForeignKey(t => t.UserId).IsRequired();
                b.HasMany(u => u.UserRoles).WithOne(ur => ur.User).HasForeignKey(ur => ur.UserId).IsRequired();
            });

            builder.Entity<RoleEntity>(b =>
            {
                b.ToTable("Roles");
                b.HasMany(r => r.UserRoles).WithOne(ur => ur.Role).HasForeignKey(ur => ur.RoleId).IsRequired();
                b.HasMany(r => r.RoleClaims).WithOne(rc => rc.Role).HasForeignKey(rc => rc.RoleId).IsRequired();
            });

            builder.Entity<UserClaimEntity>().ToTable("UserClaims");
            builder.Entity<UserRoleEntity>().ToTable("UserRoles");
            builder.Entity<UserLoginEntity>().ToTable("UserLogins");
            builder.Entity<RoleClaimEntity>().ToTable("RoleClaims");
            builder.Entity<UserTokenEntity>().ToTable("UserTokens");
        }

    }
}
