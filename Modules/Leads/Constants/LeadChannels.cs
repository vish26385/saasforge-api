namespace SaaSForge.Api.Modules.Leads.Constants;

public static class LeadChannels
{
    public const string Manual = "Manual";
    public const string WhatsApp = "WhatsApp";
    public const string Instagram = "Instagram";
    public const string Website = "Website";
    public const string Facebook = "Facebook";
    public const string Email = "Email";
    public const string Other = "Other";

    public static readonly string[] All =
    [
        Manual,
        WhatsApp,
        Instagram,
        Website,
        Facebook,
        Email,
        Other
    ];
}