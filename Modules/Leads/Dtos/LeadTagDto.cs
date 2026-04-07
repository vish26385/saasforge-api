namespace SaaSForge.Api.Modules.Leads.Dtos;

public class LeadTagDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = default!;
    public string? Color { get; set; }
}