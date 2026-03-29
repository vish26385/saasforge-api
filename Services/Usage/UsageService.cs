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

            // ✅ ADD HERE
            await EnsureSubscriptionStateAsync(business.Id);

            var subscription = await _subscriptionService.GetOrCreateSubscriptionAsync(business.Id);
            var usage = await GetOrCreateUsageAsync(business.Id);

            var isActivePro =
                subscription.PlanCode == "pro" &&
                subscription.Status == "active" &&
                subscription.EndDateUtc.HasValue &&
                subscription.EndDateUtc.Value > DateTime.UtcNow;

            //if (IsPro(subscription.PlanCode))
            if (isActivePro)
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

            var isActivePro =
                subscription.PlanCode == "pro" &&
                subscription.Status == "active" &&
                subscription.EndDateUtc.HasValue &&
                subscription.EndDateUtc.Value > DateTime.UtcNow;

            var isExpiredPro =
                subscription.PlanCode == "pro" &&
                subscription.Status == "expired";

            if (isActivePro)
            {
                if (usage.AiRequestsUsed >= 1000)
                    throw new InvalidOperationException("You have reached your Pro plan limit.");
            }
            else if (isExpiredPro && usage.AiRequestsUsed > 50)
            {
                throw new InvalidOperationException("Your Pro plan has expired. Please renew or upgrade again to continue.");
            }
            else
            {
                if (usage.AiRequestsUsed >= 50)
                    throw new InvalidOperationException("You have reached your 50 free replies. Upgrade to Pro to continue.");
            }

            //if (IsPro(subscription.PlanCode))
            if (isActivePro)
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
            await EnsureSubscriptionStateAsync(businessId);

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

        private static UsageResponseDto MapToDto(
            BusinessUsage usage,
            BusinessSubscription subscription)
        {
            var isActivePro =
                subscription.PlanCode == "pro" &&
                subscription.Status == "active" &&
                subscription.EndDateUtc.HasValue &&
                subscription.EndDateUtc.Value > DateTime.UtcNow;

            var isExpiredPro =
              subscription.PlanCode == "pro" &&
              subscription.Status == "expired";

            //if (IsPro(subscription.PlanCode))
            if (isActivePro)
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
            else if (isExpiredPro && usage.AiRequestsUsed > 50)
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
            else
            {
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

        public async Task EnsureSubscriptionStateAsync(int businessId)
        {
            var now = DateTime.UtcNow;

            var subscription = await _context.BusinessSubscriptions
                .FirstOrDefaultAsync(x => x.BusinessId == businessId);

            if (subscription == null)
                return;

            var usage = await _context.BusinessUsages
                .FirstOrDefaultAsync(x => x.BusinessId == businessId);

            var changed = false;

            // PRO expired -> downgrade to FREE
            if (subscription.PlanCode == "pro" &&
                subscription.Status == "active" &&
                subscription.EndDateUtc.HasValue &&
                subscription.EndDateUtc.Value <= now &&
                usage != null &&
                usage.AiRequestsUsed <= 50)
            {
                subscription.PlanCode = "free";
                subscription.Status = "active";
                subscription.PaymentProvider = subscription.PaymentProvider; // keep history fields as-is
                subscription.UpdatedAtUtc = now;
                changed = true;

                if (usage == null)
                {
                    usage = new BusinessUsage
                    {
                        BusinessId = businessId,
                        PlanCode = "free",
                        CurrentPeriodStartUtc = now,
                        AiRequestsUsed = 0,
                        AiRequestLimit = 50,
                        LastUpdatedAtUtc = now
                    };

                    _context.BusinessUsages.Add(usage);
                }
                else
                {
                    usage.PlanCode = "free";
                    usage.AiRequestLimit = 50;
                    usage.LastUpdatedAtUtc = now;
                }

                changed = true;
            }
            else if (subscription.PlanCode == "pro" &&
                     subscription.Status == "active" &&
                     subscription.EndDateUtc.HasValue &&
                     subscription.EndDateUtc.Value <= now &&
                     usage != null &&
                     usage.AiRequestsUsed > 50)
            {
                subscription.Status = "expired";
                subscription.UpdatedAtUtc = now;
                changed = true;
            }

            if (changed)
            {
                await _context.SaveChangesAsync();
            }
        }
    }
}