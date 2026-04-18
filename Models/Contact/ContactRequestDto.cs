using System.ComponentModel.DataAnnotations;

namespace SaaSForge.Api.Models.Contact
{
    public class ContactRequestDto
    {
        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        [StringLength(200)]
        public string Email { get; set; } = string.Empty;

        [Required]
        [StringLength(200)]
        public string Subject { get; set; } = string.Empty;

        [Required]
        [StringLength(5000)]
        public string Message { get; set; } = string.Empty;
    }
}
