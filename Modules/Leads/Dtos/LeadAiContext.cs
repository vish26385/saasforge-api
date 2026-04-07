namespace SaaSForge.Api.Modules.Leads.Dtos
{
    public sealed class LeadAiContext
    {
        public Guid LeadId { get; set; }
        public string LeadName { get; set; } = "";
        public string? Source { get; set; }
        public string Status { get; set; } = "";
        public DateTime? LastContactAtUtc { get; set; }
        public DateTime? LastIncomingAtUtc { get; set; }
        public DateTime? NextFollowUpAtUtc { get; set; }
        public string? Notes { get; set; }
        public List<string> Alerts { get; set; } = new();
    }
}
