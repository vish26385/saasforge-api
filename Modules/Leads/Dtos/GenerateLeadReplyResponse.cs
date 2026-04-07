namespace SaaSForge.Api.Modules.Leads.Dtos;

public class GenerateLeadReplyResponse
{
    public Guid LeadId { get; set; }
    public DateTime GeneratedAtUtc { get; set; }
    public string ResponseType { get; set; } = default!;
    public string Reply { get; set; } = default!;
    public string ReasoningSummary { get; set; } = default!;
    public string SuggestedNextStep { get; set; } = default!;
    public string Confidence { get; set; } = default!;
    public string ToneUsed { get; set; } = default!;
    public int RecommendedIndex { get; set; } = 0;
    public List<string> MissingInformation { get; set; } = [];
    public List<LeadReplyVariationDto> Variations { get; set; } = [];
}

public class LeadReplyVariationDto
{
    public string Label { get; set; } = default!;
    public string Reply { get; set; } = default!;
}