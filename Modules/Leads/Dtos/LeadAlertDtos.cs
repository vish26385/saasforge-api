namespace SaaSForge.Api.Modules.Leads.Dtos;

public sealed class LeadAlertSignalDto
{
    public Guid AlertId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
}

public sealed class LeadAlertGroupItemDto
{
    public Guid LeadId { get; set; }

    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? CompanyName { get; set; }

    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string Source { get; set; } = string.Empty;

    public string PrimaryType { get; set; } = string.Empty;
    public string PrimaryMessage { get; set; } = string.Empty;
    public string PrimarySeverity { get; set; } = string.Empty;
    public DateTime PrimaryCreatedAtUtc { get; set; }

    public int TotalSignals { get; set; }
    public int AdditionalSignalsCount { get; set; }

    public List<LeadAlertSignalDto> Signals { get; set; } = new();
}

public sealed class LeadAlertsResponseDto
{
    public DateTime GeneratedAtUtc { get; set; }
    public int Count { get; set; }
    public int HighSeverityCount { get; set; }
    public List<LeadAlertGroupItemDto> Items { get; set; } = new();
}