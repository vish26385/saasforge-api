namespace SaaSForge.Api.Modules.Leads.Dtos
{
    public class UpdateLeadRequest
    {
        public string FullName { get; set; } = default!;
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? CompanyName { get; set; }
        public string Source { get; set; } = default!;
        public string Priority { get; set; } = default!;
        public decimal? EstimatedValue { get; set; }
        public string? InquirySummary { get; set; }
        public DateTime? NextFollowUpAtUtc { get; set; }
        public bool IsArchived { get; set; }
    }
}
