namespace SaaSForge.Api.DTOs.Ai
{
    public class AskAiResponseDto
    {
        public long Id { get; set; }
        public int BusinessId { get; set; }
        public string FeatureType { get; set; } = string.Empty;
        public string Prompt { get; set; } = string.Empty;
        public string? SystemPrompt { get; set; }
        public string? InputContextJson { get; set; }
        public string Response { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; }
    }
}
