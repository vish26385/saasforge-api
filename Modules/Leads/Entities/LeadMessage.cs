namespace SaaSForge.Api.Modules.Leads.Entities
{
    public class LeadMessage
    {
        public Guid Id { get; set; }

        public int BusinessId { get; set; }

        public Guid LeadId { get; set; }
        public Lead Lead { get; set; } = default!;

        public string Direction { get; set; } = default!;
        public string Channel { get; set; } = default!;
        public string MessageType { get; set; } = "text";
        public string Content { get; set; } = default!;

        public bool IsAiGenerated { get; set; }
        public bool IsSent { get; set; }

        public string? AiTone { get; set; }
        public string? AiGoal { get; set; }
        public string? AiModel { get; set; }

        public DateTime CreatedAtUtc { get; set; }
    }
}
