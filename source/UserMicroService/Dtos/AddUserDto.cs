using System.ComponentModel.DataAnnotations;

namespace UserMicroService.Dtos
{
    public sealed class AddUserDto
    {
        [Required(AllowEmptyStrings = false)]
        [EmailAddress]
        [StringLength(256)]
        public string Email { get; set; } = string.Empty;

        [Required(AllowEmptyStrings = false)]
        [StringLength(64, MinimumLength = 6)]
        [RegularExpression(
            "^[a-zA-Z0-9-._@+]+$",
            ErrorMessage = "Username may only contain letters, digits and the characters - . _ @ +"
            )]
        public string Username { get; set; } = string.Empty;

        [Required(AllowEmptyStrings = false)]
        [StringLength(64, MinimumLength = 6)]
        public string DisplayName { get; set;  } = string.Empty;

        [Required(AllowEmptyStrings = false)]
        [StringLength(128, MinimumLength = 12)]
        public string Password { get; set; } = string.Empty;
    }
}
