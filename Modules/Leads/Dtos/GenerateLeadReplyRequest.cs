namespace SaaSForge.Api.Modules.Leads.Dtos;

public class GenerateLeadReplyRequest
{
    public Guid LeadId { get; set; }
    public string Goal { get; set; } = "convert";
    public string Tone { get; set; } = "professional";
    public string? CustomInstruction { get; set; }
}