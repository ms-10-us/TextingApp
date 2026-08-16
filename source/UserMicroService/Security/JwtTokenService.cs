using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using System.Security.Claims;
using System.Text;
using UserMicroService.Configuration;
using UserMicroService.Models;

namespace UserMicroService.Security
{
    /// <inheritdoc cref="ITokenService"/>
    public sealed class JwtTokenService : ITokenService
    {
        private readonly JwtOptions _options;
        private readonly SigningCredentials _signingCreadentials;
        private readonly JsonWebTokenHandler _handler = new JsonWebTokenHandler();

        public JwtTokenService(IOptions<JwtOptions> options)
        {
            _options = options.Value;

            // Built once: SymmetricSecurityKey is thread-safe and rebuilding it per
            // request wastes cycles on every login.
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SigningKey));
            _signingCreadentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        }

        public AccessTokenModel CreateAccessToken(UserModel user, IReadOnlyCollection<string> roles)
        {
            ArgumentNullException.ThrowIfNull(user);

            DateTime issuesAt = DateTime.UtcNow;
            DateTime expiresAt = issuesAt.AddMinutes(_options.AccessTokenMinutes);

            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(JwtRegisteredClaimNames.PreferredUsername, user.UserName),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N")),
                new Claim("display_name", user.DisplayName)
            };

            foreach (string role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            var descriptor = new SecurityTokenDescriptor
            {
                Issuer = _options.Issuer,
                Audience = _options.Audience,
                Subject = new ClaimsIdentity(claims),
                IssuedAt = issuesAt,
                NotBefore = issuesAt,
                Expires = expiresAt,
                SigningCredentials = _signingCreadentials
            };

            // JsonWebTokenHandler does not apply the legacy outbound claim-type map,
            // so "sub" is written as "sub" rather than the long WS-Federation URI.
            // This matches NameClaimType = JwtRegisteredClaimNames.Sub in Program.cs.
            string token = _handler.CreateToken(descriptor);

            return new AccessTokenModel
            {
                Token = token, 
                ExpiresAtUtc = expiresAt
            };

        }

    }
}
