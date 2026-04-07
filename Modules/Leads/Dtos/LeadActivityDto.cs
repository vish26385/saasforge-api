namespace SaaSForge.Api.Modules.Leads.Dtos;

public class LeadActivityDto
{
    public Guid Id { get; set; }
    public string ActivityType { get; set; } = default!;
    public string Title { get; set; } = default!;
    public string? Description { get; set; }
    public DateTime CreatedAtUtc { get; set; }
}