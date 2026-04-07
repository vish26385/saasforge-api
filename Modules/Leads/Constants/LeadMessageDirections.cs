namespace SaaSForge.Api.Modules.Leads.Constants;

public static class LeadMessageDirections
{
    public const string Incoming = "Incoming";
    public const string Outgoing = "Outgoing";

    public static readonly string[] All =
    [
        Incoming,
        Outgoing
    ];
}