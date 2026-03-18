using SaaSForge.Api.DTOs.Subscription;
using SaaSForge.Api.Models;

namespace SaaSForge.Api.Services.Subscription
{
    public interface ISubscriptionService
    {
        Task<SubscriptionResponseDto> GetMySubscriptionAsync(string ownerUserId);
        Task<BusinessSubscription> GetOrCreateSubscriptionAsync(int businessId);
        Task<SubscriptionResponseDto> ChangePlanAsync(string ownerUserId, string planCode);
    }
}