namespace SaaSForge.Api.Modules.Leads.Dtos;

public class LeadDetailsDto
{
    public Guid Id { get; set; }
    public string FullName { get; set; } = default!;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? CompanyName { get; set; }
    public string Source { get; set; } = default!;
    public string Status { get; set; } = default!;
    public string Priority { get; set; } = default!;
    public decimal? EstimatedValue { get; set; }
    public string? InquirySummary { get; set; }
    public string? LastIncomingMessagePreview { get; set; }
    public DateTime? LastContactAtUtc { get; set; }
    public DateTime? LastReplyAtUtc { get; set; }
    public DateTime? LastIncomingAtUtc { get; set; }
    public DateTime? NextFollowUpAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public List<LeadMessageDto> Messages { get; set; } = [];
    public List<LeadNoteDto> Notes { get; set; } = [];
    public List<LeadActivityDto> Activities { get; set; } = [];
    public List<LeadTagDto> Tags { get; set; } = [];
}