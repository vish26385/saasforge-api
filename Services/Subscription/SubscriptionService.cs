using Microsoft.EntityFrameworkCore;
using SaaSForge.Api.Data;
using SaaSForge.Api.DTOs.Subscription;
using SaaSForge.Api.Models;

namespace SaaSForge.Api.Services.Subscription
{
    public class SubscriptionService : ISubscriptionService
    {
        private readonly AppDbContext _context;

        public SubscriptionService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<SubscriptionResponseDto> GetMySubscriptionAsync(string ownerUserId)
        {
            var business = await _context.Businesses
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.OwnerUserId == ownerUserId);

            if (business == null)
            {
                throw new InvalidOperationException("Business not found for the current user.");
            }

            var subscription = await GetOrCreateSubscriptionAsync(business.Id);
            return MapToDto(subscription);
        }

        public async Task<BusinessSubscription> GetOrCreateSubscriptionAsync(int businessId)
        {
            var subscription = await _context.BusinessSubscriptions
                .FirstOrDefaultAsync(x => x.BusinessId == businessId);

            if (subscription != null)
            {
                return subscription;
            }

            var freePlan = await _context.SubscriptionPlans
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Code == "free" && x.IsActive);

            if (freePlan == null)
            {
                throw new InvalidOperationException("Default free plan is not configured.");
            }

            subscription = new BusinessSubscription
            {
                BusinessId = businessId,
                PlanCode = freePlan.Code,
                Status = "active",
                StartDateUtc = DateTime.UtcNow,
                EndDateUtc = null,
                CreatedAtUtc = DateTime.UtcNow,
                UpdatedAtUtc = DateTime.UtcNow
            };

            _context.BusinessSubscriptions.Add(subscription);
            await _context.SaveChangesAsync();

            return subscription;
        }

        public async Task<SubscriptionResponseDto> ChangePlanAsync(string ownerUserId, string planCode)
        {
            if (string.IsNullOrWhiteSpace(planCode))
            {
                throw new InvalidOperationException("Plan code is required.");
            }

            var normalizedPlanCode = planCode.Trim().ToLower();

            var business = await _context.Businesses
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.OwnerUserId == ownerUserId);

            if (business == null)
            {
                throw new InvalidOperationException("Business not found for the current user.");
            }

            var plan = await _context.SubscriptionPlans
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Code == normalizedPlanCode && x.IsActive);

            if (plan == null)
            {
                throw new InvalidOperationException("Requested plan does not exist or is inactive.");
            }

            var subscription = await GetOrCreateSubscriptionAsync(business.Id);

            subscription.PlanCode = plan.Code;
            subscription.Status = "active";
            subscription.EndDateUtc = null;
            subscription.UpdatedAtUtc = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            var usage = await _context.BusinessUsages
                .FirstOrDefaultAsync(x => x.BusinessId == business.Id);

            if (usage != null)
            {
                usage.PlanCode = plan.Code;
                usage.AiRequestLimit = plan.MonthlyAiRequestLimit;
                usage.LastUpdatedAtUtc = DateTime.UtcNow;

                await _context.SaveChangesAsync();
            }

            return MapToDto(subscription);
        }

        private static SubscriptionResponseDto MapToDto(BusinessSubscription subscription)
        {
            return new SubscriptionResponseDto
            {
                BusinessId = subscription.BusinessId,
                PlanCode = subscription.PlanCode,
                Status = subscription.Status,
                StartDateUtc = subscription.StartDateUtc,
                EndDateUtc = subscription.EndDateUtc,
                CreatedAtUtc = subscription.CreatedAtUtc,
                UpdatedAtUtc = subscription.UpdatedAtUtc
            };
        }
    }
}