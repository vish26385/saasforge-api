namespace SaaSForge.Api.Modules.Leads.Dtos;

public class UpdateLeadMessageRequest
{
    public string Content { get; set; } = default!;
    public bool IsSent { get; set; }
    public string? AiTone { get; set; }
    public string? AiGoal { get; set; }
}