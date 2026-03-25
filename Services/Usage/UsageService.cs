using Microsoft.EntityFrameworkCore;
using SaaSForge.Api.Data;
using SaaSForge.Api.DTOs.Usage;
using SaaSForge.Api.Models;
using SaaSForge.Api.Services.Subscription;

namespace SaaSForge.Api.Services.Usage
{
    public class UsageService : IUsageService
    {
        private readonly AppDbContext _context;
        private readonly ISubscriptionService _subscriptionService;

        public UsageService(AppDbContext context, ISubscriptionService subscriptionService)
        {
            _context = context;
            _subscriptionService = subscriptionService;
        }

        // ============================
        // GET USAGE
        // ============================
        public async Task<UsageResponseDto> GetMyUsageAsync(string ownerUserId)
        {
            var business = await _context.Businesses
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.OwnerUserId == ownerUserId);

            if (business == null)
            {
                throw new InvalidOperationException("Business not found for the current user.");
            }

            var subscription = await _subscriptionService.GetOrCreateSubscriptionAsync(business.Id);
            var usage = await GetOrCreateUsageAsync(business.Id);

            if (IsPro(subscription.PlanCode))
            {
                subscription = await EnsureProCycleAsync(subscription, usage);
            }

            return MapToDto(usage, subscription);
        }

        // ============================
        // ENFORCEMENT
        // ============================
        public async Task EnsureCanUseAiAsync(int businessId)
        {
            var subscription = await _subscriptionService.GetOrCreateSubscriptionAsync(businessId);
            var usage = await GetOrCreateUsageAsync(businessId);

            if (IsPro(subscription.PlanCode))
            {
                subscription = await EnsureProCycleAsync(subscription, usage);

                if (usage.AiRequestsUsed >= 1000)
                {
                    throw new InvalidOperationException(
                        "You have reached your plan limit. It will reset at the end of your billing cycle.");
                }

                return;
            }

            // FREE PLAN (LIFETIME)
            if (usage.AiRequestsUsed >= 50)
            {
                throw new InvalidOperationException(
                    "You have reached the 50-response limit for the Free plan. Upgrade to Pro to continue.");
            }
        }

        // ============================
        // INCREMENT
        // ============================
        public async Task IncrementAiUsageAsync(int businessId)
        {
            var usage = await GetOrCreateUsageAsync(businessId);

            usage.AiRequestsUsed += 1;
            usage.LastUpdatedAtUtc = DateTime.UtcNow;

            await _context.SaveChangesAsync();
        }

        // ============================
        // CREATE / GET USAGE
        // ============================
        public async Task<BusinessUsage> GetOrCreateUsageAsync(int businessId)
        {
            var usage = await _context.BusinessUsages
                .FirstOrDefaultAsync(x => x.BusinessId == businessId);

            if (usage != null)
            {
                return usage;
            }

            var subscription = await _subscriptionService.GetOrCreateSubscriptionAsync(businessId);

            var plan = await _context.SubscriptionPlans
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Code == subscription.PlanCode && x.IsActive);

            if (plan == null)
            {
                throw new InvalidOperationException("Default plan is not configured.");
            }

            usage = new BusinessUsage
            {
                BusinessId = businessId,
                PlanCode = subscription.PlanCode,
                AiRequestsUsed = 0,
                AiRequestLimit = plan.MonthlyAiRequestLimit,
                CurrentPeriodStartUtc = subscription.StartDateUtc, // only relevant for pro
                LastUpdatedAtUtc = DateTime.UtcNow
            };

            _context.BusinessUsages.Add(usage);
            await _context.SaveChangesAsync();

            return usage;
        }

        // ============================
        // PRO ROLLING 30-DAY CYCLE
        // ============================
        private async Task<BusinessSubscription> EnsureProCycleAsync(
            BusinessSubscription subscription,
            BusinessUsage usage)
        {
            var now = DateTime.UtcNow;

            // Initialize if missing
            if (!subscription.EndDateUtc.HasValue)
            {
                subscription.StartDateUtc = now;
                subscription.EndDateUtc = now.AddDays(30);
                subscription.UpdatedAtUtc = now;

                usage.AiRequestsUsed = 0;
                usage.CurrentPeriodStartUtc = now;
                usage.AiRequestLimit = 1000;
                usage.LastUpdatedAtUtc = now;

                await _context.SaveChangesAsync();
                return subscription;
            }

            // If still valid → no reset
            if (now < subscription.EndDateUtc.Value)
            {
                return subscription;
            }

            // RESET (ROLLING 30 DAYS)
            subscription.StartDateUtc = now;
            subscription.EndDateUtc = now.AddDays(30);
            subscription.UpdatedAtUtc = now;

            usage.AiRequestsUsed = 0;
            usage.CurrentPeriodStartUtc = now;
            usage.AiRequestLimit = 1000;
            usage.LastUpdatedAtUtc = now;

            await _context.SaveChangesAsync();

            return subscription;
        }

        // ============================
        // HELPERS
        // ============================
        private static bool IsPro(string? planCode)
        {
            return string.Equals(planCode, "pro", StringComparison.OrdinalIgnoreCase);
        }

        private static UsageResponseDto MapToDto(
            BusinessUsage usage,
            BusinessSubscription subscription)
        {
            if (IsPro(subscription.PlanCode))
            {
                return new UsageResponseDto
                {
                    Plan = "pro",
                    Used = usage.AiRequestsUsed,
                    Limit = 1000,
                    Type = "subscription",
                    CurrentPeriodStartUtc = subscription.StartDateUtc,
                    CurrentPeriodEndUtc = subscription.EndDateUtc
                };
            }

            return new UsageResponseDto
            {
                Plan = "free",
                Used = usage.AiRequestsUsed,
                Limit = 50,
                Type = "lifetime",
                CurrentPeriodStartUtc = null,
                CurrentPeriodEndUtc = null
            };
        }
    }
}