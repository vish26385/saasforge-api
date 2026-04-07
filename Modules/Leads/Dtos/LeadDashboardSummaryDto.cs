namespace SaaSForge.Api.Modules.Leads.Dtos;

public class LeadDashboardSummaryDto
{
    public int TotalLeads { get; set; }
    public int NewLeads { get; set; }
    public int QualifiedLeads { get; set; }
    public int WonLeads { get; set; }
    public int LostLeads { get; set; }
    public int FollowUpsDue { get; set; }
}