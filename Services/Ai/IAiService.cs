using SaaSForge.Api.DTOs.Ai;

namespace SaaSForge.Api.Services.Ai
{
    public interface IAiService
    {
        Task<AskAiResponseDto> AskAsync(string ownerUserId, AskAiRequestDto dto);
        Task<List<AiConversationHistoryDto>> GetHistoryAsync(string ownerUserId, int take = 50);
    }
}