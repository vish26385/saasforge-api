using SaaSForge.Api.DTOs.Usage;
using SaaSForge.Api.Models;

namespace SaaSForge.Api.Services.Usage
{
    public interface IUsageService
    {
        Task<UsageResponseDto> GetMyUsageAsync(string ownerUserId);
        Task EnsureCanUseAiAsync(int businessId);
        Task IncrementAiUsageAsync(int businessId);
        Task<BusinessUsage> GetOrCreateUsageAsync(int businessId);
    }
}