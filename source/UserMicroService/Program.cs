using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using UserMicroService.Configuration;
using UserMicroService.DbContext;
using UserMicroService.Entities;
using UserMicroService.Orchestrators;
using UserMicroService.Repositories;
using UserMicroService.Security;

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

            builder.Services.AddOptions<JwtOptions>()
                .Bind(builder.Configuration.GetSection(JwtOptions.SectionName))
                .ValidateDataAnnotations()
                .ValidateOnStart();

            var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
                ?? throw new InvalidOperationException("The 'Jwt' configuration section is not configured");

            builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
                .AddJwtBearer(o =>
                {
                    o.MapInboundClaims = false;
                    o.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
                    o.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
                    {
                        ValidateIssuer = true,
                        ValidIssuer = jwtOptions.Issuer,
                        ValidateAudience = true,
                        ValidAudience = jwtOptions.Audience,
                        ValidateIssuerSigningKey = true,
                        IssuerSigningKey = new SymmetricSecurityKey(
                            Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
                        ValidateLifetime = true,
                        ClockSkew = TimeSpan.FromSeconds(30),
                        NameClaimType = JwtRegisteredClaimNames.Sub,
                        RoleClaimType = ClaimTypes.Role
                    };
                });

            builder.Services.AddAuthorization();

            builder.Services.AddScoped<IUserRepository, UserRepository>();
            builder.Services.AddScoped<IUserOrchestrator, UserOrchestrator>();

            builder.Services.AddScoped<IAuthRepository, AuthRepository>();
            builder.Services.AddScoped<IAuthOrchestrator, AuthOrchestrator>();
            builder.Services.AddSingleton<ITokenService, JwtTokenService>();

            builder.Services.AddControllers();
            builder.Services.AddOpenApi();

            builder.Services.AddSwaggerGen(o =>
            {
                o.AddSecurityDefinition(JwtBearerDefaults.AuthenticationScheme, new OpenApiSecurityScheme
                {
                    Name = "Authorization",
                    In = ParameterLocation.Header,
                    Type = SecuritySchemeType.Http,
                    Scheme = "bearer",
                    BearerFormat = "JWT",
                    Description = "Paste the raw JWT. Swagger adds the \"Bearer \" prefix for you."
                });

                o.AddSecurityRequirement(document => new OpenApiSecurityRequirement
                {
                    {
                        new OpenApiSecuritySchemeReference(JwtBearerDefaults.AuthenticationScheme, document, null),
                        new List<string>()
                    }
                }); 
            });

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
