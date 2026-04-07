using SaaSForge.Api.Modules.Leads.Dtos;

namespace SaaSForge.Api.Modules.Leads.Services
{
    public interface ILeadAiPromptBuilder
    {
        string Build(LeadAiContext ctx, string responseType);
    }

    public class LeadAiPromptBuilder : ILeadAiPromptBuilder
    {
        public string Build(LeadAiContext ctx, string responseType)
        {
            return $@"
You are LeadFlow AI, an expert sales follow-up assistant.

Generate a short, natural, high-converting message.

SCENARIO: {responseType}

LEAD CONTEXT:
- Name: {ctx.LeadName}
- Source: {ctx.Source}
- Status: {ctx.Status}
- LastContact: {ctx.LastContactAtUtc}
- LastIncoming: {ctx.LastIncomingAtUtc}
- NextFollowUp: {ctx.NextFollowUpAtUtc}
- Alerts: {string.Join(", ", ctx.Alerts)}
- Notes: {ctx.Notes}

RULES:
- Human-like
- Short (max 80 words)
- No fluff
- No fake promises
- One clear CTA

OUTPUT JSON:
{{
  ""responseType"": ""..."",
  ""message"": ""..."",
  ""cta"": ""..."",
  ""tone"": ""..."",
  ""whyThisWorks"": ""..."",
  ""confidence"": ""high|medium|low""
}}
";
        }
    }
}
