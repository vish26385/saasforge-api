namespace SaaSForge.Api.Modules.Leads.Entities;

public sealed class LeadAlert
{
    public Guid Id { get; set; }

    public Guid LeadId { get; set; }
    public Lead Lead { get; set; } = default!;

    public int BusinessId { get; set; }

    public string Type { get; set; } = default!;
    public string Message { get; set; } = default!;
    public string Severity { get; set; } = "Medium";

    public bool IsResolved { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? ResolvedAtUtc { get; set; }

    public DateTime? AcknowledgedAtUtc { get; set; }
    public DateTime? SuppressedUntilUtc { get; set; }
}