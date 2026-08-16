using System.ComponentModel.DataAnnotations;

namespace UserMicroService.Dtos
{
    public sealed class AddUserDtoResponse
    {
        public Guid Id { get; set; }

        public string Email { get; set; } = string.Empty;

        public string UserName { get; set; } = string.Empty;

        public DateTime? LastSeen { get; set; }
    }
}
