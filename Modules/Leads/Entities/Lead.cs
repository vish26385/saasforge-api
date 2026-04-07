using SaaSForge.Api.Models;

namespace SaaSForge.Api.Modules.Leads.Entities
{
    public class Lead
    {
        public Guid Id { get; set; }

        public int BusinessId { get; set; }
        public Business Business { get; set; } = default!;

        public string FullName { get; set; } = default!;
        public string? Email { get; set; }
        public string? Phone { get; set; }
        public string? CompanyName { get; set; }

        public string Source { get; set; } = "manual";
        public string Status { get; set; } = "New";
        public string Priority { get; set; } = "Medium";

        public decimal? EstimatedValue { get; set; }

        public string? InquirySummary { get; set; }
        public string? LastIncomingMessagePreview { get; set; }

        public DateTime? LastContactAtUtc { get; set; }
        public DateTime? LastReplyAtUtc { get; set; }
        public DateTime? LastIncomingAtUtc { get; set; }
        public DateTime? NextFollowUpAtUtc { get; set; }

        public bool IsArchived { get; set; }

        public DateTime CreatedAtUtc { get; set; }
        public DateTime UpdatedAtUtc { get; set; }

        public ICollection<LeadMessage> Messages { get; set; } = new List<LeadMessage>();
        public ICollection<LeadNote> Notes { get; set; } = new List<LeadNote>();
        public ICollection<LeadTagMap> LeadTags { get; set; } = new List<LeadTagMap>();
        public ICollection<LeadActivity> Activities { get; set; } = new List<LeadActivity>();
        public ICollection<LeadAiSuggestion> AiSuggestions { get; set; } = new List<LeadAiSuggestion>();
        public ICollection<LeadAlert> Alerts { get; set; } = new List<LeadAlert>();
    }
}