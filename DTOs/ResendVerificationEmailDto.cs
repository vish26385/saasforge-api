using System.ComponentModel.DataAnnotations;

namespace SaaSForge.Api.DTOs
{
    public class ResendVerificationEmailDto
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;
    }
}