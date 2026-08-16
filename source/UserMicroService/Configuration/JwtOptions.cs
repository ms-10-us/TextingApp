using System.ComponentModel.DataAnnotations;

namespace UserMicroService.Configuration
{
    public sealed class JwtOptions
    {
        public const string SectionName = "Jwt";

        [Required(AllowEmptyStrings = false)]
        public string Issuer { get; set; } = string.Empty;

        [Required(AllowEmptyStrings = false)]
        public string Audience { get; set; } = string.Empty;

        [Required(AllowEmptyStrings = false)]
        [MinLength(32)]
        public string SigningKey {  get; set; } = string.Empty;

        [Range(1, 1440)]
        public int AccessTokenMinutes { get; set; } = 120;
    }
}
