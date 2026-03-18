using SaaSForge.Api.Models.Auth;

namespace SaaSForge.Api.Models
{
    public class Business
    {
        public int Id { get; set; }

        public string OwnerUserId { get; set; } = string.Empty;
        public ApplicationUser OwnerUser { get; set; } = null!;

        public string Name { get; set; } = string.Empty;
        public string Slug { get; set; } = string.Empty;

        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? Address { get; set; }
        public string? TimeZone { get; set; }

        public DateTime CreatedAtUtc { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
    }
}
