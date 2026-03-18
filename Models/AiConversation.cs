namespace SaaSForge.Api.Models
{
    public class AiConversation
    {
        public long Id { get; set; }

        public int BusinessId { get; set; }
        public Business Business { get; set; } = null!;

        public string FeatureType { get; set; } = string.Empty;
        public string Prompt { get; set; } = string.Empty;
        public string? SystemPrompt { get; set; }
        public string? InputContextJson { get; set; }

        public string Response { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;

        public DateTime CreatedAtUtc { get; set; }
    }
}
