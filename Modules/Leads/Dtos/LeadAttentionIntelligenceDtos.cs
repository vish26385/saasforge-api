namespace SaaSForge.Api.Modules.Leads.Dtos;

public sealed class LeadAttentionItemDto
{
    public Guid LeadId { get; set; }

    public string FullName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public string? CompanyName { get; set; }

    public string Source { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;

    public decimal? EstimatedValue { get; set; }
    public string? InquirySummary { get; set; }
    public string? LastIncomingMessagePreview { get; set; }

    public DateTime? LastContactAtUtc { get; set; }
    public DateTime? LastReplyAtUtc { get; set; }
    public DateTime? LastIncomingAtUtc { get; set; }
    public DateTime? NextFollowUpAtUtc { get; set; }

    public DateTime CreatedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }

    public string Reason { get; set; } = string.Empty;
    public int AgeHours { get; set; }
}

public sealed class LeadAttentionBucketDto
{
    public int Count { get; set; }
    public List<LeadAttentionItemDto> Items { get; set; } = new();
}

public sealed class LeadAttentionIntelligenceDto
{
    public DateTime GeneratedAtUtc { get; set; }

    public int StaleLeadThresholdHours { get; set; }
    public int AwaitingReplyThresholdHours { get; set; }
    public int NoFollowUpThresholdHours { get; set; }

    public LeadAttentionBucketDto StaleLeads { get; set; } = new();
    public LeadAttentionBucketDto AwaitingReply { get; set; } = new();
    public LeadAttentionBucketDto NoFollowUpScheduled { get; set; } = new();
    public LeadAttentionBucketDto HighPriorityNeedsAction { get; set; } = new();
}