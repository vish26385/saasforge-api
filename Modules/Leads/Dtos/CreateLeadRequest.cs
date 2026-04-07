namespace SaaSForge.Api.Modules.Leads.Dtos;

public class CreateLeadRequest
{
    public string FullName { get; set; } = default!;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? CompanyName { get; set; }
    public string Source { get; set; } = "manual";
    public string Priority { get; set; } = "Medium";
    public decimal? EstimatedValue { get; set; }
    public string? InquirySummary { get; set; }
    public string? InitialMessage { get; set; }
    public DateTime? NextFollowUpAtUtc { get; set; }
    public List<Guid>? TagIds { get; set; }
}