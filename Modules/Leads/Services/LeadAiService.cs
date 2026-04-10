using Microsoft.EntityFrameworkCore;
using SaaSForge.Api.Data;
using SaaSForge.Api.DTOs.Ai;
using SaaSForge.Api.Modules.Leads.Constants;
using SaaSForge.Api.Modules.Leads.Dtos;
using SaaSForge.Api.Modules.Leads.Entities;
using SaaSForge.Api.Modules.Leads.Interfaces;
using SaaSForge.Api.Services.Ai;
using SaaSForge.Api.Services.Usage;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace SaaSForge.Api.Modules.Leads.Services;

public class LeadAiService : ILeadAiService
{
    private readonly AppDbContext _context;
    private readonly IAiService _aiService;
    private readonly IUsageService _usageService;
    private readonly ILeadActivityService _leadActivityService;

    public LeadAiService(
        AppDbContext context,
        IAiService aiService,
        IUsageService usageService,
        ILeadActivityService leadActivityService)
    {
        _context = context;
        _aiService = aiService;
        _usageService = usageService;
        _leadActivityService = leadActivityService;
    }

    public async Task<GenerateLeadReplyResponse> GenerateReplyAsync(
        int businessId,
        string userId,
        GenerateLeadReplyRequest request)
    {
        if (request.LeadId == Guid.Empty)
            throw new InvalidOperationException("LeadId is required.");

        if (string.IsNullOrWhiteSpace(userId))
            throw new InvalidOperationException("UserId is required.");

        var goal = NormalizeGoal(request.Goal);
        var tone = NormalizeTone(request.Tone);

        var lead = await _context.Leads
            .AsNoTracking()
            .FirstOrDefaultAsync(x =>
                x.BusinessId == businessId &&
                x.Id == request.LeadId &&
                !x.IsArchived);

        if (lead is null)
            throw new InvalidOperationException("Lead not found.");

        var recentMessages = await _context.LeadMessages
            .AsNoTracking()
            .Where(x => x.BusinessId == businessId && x.LeadId == request.LeadId)
            .OrderByDescending(x => x.CreatedAtUtc)
            .Take(8)
            .OrderBy(x => x.CreatedAtUtc)
            .ToListAsync();

        var business = await _context.Businesses
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == businessId);

        if (business is null)
            throw new InvalidOperationException("Business not found.");

        await _usageService.EnsureCanUseAiAsync(businessId);

        var prompt = BuildLeadReplyPrompt(
            businessName: GetBusinessName(business),
            lead: lead,
            recentMessages: recentMessages,
            goal: goal,
            tone: tone,
            customInstruction: request.CustomInstruction);

        var aiRequest = BuildAskAiRequest(prompt, tone, goal);

        var aiResponse = await _aiService.AskAsync(userId, aiRequest);
        var rawAiText = ExtractAiText(aiResponse);

        var parsed = ParseAiResponse(rawAiText);

        var suggestion = new LeadAiSuggestion
        {
            Id = Guid.NewGuid(),
            BusinessId = businessId,
            LeadId = lead.Id,
            SuggestionType = MapGoalToSuggestionType(goal),
            InputContext = prompt,
            OutputText = parsed.Reply,
            Tone = tone,
            Goal = goal,
            Model = TryReadStringProperty(aiResponse, "Model"),
            IsUsed = false,
            CreatedAtUtc = DateTime.UtcNow
        };

        _context.LeadAiSuggestions.Add(suggestion);
        await _context.SaveChangesAsync();

        await _leadActivityService.AddAsync(
            businessId,
            lead.Id,
            LeadActivityTypes.ReplyGenerated,
            "AI reply generated",
            $"AI generated a {goal} reply in {tone} tone.");

        parsed.LeadId = lead.Id;

