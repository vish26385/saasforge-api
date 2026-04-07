namespace SaaSForge.Api.Modules.Leads.Constants;

public static class LeadStatuses
{
    public const string New = "New";
    public const string Contacted = "Contacted";
    public const string Qualified = "Qualified";
    public const string ProposalSent = "ProposalSent";
    public const string Won = "Won";
    public const string Lost = "Lost";
    public const string Nurturing = "Nurturing";

    public static readonly string[] All =
    [
        New,
        Contacted,
        Qualified,
        ProposalSent,
        Won,
        Lost,
        Nurturing
    ];
}