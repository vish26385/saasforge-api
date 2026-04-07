namespace SaaSForge.Api.Modules.Leads.Models;

public class LeadListQuery
{
    public string? Search { get; set; }
    public string? Status { get; set; }
    public string? Priority { get; set; }
    public string? Source { get; set; }
    public bool FollowUpDueOnly { get; set; }

    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}