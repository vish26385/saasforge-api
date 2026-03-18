using System.ComponentModel.DataAnnotations;

namespace SaaSForge.Api.DTOs.Ai
{
    public class AskAiRequestDto
    {
        [Required]
        [MaxLength(100)]
        public string FeatureType { get; set; } = string.Empty;

        [Required]
        [MaxLength(4000)]
        public string Prompt { get; set; } = string.Empty;

        public string? SystemPrompt { get; set; }
        public string? InputContextJson { get; set; }
    }
}
