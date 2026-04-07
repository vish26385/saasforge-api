using System.CodeDom;
using System.Text;
using Microsoft.EntityFrameworkCore;
using OpenAI.Chat;
using SaaSForge.Api.Data;
using SaaSForge.Api.DTOs.Ai;
using SaaSForge.Api.Models;
using SaaSForge.Api.Services.Usage;

namespace SaaSForge.Api.Services.Ai
{
    public class AiService : IAiService
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly IUsageService _usageService;

        public AiService(AppDbContext context, IConfiguration configuration, IUsageService usageService)
        {
            _context = context;
            _configuration = configuration;
            _usageService = usageService;
        }

        public async Task<AskAiResponseDto> AskAsync(string ownerUserId, AskAiRequestDto dto)
        {
            var business = await _context.Businesses
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.OwnerUserId == ownerUserId);

            if (business == null)
            {
                throw new InvalidOperationException("Business not found for the current user.");
            }

            // ✅ ADD HERE
            await _usageService.EnsureSubscriptionStateAsync(business.Id);

            await _usageService.EnsureCanUseAiAsync(business.Id);

            var apiKey = _configuration["OpenAI:ApiKey"];
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                throw new InvalidOperationException("OpenAI API key is not configured.");
            }

            var model = _configuration["OpenAI:Model"] ?? "gpt-4o-mini";

            var finalSystemPrompt = BuildSystemPrompt(business, dto);

            var client = new ChatClient(model: model, apiKey: apiKey);

            var messages = new List<ChatMessage>
            {
                ChatMessage.CreateSystemMessage(finalSystemPrompt),
                ChatMessage.CreateUserMessage(dto.Prompt)
            };

            var response = await client.CompleteChatAsync(messages);
            var aiText = response.Value.Content.FirstOrDefault()?.Text?.Trim();

            if (string.IsNullOrWhiteSpace(aiText))
            {
                aiText = "No response generated.";
            }

            var entity = new AiConversation
            {
                BusinessId = business.Id,
                FeatureType = dto.FeatureType?.Trim() ?? string.Empty,
                Prompt = dto.Prompt?.Trim() ?? string.Empty,
                SystemPrompt = string.IsNullOrWhiteSpace(dto.SystemPrompt) ? null : dto.SystemPrompt.Trim(),
                InputContextJson = string.IsNullOrWhiteSpace(dto.InputContextJson) ? null : dto.InputContextJson,
                Response = aiText,
                Model = model,
                CreatedAtUtc = DateTime.UtcNow
            };

            _context.AiConversations.Add(entity);
            await _context.SaveChangesAsync();

            await _usageService.IncrementAiUsageAsync(business.Id);

            var usage = await _usageService.GetOrCreateUsageAsync(business.Id);

            var remaining = usage.AiRequestLimit - usage.AiRequestsUsed;
            if (remaining < 0) remaining = 0;

            return new AskAiResponseDto
            {
                Id = entity.Id,
                BusinessId = entity.BusinessId,
                FeatureType = entity.FeatureType,
                Prompt = entity.Prompt,
                SystemPrompt = entity.SystemPrompt,
                InputContextJson = entity.InputContextJson,
                Response = entity.Response,
                Model = entity.Model,
                CreatedAtUtc = entity.CreatedAtUtc,
                Remaining = remaining // ✅ ADD THIS
            };
        }

        public async Task<List<AiConversationHistoryDto>> GetHistoryAsync(string ownerUserId, int take = 50)
        {
            var business = await _context.Businesses
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.OwnerUserId == ownerUserId);

            if (business == null)
            {
                throw new InvalidOperationException("Business not found for the current user.");
            }

            take = Math.Clamp(take, 1, 100);

            return await _context.AiConversations
                .AsNoTracking()
                .Where(x => x.BusinessId == business.Id)
                .OrderByDescending(x => x.CreatedAtUtc)
                .Take(take)
                .Select(x => new AiConversationHistoryDto
                {
                    Id = x.Id,
                    FeatureType = x.FeatureType,
                    Prompt = x.Prompt,
                    Response = x.Response,
                    Model = x.Model,
                    CreatedAtUtc = x.CreatedAtUtc
                })
                .ToListAsync();
        }

        private static string BuildSystemPrompt(SaaSForge.Api.Models.Business business, AskAiRequestDto dto)
        {
            var sb = new StringBuilder();

            sb.AppendLine("You are an AI assistant working for a business.");
            sb.AppendLine("Give clear, helpful, and concise answers.");
            sb.AppendLine("Do not invent facts when context is missing.");
            sb.AppendLine();

            sb.AppendLine($"Business Name: {business.Name}");
            sb.AppendLine($"Business Slug: {business.Slug}");

            if (!string.IsNullOrWhiteSpace(business.Email))
                sb.AppendLine($"Business Email: {business.Email}");

            if (!string.IsNullOrWhiteSpace(business.Phone))
                sb.AppendLine($"Business Phone: {business.Phone}");

            if (!string.IsNullOrWhiteSpace(business.Address))
                sb.AppendLine($"Business Address: {business.Address}");

            if (!string.IsNullOrWhiteSpace(business.TimeZone))
                sb.AppendLine($"Business Time Zone: {business.TimeZone}");

            sb.AppendLine();
            sb.AppendLine($"Feature Type: {dto.FeatureType}");

            if (!string.IsNullOrWhiteSpace(dto.SystemPrompt))
            {
                sb.AppendLine();
                sb.AppendLine("Additional Instructions:");
                sb.AppendLine(dto.SystemPrompt.Trim());
            }

            if (!string.IsNullOrWhiteSpace(dto.InputContextJson))
            {
                sb.AppendLine();
                sb.AppendLine("Structured Input Context:");
                sb.AppendLine(dto.InputContextJson.Trim());
            }

            return sb.ToString();
        }
    }
}