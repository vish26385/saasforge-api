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

        // IMPORTANT:
        // Load the FULL stored conversation history for this lead.
        // User explicitly requested no limit on past conversation memory.
        var conversationMessages = await _context.LeadMessages
            .AsNoTracking()
            .Where(x => x.BusinessId == businessId && x.LeadId == request.LeadId)
            .OrderBy(x => x.CreatedAtUtc)
            .ToListAsync();

        var business = await _context.Businesses
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == businessId);

        if (business is null)
            throw new InvalidOperationException("Business not found.");

        await _usageService.EnsureCanUseAiAsync(businessId);

        var latestIncomingMessage = conversationMessages
            .LastOrDefault(x => string.Equals(x.Direction, "Incoming", StringComparison.OrdinalIgnoreCase));

        var latestOutgoingMessage = conversationMessages
            .LastOrDefault(x => string.Equals(x.Direction, "Outgoing", StringComparison.OrdinalIgnoreCase));

        var prompt = BuildLeadReplyPrompt(
            businessName: GetBusinessName(business),
            lead: lead,
            conversationMessages: conversationMessages,
            latestIncomingMessage: latestIncomingMessage,
            latestOutgoingMessage: latestOutgoingMessage,
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

        try
        {
            await _context.SaveChangesAsync();

            // ✅ Increment ONLY after successful save
            await _usageService.IncrementAiUsageAsync(businessId);
        }
        catch (Exception ex)
        {
            // ❌ DO NOT increment usage
            // ❌ DO NOT silently ignore

            throw new InvalidOperationException(
                "AI reply was generated but failed to save. Please try again.",
                ex);
        }

        await _leadActivityService.AddAsync(
            businessId,
            lead.Id,
            LeadActivityTypes.ReplyGenerated,
            "AI reply generated",
            $"AI generated a {goal} reply in {tone} tone using full conversation context.");

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
    //    string businessName,
    //    Lead lead,
    //    List<LeadMessage> conversationMessages,
    //    LeadMessage? latestIncomingMessage,
    //    LeadMessage? latestOutgoingMessage,
    //    string goal,
    //    string tone,
    //    string? customInstruction)
    //{
    //    var sb = new StringBuilder();

    //    sb.AppendLine("You are LeadFlow AI, a world-class AI sales assistant designed to help businesses convert leads into paying customers.");
    //    sb.AppendLine();
    //    sb.AppendLine("Your job is to generate high-quality reply suggestions that help the business owner or team respond manually.");
    //    sb.AppendLine("IMPORTANT: LeadFlow AI suggests replies only. It does NOT automatically send messages, auto-reply, or claim that the system is already automating communication.");
    //    sb.AppendLine();

    //    sb.AppendLine("CORE OBJECTIVE:");
    //    sb.AppendLine("- Maximize probability of conversion");
    //    sb.AppendLine("- Increase reply rate");
    //    sb.AppendLine("- Reduce friction and hesitation");
    //    sb.AppendLine("- Guide the lead toward the right next step");
    //    sb.AppendLine("- Help the user respond faster with a message that feels natural and ready to send");
    //    sb.AppendLine();

    //    sb.AppendLine("HUMAN-LIKE WRITING RULES:");
    //    sb.AppendLine("- Sound like a real human, not AI");
    //    sb.AppendLine("- Keep it short, clear, and natural");
    //    sb.AppendLine("- Avoid robotic, generic, or salesy tone");
    //    sb.AppendLine("- Avoid hype or exaggerated claims");
    //    sb.AppendLine("- Avoid repeating lead context unnecessarily");
    //    sb.AppendLine("- Avoid long paragraphs");
    //    sb.AppendLine("- No bullet points in the reply");
    //    sb.AppendLine("- No emojis unless absolutely natural");
    //    sb.AppendLine("- Message must feel ready to send instantly by the user");
    //    sb.AppendLine("- Keep each reply concise and practical");
    //    sb.AppendLine();

    //    sb.AppendLine("PRODUCT TRUTH RULES (VERY IMPORTANT):");
    //    sb.AppendLine("- Do NOT say or imply that the system automatically replies to customers");
    //    sb.AppendLine("- Do NOT say or imply that replies are automatically sent");
    //    sb.AppendLine("- Do NOT describe features that are not explicitly present in the provided context");
    //    sb.AppendLine("- Do NOT mention automation, routing, workflows, AI agents, or integrations unless the lead explicitly asked and the provided context confirms it");
    //    sb.AppendLine("- If the lead asks how the product works, describe it as helping the business respond faster, organize leads, and manage follow-ups");
    //    sb.AppendLine("- Keep product descriptions truthful, grounded, and aligned with the provided context only");
    //    sb.AppendLine();

    //    sb.AppendLine("MEMORY AND CONVERSATION RULES (VERY IMPORTANT):");
    //    sb.AppendLine("- You MUST read the complete conversation history provided below from oldest to latest");
    //    sb.AppendLine("- Treat the entire conversation history as memory");
    //    sb.AppendLine("- Never ignore earlier messages if they are relevant to the latest customer reply");
    //    sb.AppendLine("- Never contradict earlier commitments, explanations, pricing mentions, or promises already made in the conversation");
    //    sb.AppendLine("- If something was already explained earlier, do not repeat the same full explanation again unless the customer clearly asks for it");
    //    sb.AppendLine("- Build on the conversation naturally instead of restarting it");
    //    sb.AppendLine("- If the customer has already shown interest, move forward instead of repeating generic introduction messages");
    //    sb.AppendLine();

    //    sb.AppendLine("ANTI-REPETITION RULES (CRITICAL):");
    //    sb.AppendLine("- Never repeat the same reply or near-identical reply already sent earlier in the conversation");
    //    sb.AppendLine("- Never repeat the same CTA again and again");
    //    sb.AppendLine("- If the previous outgoing message already answered the question, acknowledge that context and move the conversation forward");
    //    sb.AppendLine("- If the lead gives a short reply after a detailed explanation, assume they are responding to that context");
    //    sb.AppendLine("- Do not generate lazy filler replies");
    //    sb.AppendLine();

    //    sb.AppendLine("SHORT REPLY INTERPRETATION RULES (CRITICAL):");
    //    sb.AppendLine("- Many customer replies are short but meaningful");
    //    sb.AppendLine("- You MUST interpret short replies using the full conversation context");
    //    sb.AppendLine("- Examples of short replies include: ok, okay, yes, yeah, hmm, tell price, price?, go ahead, give me, show me, yes give me, tell me more, send details, how much, what next");
    //    sb.AppendLine("- If the latest customer message is short, infer the likely intent from the earlier conversation");
    //    sb.AppendLine("- If the short reply means interest, move toward the next useful step");
    //    sb.AppendLine("- If the short reply means pricing interest, answer pricing or value directly");
    //    sb.AppendLine("- If the short reply means confirmation, continue the flow rather than repeating old content");
    //    sb.AppendLine("- If the short reply is genuinely unclear, ask one focused clarifying question instead of giving a generic repeated answer");
    //    sb.AppendLine();

    //    sb.AppendLine("CRITICAL SALES INTELLIGENCE RULES:");
    //    sb.AppendLine("- Do NOT always push for a call");
    //    sb.AppendLine("- Choose response strategy based on lead intent:");
    //    sb.AppendLine("  • Cold -> Soft");
    //    sb.AppendLine("  • Price concern -> Balanced");
    //    sb.AppendLine("  • Interested -> Direct");
    //    sb.AppendLine("  • Confused -> Balanced");
    //    sb.AppendLine("  • Competitor -> Soft");
    //    sb.AppendLine("  • Close -> Direct");
    //    sb.AppendLine("  • Not ready -> Soft");
    //    sb.AppendLine("- Always answer the lead first before CTA");
    //    sb.AppendLine("- CTA must feel natural, not forced");
    //    sb.AppendLine("- If pricing is asked, answer pricing/value before asking for next step");
    //    sb.AppendLine("- If the lead is already warm, do not go back to cold-intro mode");
    //    sb.AppendLine();

    //    sb.AppendLine("STRICT OUTPUT RULES:");
    //    sb.AppendLine("- Return only valid JSON");
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

    //    sb.AppendLine("VARIATION RULES:");
    //    sb.AppendLine("- Balanced = neutral, helpful, moderate CTA");
    //    sb.AppendLine("- Direct = stronger CTA, used when intent is high");
    //    sb.AppendLine("- Soft = lower pressure, used when lead is hesitant or early stage");
    //    sb.AppendLine("- All variations must still respect memory and anti-repetition rules");
    //    sb.AppendLine();

    //    sb.AppendLine("CRITICAL RECOMMENDATION LOGIC:");
    //    sb.AppendLine("- You must not always choose 0");
    //    sb.AppendLine("- You must choose recommendedIndex based on lead intent");
    //    sb.AppendLine("- High intent / ready -> recommendedIndex = 1 (Direct)");
    //    sb.AppendLine("- Low intent / cold / hesitant -> recommendedIndex = 2 (Soft)");
    //    sb.AppendLine("- Medium intent -> recommendedIndex = 0 (Balanced)");
    //    sb.AppendLine("- If you always choose 0, your response is considered wrong");
    //    sb.AppendLine("- Your selection must vary depending on scenario");
    //    sb.AppendLine();

    //    sb.AppendLine("FIELD RULES:");
    //    sb.AppendLine("- whyThisWorks = short reasoning");
    //    sb.AppendLine("- suggestedNextStep = one action");
    //    sb.AppendLine("- missingInformation = short phrases only");
    //    sb.AppendLine("- If no information is missing, return an empty array");
    //    sb.AppendLine("- whyThisWorks should mention conversation context if useful");
    //    sb.AppendLine();

    //    sb.AppendLine();
    //    sb.AppendLine("BUSINESS CONTEXT:");
    //    sb.AppendLine($"Business Name: {businessName}");

    //    sb.AppendLine();
    //    sb.AppendLine("LEAD CONTEXT:");
    //    sb.AppendLine($"Lead Name: {lead.FullName}");
    //    sb.AppendLine($"Status: {lead.Status}");
    //    sb.AppendLine($"Priority: {lead.Priority}");
    //    sb.AppendLine($"Source: {lead.Source}");
    //    sb.AppendLine($"Company Name: {lead.CompanyName ?? "N/A"}");
    //    sb.AppendLine($"Inquiry: {lead.InquirySummary ?? "N/A"}");
    //    sb.AppendLine($"Lead Last Incoming Preview: {lead.LastIncomingMessagePreview ?? "N/A"}");

    //    sb.AppendLine();
    //    sb.AppendLine("GENERATION SETTINGS:");
    //    sb.AppendLine($"Goal: {goal}");
    //    sb.AppendLine($"Tone: {tone}");

    //    if (!string.IsNullOrWhiteSpace(customInstruction))
    //    {
    //        sb.AppendLine($"Custom: {customInstruction}");
    //    }

    //    sb.AppendLine();
    //    sb.AppendLine("LATEST CUSTOMER MESSAGE:");
    //    sb.AppendLine(latestIncomingMessage?.Content?.Trim() ?? "No incoming customer message found.");

    //    sb.AppendLine();
    //    sb.AppendLine("LAST OUTGOING MESSAGE SENT OR DRAFTED:");
    //    sb.AppendLine(latestOutgoingMessage?.Content?.Trim() ?? "No prior outgoing message found.");

    //    sb.AppendLine();
    //    sb.AppendLine("FULL CONVERSATION HISTORY (OLDEST TO LATEST):");

    //    if (conversationMessages.Count == 0)
    //    {
    //        sb.AppendLine("No prior messages.");
    //    }
    //    else
    //    {
    //        foreach (var message in conversationMessages)
    //        {
    //            var role = string.Equals(message.Direction, "Incoming", StringComparison.OrdinalIgnoreCase)
    //                ? "Customer"
    //                : "You";

    //            var channel = string.IsNullOrWhiteSpace(message.Channel)
    //                ? "Unknown"
    //                : message.Channel.Trim();

    //            var sentState = string.Equals(message.Direction, "Outgoing", StringComparison.OrdinalIgnoreCase)
    //                ? (message.IsSent ? "Sent" : "Draft")
    //                : "Received";

    //            sb.AppendLine(
    //                $"{role} | {channel} | {sentState} | {message.CreatedAtUtc:O}: {message.Content}");
    //        }
    //    }

    //    sb.AppendLine();
    //    sb.AppendLine("IMPORTANT RESPONSE BEHAVIOR:");
    //    sb.AppendLine("- First understand the real intent of the latest customer message in the context of the full history");
    //    sb.AppendLine("- Then generate the best next reply");
    //    sb.AppendLine("- Do not restart the conversation");
    //    sb.AppendLine("- Do not repeat old explanations unless necessary");
    //    sb.AppendLine("- If the customer message is short, infer intent from the history before replying");
    //    sb.AppendLine();

    //    sb.AppendLine("Generate response now.");

    //    return sb.ToString();
    //}


    private static string BuildLeadReplyPrompt(
    string businessName,
    Lead lead,
    List<LeadMessage> conversationMessages,
    LeadMessage? latestIncomingMessage,
    LeadMessage? latestOutgoingMessage,
    string goal,
    string tone,
    string? customInstruction)
    {
        var sb = new StringBuilder();

        sb.AppendLine("You are LeadFlow AI, a world-class AI sales assistant designed to help businesses convert leads into paying customers.");
        sb.AppendLine();
        sb.AppendLine("Your job is to generate high-quality reply suggestions that help the business owner or team respond manually.");
        sb.AppendLine("IMPORTANT: LeadFlow AI suggests replies only. It does NOT automatically send messages.");
        sb.AppendLine();

        sb.AppendLine("CORE OBJECTIVE:");
        sb.AppendLine("- Maximize probability of conversion");
        sb.AppendLine("- Increase reply rate");
        sb.AppendLine("- Reduce friction and hesitation");
        sb.AppendLine("- Guide the lead toward the next step");
        sb.AppendLine();

        sb.AppendLine("HUMAN-LIKE WRITING RULES:");
        sb.AppendLine("- Sound like a real human");
        sb.AppendLine("- Keep it short, clear, and natural");
        sb.AppendLine("- Avoid robotic or technical language");
        sb.AppendLine("- No long paragraphs");
        sb.AppendLine("- Message must feel instantly sendable");
        sb.AppendLine();

        // 🔥 NEW CRITICAL BLOCK (THIS FIXES YOUR ISSUE)
        sb.AppendLine("ACTIONABLE RESPONSE RULES (VERY IMPORTANT):");
        sb.AppendLine("- Always give clear step-by-step instructions when solving a problem");
        sb.AppendLine("- NEVER ask user to describe technical settings");
        sb.AppendLine("- NEVER ask for screenshots or file uploads");
        sb.AppendLine("- Always guide user using simple phone navigation steps (like Settings → Apps → WhatsApp)");
        sb.AppendLine("- Always end with a clear next action (example: 'Once done, reply Done')");
        sb.AppendLine("- Prefer simple actions over explanations");
        sb.AppendLine("- Reduce thinking effort for the user");
        sb.AppendLine();

        sb.AppendLine("BAD EXAMPLES (DO NOT DO):");
        sb.AppendLine("- 'Check your settings and tell me what you see'");
        sb.AppendLine("- 'Send screenshot'");
        sb.AppendLine("- 'Describe your issue in detail'");
        sb.AppendLine();

        sb.AppendLine("GOOD EXAMPLES (FOLLOW THIS STYLE):");
        sb.AppendLine("Got it 👍");
        sb.AppendLine("Let’s fix this quickly.");
        sb.AppendLine("Open: Settings → Apps → WhatsApp → Notifications");
        sb.AppendLine("Make sure all notifications are ON.");
        sb.AppendLine("Then go to: Settings → Battery → Battery Optimization → WhatsApp → Set to Don't optimize");
        sb.AppendLine("Once done, reply Done.");
        sb.AppendLine();

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
        sb.AppendLine("BUSINESS CONTEXT:");
        sb.AppendLine($"Business Name: {businessName}");

        sb.AppendLine();
        sb.AppendLine("LEAD CONTEXT:");
        sb.AppendLine($"Lead Name: {lead.FullName}");
        sb.AppendLine($"Status: {lead.Status}");
        sb.AppendLine($"Priority: {lead.Priority}");
        sb.AppendLine($"Source: {lead.Source}");

        sb.AppendLine();
        sb.AppendLine("LATEST CUSTOMER MESSAGE:");
        sb.AppendLine(latestIncomingMessage?.Content?.Trim() ?? "No incoming message.");

        sb.AppendLine();
        sb.AppendLine("CONVERSATION HISTORY:");

        foreach (var message in conversationMessages)
        {
            var role = message.Direction == "Incoming" ? "Customer" : "You";
            sb.AppendLine($"{role}: {message.Content}");
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
            "full conversation history",
            "latest customer message",
            "last outgoing message",
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

#region Dont delete
//// 🔥 UPDATED VERSION — FULL FILE

//using Microsoft.EntityFrameworkCore;
//using SaaSForge.Api.Data;
//using SaaSForge.Api.DTOs.Ai;
//using SaaSForge.Api.Modules.Leads.Constants;
//using SaaSForge.Api.Modules.Leads.Dtos;
//using SaaSForge.Api.Modules.Leads.Entities;
//using SaaSForge.Api.Modules.Leads.Interfaces;
//using SaaSForge.Api.Services.Ai;
//using SaaSForge.Api.Services.Usage;
//using System.Reflection;
//using System.Text;
//using System.Text.Json;

//namespace SaaSForge.Api.Modules.Leads.Services;

//public class LeadAiService : ILeadAiService
//{
//    private readonly AppDbContext _context;
//    private readonly IAiService _aiService;
//    private readonly IUsageService _usageService;
//    private readonly ILeadActivityService _leadActivityService;

//    public LeadAiService(
//        AppDbContext context,
//        IAiService aiService,
//        IUsageService usageService,
//        ILeadActivityService leadActivityService)
//    {
//        _context = context;
//        _aiService = aiService;
//        _usageService = usageService;
//        _leadActivityService = leadActivityService;
//    }

//    public async Task<GenerateLeadReplyResponse> GenerateReplyAsync(
//        int businessId,
//        string userId,
//        GenerateLeadReplyRequest request)
//    {
//        if (request.LeadId == Guid.Empty)
//            throw new InvalidOperationException("LeadId is required.");

//        if (string.IsNullOrWhiteSpace(userId))
//            throw new InvalidOperationException("UserId is required.");

//        var goal = NormalizeGoal(request.Goal);
//        var tone = NormalizeTone(request.Tone);

//        var lead = await _context.Leads
//            .AsNoTracking()
//            .FirstOrDefaultAsync(x =>
//                x.BusinessId == businessId &&
//                x.Id == request.LeadId &&
//                !x.IsArchived);

//        if (lead is null)
//            throw new InvalidOperationException("Lead not found.");

//        // 🔥🔥🔥 FIX — LOAD FULL CONVERSATION (NOT JUST 8)
//        var allMessages = await _context.LeadMessages
//            .AsNoTracking()
//            .Where(x => x.BusinessId == businessId && x.LeadId == request.LeadId)
//            .OrderBy(x => x.CreatedAtUtc)
//            .ToListAsync();

//        // 🔥 SAFE LIMIT (avoid huge token usage)
//        var messages = allMessages.Count > 50
//            ? allMessages.TakeLast(50).ToList()
//            : allMessages;

//        var business = await _context.Businesses
//            .AsNoTracking()
//            .FirstOrDefaultAsync(x => x.Id == businessId);

//        if (business is null)
//            throw new InvalidOperationException("Business not found.");

//        await _usageService.EnsureCanUseAiAsync(businessId);

//        var prompt = BuildLeadReplyPrompt(
//            businessName: GetBusinessName(business),
//            lead: lead,
//            recentMessages: messages,
//            goal: goal,
//            tone: tone,
//            customInstruction: request.CustomInstruction);

//        var aiRequest = BuildAskAiRequest(prompt, tone, goal);

//        var aiResponse = await _aiService.AskAsync(userId, aiRequest);
//        var rawAiText = ExtractAiText(aiResponse);

//        var parsed = ParseAiResponse(rawAiText);

//        var suggestion = new LeadAiSuggestion
//        {
//            Id = Guid.NewGuid(),
//            BusinessId = businessId,
//            LeadId = lead.Id,
//            SuggestionType = MapGoalToSuggestionType(goal),
//            InputContext = prompt,
//            OutputText = parsed.Reply,
//            Tone = tone,
//            Goal = goal,
//            Model = TryReadStringProperty(aiResponse, "Model"),
//            IsUsed = false,
//            CreatedAtUtc = DateTime.UtcNow
//        };

//        _context.LeadAiSuggestions.Add(suggestion);
//        await _context.SaveChangesAsync();

//        await _leadActivityService.AddAsync(
//            businessId,
//            lead.Id,
//            LeadActivityTypes.ReplyGenerated,
//            "AI reply generated",
//            $"AI generated a {goal} reply in {tone} tone.");

//        parsed.LeadId = lead.Id;

//        return parsed;
//    }

//    private static AskAiRequestDto BuildAskAiRequest(string prompt, string tone, string goal)
//    {
//        var dto = new AskAiRequestDto();

//        SetIfExists(dto, "Message", prompt);
//        SetIfExists(dto, "Prompt", prompt);
//        SetIfExists(dto, "Input", prompt);
//        SetIfExists(dto, "UserMessage", prompt);
//        SetIfExists(dto, "Question", prompt);

//        SetIfExists(dto, "Tone", tone);
//        SetIfExists(dto, "Goal", goal);
//        SetIfExists(dto, "Focus", goal);
//        SetIfExists(dto, "Category", "lead-conversion");
//        SetIfExists(dto, "Platform", "leadflow-ai");
//        SetIfExists(dto, "Context", "lead-conversion");
//        SetIfExists(dto, "UseCase", "lead-conversion");

//        return dto;
//    }

//    // 🔥🔥🔥 MASSIVE UPGRADE HERE
//    private static string BuildLeadReplyPrompt(
//        string businessName,
//        Lead lead,
//        List<LeadMessage> recentMessages,
//        string goal,
//        string tone,
//        string? customInstruction)
//    {
//        var sb = new StringBuilder();

//        sb.AppendLine("You are a world-class sales assistant AI.");
//        sb.AppendLine();

//        sb.AppendLine("CRITICAL:");
//        sb.AppendLine("- You MUST understand customer intent even if message is short");
//        sb.AppendLine("- Short messages like 'ok', 'yes', 'hmm', 'tell price', 'go ahead' MUST be interpreted correctly");
//        sb.AppendLine("- Use full conversation context to understand intent");
//        sb.AppendLine();

//        sb.AppendLine("INTENT DETECTION RULES:");
//        sb.AppendLine("- 'ok/yes/hmm' → customer is interested or acknowledging");
//        sb.AppendLine("- 'price' → customer evaluating cost");
//        sb.AppendLine("- 'tell me' → customer wants details");
//        sb.AppendLine("- 'go ahead' → high intent");
//        sb.AppendLine("- Always infer intent from history");
//        sb.AppendLine();

//        sb.AppendLine("RESPONSE RULES:");
//        sb.AppendLine("- Human-like");
//        sb.AppendLine("- Short & natural");
//        sb.AppendLine("- No robotic text");
//        sb.AppendLine("- Ready to send");
//        sb.AppendLine();

//        sb.AppendLine();
//        sb.AppendLine("BUSINESS:");
//        sb.AppendLine(businessName);

//        sb.AppendLine();
//        sb.AppendLine("LEAD:");
//        sb.AppendLine($"Name: {lead.FullName}");
//        sb.AppendLine($"Inquiry: {lead.InquirySummary}");

//        sb.AppendLine();
//        sb.AppendLine("CONVERSATION (IMPORTANT):");

//        foreach (var m in recentMessages)
//        {
//            var role = m.Direction == "Incoming" ? "Customer" : "You";
//            sb.AppendLine($"{role}: {m.Content}");
//        }

//        sb.AppendLine();
//        sb.AppendLine("Generate best reply now.");

//        return sb.ToString();
//    }

//    // ❌ DO NOT CHANGE BELOW (keep your existing parsing)

//    private static GenerateLeadReplyResponse ParseAiResponse(string rawText)
//    {
//        if (string.IsNullOrWhiteSpace(rawText))
//            throw new InvalidOperationException("AI returned empty response.");

//        var jsonText = ExtractJsonObject(rawText);

//        if (!string.IsNullOrWhiteSpace(jsonText))
//        {
//            try
//            {
//                var parsed = JsonSerializer.Deserialize<LeadAiJsonResponse>(
//                    jsonText,
//                    new JsonSerializerOptions
//                    {
//                        PropertyNameCaseInsensitive = true
//                    });

//                if (parsed is not null)
//                {
//                    var variations = (parsed.Variations ?? [])
//                        .Where(x => x is not null && !string.IsNullOrWhiteSpace(x.Reply))
//                        .Select(x => new LeadReplyVariationDto
//                        {
//                            Label = string.IsNullOrWhiteSpace(x!.Label) ? "Variation" : x.Label.Trim(),
//                            Reply = x.Reply!.Trim()
//                        })
//                        .ToList();

//                    var recommendedIndex = parsed.RecommendedIndex ?? 0;

//                    if (recommendedIndex < 0 || recommendedIndex >= variations.Count)
//                    {
//                        recommendedIndex = 0;
//                    }

//                    var primaryReply =
//                        variations.Count > 0
//                            ? variations[recommendedIndex].Reply
//                            : string.Empty;

//                    if (!string.IsNullOrWhiteSpace(primaryReply))
//                    {
//                        return new GenerateLeadReplyResponse
//                        {
//                            LeadId = Guid.Empty,
//                            GeneratedAtUtc = DateTime.UtcNow,
//                            ResponseType = string.IsNullOrWhiteSpace(parsed.ResponseType)
//                                ? "follow_up"
//                                : parsed.ResponseType.Trim(),
//                            Reply = primaryReply,
//                            ReasoningSummary = string.IsNullOrWhiteSpace(parsed.WhyThisWorks)
//                                ? "AI generated a conversion-focused reply."
//                                : parsed.WhyThisWorks.Trim(),
//                            SuggestedNextStep = string.IsNullOrWhiteSpace(parsed.SuggestedNextStep)
//                                ? "Send the reply and continue qualification if needed."
//                                : parsed.SuggestedNextStep.Trim(),
//                            Confidence = string.IsNullOrWhiteSpace(parsed.Confidence)
//                                ? "medium"
//                                : parsed.Confidence.Trim(),
//                            ToneUsed = string.IsNullOrWhiteSpace(parsed.ToneUsed)
//                                ? "professional"
//                                : parsed.ToneUsed.Trim(),
//                            RecommendedIndex = recommendedIndex,
//                            MissingInformation = CleanMissingInformation(parsed.MissingInformation),
//                            Variations = variations
//                        };
//                    }
//                }
//            }
//            catch
//            {
//                // Fallback to legacy parsing below
//            }
//        }

//        var reply = ExtractSection(rawText, "REPLY:", "REASONING_SUMMARY:");
//        var reasoningSummary = ExtractSection(rawText, "REASONING_SUMMARY:", "SUGGESTED_NEXT_STEP:");
//        var suggestedNextStep = ExtractSection(rawText, "SUGGESTED_NEXT_STEP:", "MISSING_INFORMATION:");
//        var missingInformationText = ExtractSection(rawText, "MISSING_INFORMATION:", null);

//        var missingInformation = ParseMissingInformation(missingInformationText);

//        if (string.IsNullOrWhiteSpace(reply))
//        {
//            return new GenerateLeadReplyResponse
//            {
//                LeadId = Guid.Empty,
//                GeneratedAtUtc = DateTime.UtcNow,
//                ResponseType = "follow_up",
//                Reply = rawText.Trim(),
//                ReasoningSummary = "AI response returned without expected structured sections.",
//                SuggestedNextStep = "Review the reply and send or refine it.",
//                Confidence = "low",
//                ToneUsed = "professional",
//                RecommendedIndex = 0,
//                MissingInformation = [],
//                Variations =
//                [
//                    new LeadReplyVariationDto
//                    {
//                        Label = "Variation 1",
//                        Reply = rawText.Trim()
//                    }
//                ]
//            };
//        }

//        return new GenerateLeadReplyResponse
//        {
//            LeadId = Guid.Empty,
//            GeneratedAtUtc = DateTime.UtcNow,
//            ResponseType = "follow_up",
//            Reply = reply,
//            ReasoningSummary = string.IsNullOrWhiteSpace(reasoningSummary)
//                ? "AI generated a conversion-focused reply."
//                : reasoningSummary,
//            SuggestedNextStep = string.IsNullOrWhiteSpace(suggestedNextStep)
//                ? "Send the reply and continue qualification if needed."
//                : suggestedNextStep,
//            Confidence = "medium",
//            ToneUsed = "professional",
//            RecommendedIndex = 0,
//            MissingInformation = missingInformation,
//            Variations =
//            [
//                new LeadReplyVariationDto
//                {
//                    Label = "Variation 1",
//                    Reply = reply
//                }
//            ]
//        };
//    }

//    private static string ExtractSection(string text, string startMarker, string? endMarker)
//    {
//        var startIndex = text.IndexOf(startMarker, StringComparison.OrdinalIgnoreCase);
//        if (startIndex < 0)
//            return string.Empty;

//        startIndex += startMarker.Length;

//        int endIndex;
//        if (string.IsNullOrWhiteSpace(endMarker))
//        {
//            endIndex = text.Length;
//        }
//        else
//        {
//            endIndex = text.IndexOf(endMarker, startIndex, StringComparison.OrdinalIgnoreCase);
//            if (endIndex < 0)
//                endIndex = text.Length;
//        }

//        return text[startIndex..endIndex].Trim();
//    }

//    private static string? ExtractJsonObject(string rawText)
//    {
//        var start = rawText.IndexOf('{');
//        if (start < 0)
//            return null;

//        var depth = 0;
//        for (var i = start; i < rawText.Length; i++)
//        {
//            var ch = rawText[i];

//            if (ch == '{')
//                depth++;

//            if (ch == '}')
//            {
//                depth--;
//                if (depth == 0)
//                {
//                    return rawText[start..(i + 1)];
//                }
//            }
//        }

//        return null;
//    }

//    private static List<string> ParseMissingInformation(string value)
//    {
//        if (string.IsNullOrWhiteSpace(value))
//            return [];

//        if (string.Equals(value.Trim(), "None", StringComparison.OrdinalIgnoreCase))
//            return [];

//        var parts = value
//            .Split(new[] { ',', '\n', '\r', '-' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
//            .Where(x => !string.IsNullOrWhiteSpace(x))
//            .ToList();

//        return CleanMissingInformation(parts);
//    }

//    private static List<string> CleanMissingInformation(IEnumerable<string>? items)
//    {
//        if (items is null)
//            return [];

//        var blockedFragments = new[]
//        {
//            "business context",
//            "lead context",
//            "generation settings",
//            "full conversation history",
//            "latest customer message",
//            "last outgoing message",
//            "business name",
//            "lead name",
//            "lead email",
//            "lead phone",
//            "company name",
//            "source:",
//            "status:",
//            "priority:",
//            "estimated value",
//            "inquiry summary",
//            "last incoming preview",
//            "next follow-up"
//        };

//        return items
//            .Where(x => !string.IsNullOrWhiteSpace(x))
//            .Select(x => x.Trim())
//            .Where(x =>
//                x.Length <= 80 &&
//                !blockedFragments.Any(b => x.Contains(b, StringComparison.OrdinalIgnoreCase)))
//            .Distinct(StringComparer.OrdinalIgnoreCase)
//            .Take(5)
//            .ToList();
//    }

//    private static string NormalizeGoal(string? value)
//    {
//        if (string.IsNullOrWhiteSpace(value))
//            return "convert";

//        var normalized = value.Trim().ToLowerInvariant();

//        return normalized switch
//        {
//            "convert" => "convert",
//            "qualify" => "qualify",
//            "followup" => "followup",
//            "follow-up" => "followup",
//            "close" => "close",
//            "objection_handle" => "objection_handle",
//            "objection-handle" => "objection_handle",
//            "objection" => "objection_handle",
//            _ => "convert"
//        };
//    }

//    private static string NormalizeTone(string? value)
//    {
//        if (string.IsNullOrWhiteSpace(value))
//            return "professional";

//        return value.Trim().ToLowerInvariant();
//    }

//    private static string MapGoalToSuggestionType(string goal)
//    {
//        return goal switch
//        {
//            "followup" => LeadSuggestionTypes.FollowUp,
//            "objection_handle" => LeadSuggestionTypes.ObjectionHandling,
//            "qualify" => LeadSuggestionTypes.QualificationQuestion,
//            "close" => LeadSuggestionTypes.ClosingMessage,
//            _ => LeadSuggestionTypes.Reply
//        };
//    }

//    private static string GetBusinessName(object business)
//    {
//        return
//            TryReadStringProperty(business, "Name") ??
//            TryReadStringProperty(business, "BusinessName") ??
//            TryReadStringProperty(business, "CompanyName") ??
//            "Business";
//    }

//    private static string ExtractAiText(object aiResponse)
//    {
//        return
//            TryReadStringProperty(aiResponse, "Reply") ??
//            TryReadStringProperty(aiResponse, "Response") ??
//            TryReadStringProperty(aiResponse, "Content") ??
//            TryReadStringProperty(aiResponse, "Text") ??
//            TryReadStringProperty(aiResponse, "Answer") ??
//            TryReadStringProperty(aiResponse, "Output") ??
//            throw new InvalidOperationException("Could not read AI text from AskAiResponseDto. Please check its property names.");
//    }

//    private static string? TryReadStringProperty(object obj, string propertyName)
//    {
//        var property = obj.GetType().GetProperty(
//            propertyName,
//            BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

//        if (property is null)
//            return null;

//        var value = property.GetValue(obj);
//        return value?.ToString();
//    }

//    private static void SetIfExists(object obj, string propertyName, object? value)
//    {
//        if (value is null)
//            return;

//        var property = obj.GetType().GetProperty(
//            propertyName,
//            BindingFlags.Public | BindingFlags.Instance | BindingFlags.IgnoreCase);

//        if (property is null || !property.CanWrite)
//            return;

//        var targetType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;

//        try
//        {
//            object? convertedValue;

//            if (targetType == typeof(string))
//            {
//                convertedValue = value.ToString();
//            }
//            else if (targetType.IsEnum)
//            {
//                convertedValue = Enum.Parse(targetType, value.ToString()!, true);
//            }
//            else
//            {
//                convertedValue = Convert.ChangeType(value, targetType);
//            }

//            property.SetValue(obj, convertedValue);
//        }
//        catch
//        {
//            // Ignore property set failures safely
//        }
//    }

//    private sealed class LeadAiJsonResponse
//    {
//        public string? ResponseType { get; set; }
//        public string? ToneUsed { get; set; }
//        public string? Confidence { get; set; }
//        public string? WhyThisWorks { get; set; }
//        public string? SuggestedNextStep { get; set; }
//        public int? RecommendedIndex { get; set; }
//        public List<string>? MissingInformation { get; set; }
//        public List<LeadAiJsonVariation>? Variations { get; set; }
//    }

//    private sealed class LeadAiJsonVariation
//    {
//        public string? Label { get; set; }
//        public string? Reply { get; set; }
//    }
//}
#endregion