using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using UserMicroService.DbContext;
using UserMicroService.Entities;

namespace UserMicroService
{
    public class Program
    {
        public static void Main(string[] args)
        {
            DotNetEnv.Env.TraversePath().Load();

            var builder = WebApplication.CreateBuilder(args);

            var connectionString = builder.Configuration.GetConnectionString("UserDb")
                ?? throw new InvalidOperationException("ConnectionString:UserDb is not configured");

            builder.Services.AddDbContext<UserDbContext>(o =>
                o.UseSqlServer(connectionString, sql =>
                    sql.EnableRetryOnFailure(
                        maxRetryCount: 3,
                        maxRetryDelay: TimeSpan.FromSeconds(5),
                        errorNumbersToAdd: null)));

            var keyPath = builder.Environment.IsDevelopment() && !OperatingSystem.IsLinux()
                          ? Path.Combine(builder.Environment.ContentRootPath, "keys")
                          : "/data/keys";
            var keyRing = new DirectoryInfo(keyPath);
            keyRing.Create();
            builder.Services.AddDataProtection()
                .PersistKeysToFileSystem(keyRing)
                .SetApplicationName("TextingApp");

            builder.Services.AddIdentityCore<UserEntity>(o =>
            {
                o.User.RequireUniqueEmail = true;
                o.Password.RequiredLength = 12;
                o.Lockout.MaxFailedAccessAttempts = 3;
                o.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
                .AddRoles<RoleEntity>()
                .AddSignInManager()
                .AddEntityFrameworkStores<UserDbContext>()
                .AddDefaultTokenProviders();

            builder.Services.AddAuthentication(IdentityConstants.ApplicationScheme)
                .AddIdentityCookies();

            builder.Services.AddControllers();
            builder.Services.AddOpenApi();

            builder.Services.AddSwaggerGen();

            var app = builder.Build();

            if (app.Environment.IsDevelopment())
            {
                app.MapOpenApi();

                app.UseSwagger();
                app.UseSwaggerUI();

                using var scopeUserDb = app.Services.CreateScope();
                var userDb = scopeUserDb.ServiceProvider.GetRequiredService<UserDbContext>();
                userDb.Database.CreateExecutionStrategy().Execute(() => userDb.Database.Migrate());
            }

            app.UseAuthentication();
            app.UseAuthorization();

            app.MapControllers();

            app.Run();
        }
    }
}
