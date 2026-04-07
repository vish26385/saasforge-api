namespace SaaSForge.Api.Modules.Leads.Dtos;

public class CreateLeadTagRequest
{
    public string Name { get; set; } = default!;
    public string? Color { get; set; }
}