        return parsed;
    }

    private static AskAiRequestDto BuildAskAiRequest(string prompt, string tone, string goal)
    {
        var dto = new AskAiRequestDto();

        SetIfExists(dto, "Message", prompt);
        SetIfExists(dto, "Prompt", prompt);
        SetIfExists(dto, "Input", prompt);
        SetIfExists(dto, "UserMessage", prompt);
        SetIfExists(dto, "Question", prompt);

        SetIfExists(dto, "Tone", tone);
        SetIfExists(dto, "Goal", goal);
        SetIfExists(dto, "Focus", goal);
        SetIfExists(dto, "Category", "lead-conversion");
        SetIfExists(dto, "Platform", "leadflow-ai");
        SetIfExists(dto, "Context", "lead-conversion");
        SetIfExists(dto, "UseCase", "lead-conversion");

        return dto;
    }

    //private static string BuildLeadReplyPrompt(
    //string businessName,
    //Lead lead,
    //List<LeadMessage> recentMessages,
    //string goal,
    //string tone,
    //string? customInstruction)
    //{
    //    var sb = new StringBuilder();

    //    sb.AppendLine("You are LeadFlow AI, a world-class AI sales assistant designed to convert leads into paying customers.");
    //    sb.AppendLine();
    //    sb.AppendLine("Your job is NOT just to reply, but to strategically move the lead forward based on their current intent.");
    //    sb.AppendLine();

    //    // CORE OBJECTIVE
    //    sb.AppendLine("CORE OBJECTIVE:");
    //    sb.AppendLine("- Maximize probability of conversion");
    //    sb.AppendLine("- Increase reply rate");
    //    sb.AppendLine("- Reduce friction and hesitation");
    //    sb.AppendLine("- Guide the lead toward the RIGHT next step (not always a call)");
    //    sb.AppendLine();

    //    // HUMAN RULES
    //    sb.AppendLine("HUMAN-LIKE WRITING RULES:");
    //    sb.AppendLine("- Sound like a real human (not AI)");
    //    sb.AppendLine("- Keep it short, clear, and natural");
    //    sb.AppendLine("- Avoid robotic, generic, or salesy tone");
    //    sb.AppendLine("- Avoid hype or exaggerated claims");
    //    sb.AppendLine("- Avoid repeating lead context");
    //    sb.AppendLine("- Avoid long paragraphs");
    //    sb.AppendLine("- No bullet points in reply");
    //    sb.AppendLine("- No emojis unless absolutely natural");
    //    sb.AppendLine("- Message must feel ready-to-send instantly");
    //    sb.AppendLine();

    //    // SALES INTELLIGENCE
    //    sb.AppendLine("CRITICAL SALES INTELLIGENCE RULES:");
    //    sb.AppendLine("- DO NOT always push for a call");
    //    sb.AppendLine("- Choose response strategy based on lead intent:");
    //    sb.AppendLine("  • Cold → Soft");
    //    sb.AppendLine("  • Price concern → Balanced");
    //    sb.AppendLine("  • Interested → Direct");
    //    sb.AppendLine("  • Confused → Balanced");
    //    sb.AppendLine("  • Competitor → Soft");
    //    sb.AppendLine("  • Close → Direct");
    //    sb.AppendLine("  • Not ready → Soft");
    //    sb.AppendLine();

    //    sb.AppendLine("- ALWAYS answer the lead first before CTA");
    //    sb.AppendLine("- CTA must feel natural, not forced");
    //    sb.AppendLine();

    //    // OUTPUT FORMAT
    //    sb.AppendLine("STRICT OUTPUT RULES:");
    //    sb.AppendLine("- Return ONLY valid JSON");
    //    sb.AppendLine("- No markdown");
    //    sb.AppendLine("- No explanation outside JSON");
    //    sb.AppendLine();

    //    sb.AppendLine();
    //    sb.AppendLine("Return EXACT JSON:");
    //    sb.AppendLine("{");
    //    sb.AppendLine("  \"responseType\": \"...\",");
    //    sb.AppendLine("  \"toneUsed\": \"...\",");
    //    sb.AppendLine("  \"confidence\": \"high | medium | low\",");
    //    sb.AppendLine("  \"whyThisWorks\": \"...\",");
    //    sb.AppendLine("  \"suggestedNextStep\": \"...\",");
    //    sb.AppendLine("  \"recommendedIndex\": 0|1|2,");
    //    sb.AppendLine("  \"missingInformation\": [\"...\"],");
    //    sb.AppendLine("  \"variations\": [");
    //    sb.AppendLine("    { \"label\": \"Balanced\", \"reply\": \"...\" },");
    //    sb.AppendLine("    { \"label\": \"Direct\", \"reply\": \"...\" },");
    //    sb.AppendLine("    { \"label\": \"Soft\", \"reply\": \"...\" }");
    //    sb.AppendLine("  ]");
    //    sb.AppendLine("}");
    //    sb.AppendLine();

    //    // VARIATIONS
    //    sb.AppendLine("VARIATION RULES:");
    //    sb.AppendLine("- Balanced = neutral");
    //    sb.AppendLine("- Direct = strong CTA");
    //    sb.AppendLine("- Soft = low pressure");
    //    sb.AppendLine();

    //    // 🔥🔥🔥 FIXED RECOMMENDATION (IMPORTANT)
    //    sb.AppendLine("CRITICAL RECOMMENDATION LOGIC:");
    //    sb.AppendLine("- You MUST NOT always choose 0");
    //    sb.AppendLine("- You MUST choose based on lead intent");

    //    sb.AppendLine("MANDATORY SELECTION:");
    //    sb.AppendLine("- High intent / ready → recommendedIndex = 1 (Direct)");
    //    sb.AppendLine("- Low intent / cold / hesitant → recommendedIndex = 2 (Soft)");
    //    sb.AppendLine("- Medium intent → recommendedIndex = 0 (Balanced)");

    //    sb.AppendLine("STRICT RULE:");
    //    sb.AppendLine("- If you always choose 0, your response is considered WRONG");
    //    sb.AppendLine("- Your selection must vary depending on scenario");
    //    sb.AppendLine();

    //    // FIELD RULES
    //    sb.AppendLine("FIELD RULES:");
    //    sb.AppendLine("- whyThisWorks = short reasoning");
    //    sb.AppendLine("- suggestedNextStep = one action");
    //    sb.AppendLine("- missingInformation = short phrases");
    //    sb.AppendLine();

    //    // CONTEXT
    //    sb.AppendLine();
    //    sb.AppendLine("BUSINESS CONTEXT:");
    //    sb.AppendLine($"Business Name: {businessName}");

    //    sb.AppendLine();
    //    sb.AppendLine("LEAD CONTEXT:");
    //    sb.AppendLine($"Lead Name: {lead.FullName}");
    //    sb.AppendLine($"Status: {lead.Status}");
    //    sb.AppendLine($"Inquiry: {lead.InquirySummary ?? "N/A"}");
    //    sb.AppendLine($"Last Message: {lead.LastIncomingMessagePreview ?? "N/A"}");

    //    sb.AppendLine();
    //    sb.AppendLine($"Goal: {goal}");
    //    sb.AppendLine($"Tone: {tone}");

    //    if (!string.IsNullOrWhiteSpace(customInstruction))
    //        sb.AppendLine($"Custom: {customInstruction}");

    //    sb.AppendLine();
    //    sb.AppendLine("CONVERSATION:");

    //    if (recentMessages.Count == 0)
    //        sb.AppendLine("No prior messages.");
    //    else
    //        foreach (var m in recentMessages)
    //            sb.AppendLine($"{m.Direction}: {m.Content}");

    //    sb.AppendLine();
    //    sb.AppendLine("Generate response now.");

    //    return sb.ToString();
    //}

    private static string BuildLeadReplyPrompt(
    string businessName,
    Lead lead,
    List<LeadMessage> recentMessages,
    string goal,
    string tone,
    string? customInstruction)
    {
        var sb = new StringBuilder();

        sb.AppendLine("You are LeadFlow AI, a world-class AI sales assistant designed to help businesses convert leads into paying customers.");
        sb.AppendLine();
        sb.AppendLine("Your job is to generate high-quality reply suggestions that help the business owner or team respond manually.");
        sb.AppendLine("IMPORTANT: LeadFlow AI suggests replies only. It does NOT automatically send messages, auto-reply, or claim that the system is already automating communication.");
        sb.AppendLine();

        // CORE OBJECTIVE
        sb.AppendLine("CORE OBJECTIVE:");
        sb.AppendLine("- Maximize probability of conversion");
        sb.AppendLine("- Increase reply rate");
        sb.AppendLine("- Reduce friction and hesitation");
        sb.AppendLine("- Guide the lead toward the right next step");
        sb.AppendLine("- Help the user respond faster with a message that feels natural and ready to send");
        sb.AppendLine();

        // HUMAN RULES
        sb.AppendLine("HUMAN-LIKE WRITING RULES:");
        sb.AppendLine("- Sound like a real human, not AI");
        sb.AppendLine("- Keep it short, clear, and natural");
        sb.AppendLine("- Avoid robotic, generic, or salesy tone");
        sb.AppendLine("- Avoid hype or exaggerated claims");
        sb.AppendLine("- Avoid repeating lead context unnecessarily");
        sb.AppendLine("- Avoid long paragraphs");
        sb.AppendLine("- No bullet points in the reply");
        sb.AppendLine("- No emojis unless absolutely natural");
        sb.AppendLine("- Message must feel ready to send instantly by the user");
        sb.AppendLine();

        // PRODUCT TRUTH / SAFETY RULES
        sb.AppendLine("PRODUCT TRUTH RULES (VERY IMPORTANT):");
        sb.AppendLine("- Do NOT say or imply that the system automatically replies to customers");
        sb.AppendLine("- Do NOT say or imply that replies are automatically sent");
        sb.AppendLine("- Do NOT describe features that are not explicitly present in the provided context");
        sb.AppendLine("- Do NOT mention automation, routing, workflows, AI agents, or integrations unless the lead explicitly asked and the provided context confirms it");
        sb.AppendLine("- If the lead asks how the product works, describe it as helping the business respond faster, organize leads, and manage follow-ups");
        sb.AppendLine("- Keep product descriptions truthful, grounded, and aligned with the provided context only");
        sb.AppendLine();

        // SALES INTELLIGENCE
        sb.AppendLine("CRITICAL SALES INTELLIGENCE RULES:");
        sb.AppendLine("- Do NOT always push for a call");
        sb.AppendLine("- Choose response strategy based on lead intent:");
        sb.AppendLine("  • Cold -> Soft");
        sb.AppendLine("  • Price concern -> Balanced");
        sb.AppendLine("  • Interested -> Direct");
        sb.AppendLine("  • Confused -> Balanced");
        sb.AppendLine("  • Competitor -> Soft");
        sb.AppendLine("  • Close -> Direct");
        sb.AppendLine("  • Not ready -> Soft");
        sb.AppendLine("- Always answer the lead first before CTA");
        sb.AppendLine("- CTA must feel natural, not forced");
        sb.AppendLine();

        // OUTPUT FORMAT
        sb.AppendLine("STRICT OUTPUT RULES:");
        sb.AppendLine("- Return only valid JSON");
        sb.AppendLine("- No markdown");
        sb.AppendLine("- No explanation outside JSON");
        sb.AppendLine();

        sb.AppendLine();
        sb.AppendLine("Return EXACT JSON:");
        sb.AppendLine("{");
        sb.AppendLine("  \"responseType\": \"...\",");
        sb.AppendLine("  \"toneUsed\": \"...\",");
        sb.AppendLine("  \"confidence\": \"high | medium | low\",");
        sb.AppendLine("  \"whyThisWorks\": \"...\",");
        sb.AppendLine("  \"suggestedNextStep\": \"...\",");
        sb.AppendLine("  \"recommendedIndex\": 0|1|2,");
        sb.AppendLine("  \"missingInformation\": [\"...\"],");
        sb.AppendLine("  \"variations\": [");
        sb.AppendLine("    { \"label\": \"Balanced\", \"reply\": \"...\" },");
        sb.AppendLine("    { \"label\": \"Direct\", \"reply\": \"...\" },");
        sb.AppendLine("    { \"label\": \"Soft\", \"reply\": \"...\" }");
        sb.AppendLine("  ]");
        sb.AppendLine("}");
        sb.AppendLine();

        // VARIATIONS
        sb.AppendLine("VARIATION RULES:");
        sb.AppendLine("- Balanced = neutral, helpful, moderate CTA");
        sb.AppendLine("- Direct = stronger CTA, used when intent is high");
        sb.AppendLine("- Soft = lower pressure, used when lead is hesitant or early stage");
        sb.AppendLine();

        // RECOMMENDATION LOGIC
        sb.AppendLine("CRITICAL RECOMMENDATION LOGIC:");
        sb.AppendLine("- You must not always choose 0");
        sb.AppendLine("- You must choose recommendedIndex based on lead intent");
        sb.AppendLine("- High intent / ready -> recommendedIndex = 1 (Direct)");
        sb.AppendLine("- Low intent / cold / hesitant -> recommendedIndex = 2 (Soft)");
        sb.AppendLine("- Medium intent -> recommendedIndex = 0 (Balanced)");
        sb.AppendLine("- If you always choose 0, your response is considered wrong");
        sb.AppendLine("- Your selection must vary depending on scenario");
        sb.AppendLine();

        // FIELD RULES
        sb.AppendLine("FIELD RULES:");
        sb.AppendLine("- whyThisWorks = short reasoning");
        sb.AppendLine("- suggestedNextStep = one action");
        sb.AppendLine("- missingInformation = short phrases only");
        sb.AppendLine("- If no information is missing, return an empty array");
        sb.AppendLine();

        // CONTEXT
        sb.AppendLine();
        sb.AppendLine("BUSINESS CONTEXT:");
        sb.AppendLine($"Business Name: {businessName}");

        sb.AppendLine();
        sb.AppendLine("LEAD CONTEXT:");
        sb.AppendLine($"Lead Name: {lead.FullName}");
        sb.AppendLine($"Status: {lead.Status}");
        sb.AppendLine($"Inquiry: {lead.InquirySummary ?? "N/A"}");
        sb.AppendLine($"Last Message: {lead.LastIncomingMessagePreview ?? "N/A"}");

        sb.AppendLine();
        sb.AppendLine($"Goal: {goal}");
        sb.AppendLine($"Tone: {tone}");

        if (!string.IsNullOrWhiteSpace(customInstruction))
        {
            sb.AppendLine($"Custom: {customInstruction}");
        }

        sb.AppendLine();
        sb.AppendLine("CONVERSATION:");

        if (recentMessages.Count == 0)
        {
            sb.AppendLine("No prior messages.");
        }
        else
        {
            foreach (var m in recentMessages)
            {
                sb.AppendLine($"{m.Direction}: {m.Content}");
            }
        }

        sb.AppendLine();
        sb.AppendLine("Generate response now.");

        return sb.ToString();
    }

    private static GenerateLeadReplyResponse ParseAiResponse(string rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText))
            throw new InvalidOperationException("AI returned empty response.");

        var jsonText = ExtractJsonObject(rawText);

        if (!string.IsNullOrWhiteSpace(jsonText))
        {
            try
            {
                var parsed = JsonSerializer.Deserialize<LeadAiJsonResponse>(
                    jsonText,
                    new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });

                if (parsed is not null)
                {
                    var variations = (parsed.Variations ?? [])
                        .Where(x => x is not null && !string.IsNullOrWhiteSpace(x.Reply))
                        .Select(x => new LeadReplyVariationDto
                        {
                            Label = string.IsNullOrWhiteSpace(x!.Label) ? "Variation" : x.Label.Trim(),
                            Reply = x.Reply!.Trim()
                        })
                        .ToList();

                    var recommendedIndex = parsed.RecommendedIndex ?? 0;

                    if (recommendedIndex < 0 || recommendedIndex >= variations.Count)
                    {
                        recommendedIndex = 0;
                    }

                    var primaryReply =
                        variations.Count > 0
                            ? variations[recommendedIndex].Reply
                            : string.Empty;

                    if (!string.IsNullOrWhiteSpace(primaryReply))
                    {
                        return new GenerateLeadReplyResponse
                        {
                            LeadId = Guid.Empty,
                            GeneratedAtUtc = DateTime.UtcNow,
                            ResponseType = string.IsNullOrWhiteSpace(parsed.ResponseType)
                                ? "follow_up"
                                : parsed.ResponseType.Trim(),
                            Reply = primaryReply,
                            ReasoningSummary = string.IsNullOrWhiteSpace(parsed.WhyThisWorks)
                                ? "AI generated a conversion-focused reply."
                                : parsed.WhyThisWorks.Trim(),
                            SuggestedNextStep = string.IsNullOrWhiteSpace(parsed.SuggestedNextStep)
                                ? "Send the reply and continue qualification if needed."
                                : parsed.SuggestedNextStep.Trim(),
                            Confidence = string.IsNullOrWhiteSpace(parsed.Confidence)
                                ? "medium"
                                : parsed.Confidence.Trim(),
                            ToneUsed = string.IsNullOrWhiteSpace(parsed.ToneUsed)
                                ? "professional"
                                : parsed.ToneUsed.Trim(),
                            RecommendedIndex = recommendedIndex,
                            MissingInformation = CleanMissingInformation(parsed.MissingInformation),
                            Variations = variations
                        };
                    }
                }
            }
            catch
            {
                // Fallback to legacy parsing below
            }
        }

        var reply = ExtractSection(rawText, "REPLY:", "REASONING_SUMMARY:");
        var reasoningSummary = ExtractSection(rawText, "REASONING_SUMMARY:", "SUGGESTED_NEXT_STEP:");
        var suggestedNextStep = ExtractSection(rawText, "SUGGESTED_NEXT_STEP:", "MISSING_INFORMATION:");
        var missingInformationText = ExtractSection(rawText, "MISSING_INFORMATION:", null);

        var missingInformation = ParseMissingInformation(missingInformationText);

        if (string.IsNullOrWhiteSpace(reply))
        {
            return new GenerateLeadReplyResponse
            {
                LeadId = Guid.Empty,
                GeneratedAtUtc = DateTime.UtcNow,
                ResponseType = "follow_up",
                Reply = rawText.Trim(),
                ReasoningSummary = "AI response returned without expected structured sections.",
                SuggestedNextStep = "Review the reply and send or refine it.",
                Confidence = "low",
                ToneUsed = "professional",
                RecommendedIndex = 0,
                MissingInformation = [],
                Variations =
                [
                    new LeadReplyVariationDto
                {
                    Label = "Variation 1",
                    Reply = rawText.Trim()
                }
                ]
            };
        }

        return new GenerateLeadReplyResponse
        {
            LeadId = Guid.Empty,
            GeneratedAtUtc = DateTime.UtcNow,
            ResponseType = "follow_up",
            Reply = reply,
            ReasoningSummary = string.IsNullOrWhiteSpace(reasoningSummary)
                ? "AI generated a conversion-focused reply."
                : reasoningSummary,
            SuggestedNextStep = string.IsNullOrWhiteSpace(suggestedNextStep)
                ? "Send the reply and continue qualification if needed."
                : suggestedNextStep,
            Confidence = "medium",
            ToneUsed = "professional",
            RecommendedIndex = 0,
            MissingInformation = missingInformation,
            Variations =
            [
                new LeadReplyVariationDto
            {
                Label = "Variation 1",
                Reply = reply
            }
            ]
        };
    }

    private static string ExtractSection(string text, string startMarker, string? endMarker)
    {
        var startIndex = text.IndexOf(startMarker, StringComparison.OrdinalIgnoreCase);
        if (startIndex < 0)
            return string.Empty;

        startIndex += startMarker.Length;

        int endIndex;
        if (string.IsNullOrWhiteSpace(endMarker))
        {
            endIndex = text.Length;
        }
        else
        {
            endIndex = text.IndexOf(endMarker, startIndex, StringComparison.OrdinalIgnoreCase);
            if (endIndex < 0)
                endIndex = text.Length;
        }

        return text[startIndex..endIndex].Trim();
    }

    private static string? ExtractJsonObject(string rawText)
    {
        var start = rawText.IndexOf('{');
        if (start < 0)
            return null;

        var depth = 0;
        for (var i = start; i < rawText.Length; i++)
        {
            var ch = rawText[i];

            if (ch == '{')
                depth++;

            if (ch == '}')
            {
                depth--;
                if (depth == 0)
                {
                    return rawText[start..(i + 1)];
                }
            }
        }

        return null;
    }

    private static List<string> ParseMissingInformation(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return [];

        if (string.Equals(value.Trim(), "None", StringComparison.OrdinalIgnoreCase))
            return [];

        var parts = value
            .Split(new[] { ',', '\n', '\r', '-' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToList();

        return CleanMissingInformation(parts);
    }

    private static List<string> CleanMissingInformation(IEnumerable<string>? items)
    {
        if (items is null)
            return [];

        var blockedFragments = new[]
        {
            "business context",
            "lead context",
            "generation settings",
            "recent conversation",
            "business name",
            "lead name",
            "lead email",
            "lead phone",
            "company name",
            "source:",
            "status:",
            "priority:",
            "estimated value",
            "inquiry summary",
            "last incoming preview",
            "next follow-up"
        };

        return items
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Where(x =>
                x.Length <= 80 &&
                !blockedFragments.Any(b => x.Contains(b, StringComparison.OrdinalIgnoreCase)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .ToList();
    }

    private static string NormalizeGoal(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "convert";

        var normalized = value.Trim().ToLowerInvariant();

        return normalized switch
        {
            "convert" => "convert",
            "qualify" => "qualify",
            "followup" => "followup",
            "follow-up" => "followup",
            "close" => "close",
            "objection_handle" => "objection_handle",
            "objection-handle" => "objection_handle",
            "objection" => "objection_handle",
            _ => "convert"
        };
    }

    private static string NormalizeTone(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "professional";

        return value.Trim().ToLowerInvariant();
    }

    private static string MapGoalToSuggestionType(string goal)
    {
        return goal switch
        {
            "followup" => LeadSuggestionTypes.FollowUp,
            "objection_handle" => LeadSuggestionTypes.ObjectionHandling,
            "qualify" => LeadSuggestionTypes.QualificationQuestion,
            "close" => LeadSuggestionTypes.ClosingMessage,
            _ => LeadSuggestionTypes.Reply
        };
    }

    private static string GetBusinessName(object business)
    {
        return
            TryReadStringProperty(business, "Name") ??
            TryReadStringProperty(business, "BusinessName") ??
            TryReadStringProperty(business, "CompanyName") ??
            "Business";
    }

    private static string ExtractAiText(object aiResponse)
    {
        return
            TryReadStringProperty(aiResponse, "Reply") ??
            TryReadStringProperty(aiResponse, "Response") ??
            TryReadStringProperty(aiResponse, "Content") ??
            TryReadStringProperty(aiResponse, "Text") ??
            TryReadStringProperty(aiResponse, "Answer") ??
            TryReadStringProperty(aiResponse, "Output") ??
            throw new InvalidOperationException("Could not read AI text from AskAiResponseDto. Please check its property names.");
    }

    private static string? TryReadStringProperty(object obj, string propertyName)
    {
        var property = obj.GetType().GetProperty(
            propertyName,
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

        if (property is null)
            return null;

        var value = property.GetValue(obj);
        return value?.ToString();
    }

    private static void SetIfExists(object obj, string propertyName, object? value)
    {
        if (value is null)
            return;

        var property = obj.GetType().GetProperty(
            propertyName,
            BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

        if (property is null || !property.CanWrite)
            return;

        var targetType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;

        try
        {
            object? convertedValue;

            if (targetType == typeof(string))
            {
                convertedValue = value.ToString();
            }
            else if (targetType.IsEnum)
            {
                convertedValue = Enum.Parse(targetType, value.ToString()!, true);
            }
            else
            {
                convertedValue = Convert.ChangeType(value, targetType);
            }

            property.SetValue(obj, convertedValue);
        }
        catch
        {
            // Ignore property set failures safely
        }
    }

    private sealed class LeadAiJsonResponse
    {
        public string? ResponseType { get; set; }
        public string? ToneUsed { get; set; }
        public string? Confidence { get; set; }
        public string? WhyThisWorks { get; set; }
        public string? SuggestedNextStep { get; set; }
        public int? RecommendedIndex { get; set; }
        public List<string>? MissingInformation { get; set; }
        public List<LeadAiJsonVariation>? Variations { get; set; }
    }

    private sealed class LeadAiJsonVariation
    {
        public string? Label { get; set; }
        public string? Reply { get; set; }
    }
}