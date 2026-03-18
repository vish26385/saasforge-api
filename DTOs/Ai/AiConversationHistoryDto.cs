namespace SaaSForge.Api.DTOs.Ai
{
    public class AiConversationHistoryDto
    {
        public long Id { get; set; }
        public string FeatureType { get; set; } = string.Empty;
        public string Prompt { get; set; } = string.Empty;
        public string Response { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public DateTime CreatedAtUtc { get; set; }
    }
}
