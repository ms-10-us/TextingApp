using System.ComponentModel.DataAnnotations;

namespace UserMicroService.Dtos
{
    public sealed class LoginDto
    {
        /// <summary>
        /// Accepts either the email address or the username.
        /// </summary>
        [Required(AllowEmptyStrings = false)]
        [StringLength(256)]
        public string Identifier { get; set; } = string.Empty;

        /// <summary>
        /// Deliberately has no StringLength or complexity attributes. Validating the
        /// shape of a submitted password tells an attacker what the policy is and
        /// rejects legacy passwords that no longer meet current rules.
        /// </summary>
        [Required(AllowEmptyStrings = false)]
        public string Password { get; set; } = string.Empty;

    }
}
