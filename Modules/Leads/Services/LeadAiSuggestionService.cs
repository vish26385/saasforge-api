using SaaSForge.Api.Data;
using SaaSForge.Api.Modules.Leads.Dtos;
using System.Text.Json;

namespace SaaSForge.Api.Modules.Leads.Services
{
    public interface ILeadAiSuggestionService
    {
        Task<LeadAiSuggestionDto> GenerateAsync(Guid leadId);
    }

    public class LeadAiSuggestionService : ILeadAiSuggestionService
    {
        private readonly AppDbContext _db;
        private readonly ILeadAiScenarioResolver _resolver;
        private readonly ILeadAiPromptBuilder _promptBuilder;
        private readonly IOpenAiService _ai;

        public LeadAiSuggestionService(
            AppDbContext db,
            ILeadAiScenarioResolver resolver,
            ILeadAiPromptBuilder promptBuilder,
            IOpenAiService ai)
        {
            _db = db;
            _resolver = resolver;
            _promptBuilder = promptBuilder;
            _ai = ai;
        }

        public async Task<LeadAiSuggestionDto> GenerateAsync(Guid leadId)
        {
            var lead = await _db.Leads.FindAsync(leadId);

            if (lead == null)
                throw new Exception("Lead not found");

            var ctx = new LeadAiContext
            {
                LeadId = lead.Id,
                LeadName = lead.FullName,
                Source = lead.Source,
                Status = lead.Status,
                LastContactAtUtc = lead.LastContactAtUtc,
                LastIncomingAtUtc = lead.LastIncomingAtUtc,
                NextFollowUpAtUtc = lead.NextFollowUpAtUtc,
                Notes = lead.Notes != null && lead.Notes.Any()
                        ? string.Join(" | ", lead.Notes.Select(x => x.Note))
                        : null,
                                Alerts = new List<string>()
            };

            var type = _resolver.Resolve(ctx);

            var prompt = _promptBuilder.Build(ctx, type);

            var aiResponse = await _ai.GenerateAsync(prompt);

            // Deserialize JSON
            var result = JsonSerializer.Deserialize<LeadAiSuggestionDto>(aiResponse,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            return result ?? new LeadAiSuggestionDto
            {
                ResponseType = type,
                Message = "Could not generate response.",
                Confidence = "low"
            };
        }
    }
}
