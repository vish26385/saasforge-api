namespace SaaSForge.Api.Modules.Leads.Entities
{
    public class LeadAiSuggestion
    {
        public Guid Id { get; set; }

        public int BusinessId { get; set; }

        public Guid LeadId { get; set; }
        public Lead Lead { get; set; } = default!;

        public string SuggestionType { get; set; } = default!;
        public string InputContext { get; set; } = default!;
        public string OutputText { get; set; } = default!;

        public string? Tone { get; set; }
        public string? Goal { get; set; }
        public string? Model { get; set; }

        public bool IsUsed { get; set; }

        public DateTime CreatedAtUtc { get; set; }
    }
}
