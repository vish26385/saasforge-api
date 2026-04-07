namespace SaaSForge.Api.Modules.Leads.Dtos;

public class AddLeadMessageRequest
{
    public string Direction { get; set; } = default!;
    public string Channel { get; set; } = default!;
    public string Content { get; set; } = default!;
    public bool IsAiGenerated { get; set; }
    public bool IsSent { get; set; }
    public string? AiTone { get; set; }
    public string? AiGoal { get; set; }
}