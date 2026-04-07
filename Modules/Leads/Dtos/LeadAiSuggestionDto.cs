namespace SaaSForge.Api.Modules.Leads.Dtos
{
    public sealed class LeadAiSuggestionDto
    {
        public string ResponseType { get; set; } = "";
        public string Message { get; set; } = "";
        public string Cta { get; set; } = "";
        public string Tone { get; set; } = "";
        public string WhyThisWorks { get; set; } = "";
        public string Confidence { get; set; } = "";
    }
}
