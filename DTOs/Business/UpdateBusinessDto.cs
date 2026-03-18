using System.ComponentModel.DataAnnotations;

namespace SaaSForge.Api.DTOs.Business
{
    public class UpdateBusinessDto
    {
        [Required]
        [MaxLength(150)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string Slug { get; set; } = string.Empty;

        [EmailAddress]
        [MaxLength(200)]
        public string? Email { get; set; }

        [MaxLength(30)]
        public string? Phone { get; set; }

        [MaxLength(300)]
        public string? Address { get; set; }

        [MaxLength(100)]
        public string? TimeZone { get; set; }
    }
}
