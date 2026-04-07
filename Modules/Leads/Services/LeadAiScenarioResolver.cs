using SaaSForge.Api.Modules.Leads.Constants;
using SaaSForge.Api.Modules.Leads.Dtos;

namespace SaaSForge.Api.Modules.Leads.Services
{
    public interface ILeadAiScenarioResolver
    {
        string Resolve(LeadAiContext context);
    }

    public class LeadAiScenarioResolver : ILeadAiScenarioResolver
    {
        public string Resolve(LeadAiContext ctx)
        {
            var now = DateTime.UtcNow;

            // Overdue follow-up → Follow-up
            if (ctx.NextFollowUpAtUtc.HasValue && ctx.NextFollowUpAtUtc < now)
                return LeadAiResponseType.FollowUp;

            // No reply for long time → Re-engagement
            if (ctx.LastIncomingAtUtc == null || ctx.LastIncomingAtUtc < now.AddDays(-3))
                return LeadAiResponseType.ReEngagement;

            // Qualified → Closing
            if (ctx.Status?.ToLower() == "qualified")
                return LeadAiResponseType.Closing;

            // Default → Reminder
            return LeadAiResponseType.Reminder;
        }
    }
}
