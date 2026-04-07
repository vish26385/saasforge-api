namespace SaaSForge.Api.Modules.Leads.Dtos;

public class LeadNoteDto
{
    public Guid Id { get; set; }
    public string Note { get; set; } = default!;
    public DateTime CreatedAtUtc { get; set; }
}