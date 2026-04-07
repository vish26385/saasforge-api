namespace SaaSForge.Api.Modules.Leads.Dtos;

public sealed class LeadFollowUpItemDto
{
    public Guid LeadId { get; set; }
    public string LeadName { get; set; } = string.Empty;
    public string? CustomerName { get; set; }
    public string? CustomerEmail { get; set; }
    public string? CustomerPhone { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public DateTime? LastActivityAtUtc { get; set; }
    public DateTime NextFollowUpAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}

public sealed class LeadFollowUpBucketDto
{
    public int Count { get; set; }
    public List<LeadFollowUpItemDto> Items { get; set; } = new();
}

public sealed class LeadFollowUpIntelligenceDto
{
    public DateTime GeneratedAtUtc { get; set; }
    public DateTime UtcTodayStart { get; set; }
    public DateTime UtcTomorrowStart { get; set; }

    public LeadFollowUpBucketDto Overdue { get; set; } = new();
    public LeadFollowUpBucketDto DueToday { get; set; } = new();
    public LeadFollowUpBucketDto Upcoming { get; set; } = new();
}