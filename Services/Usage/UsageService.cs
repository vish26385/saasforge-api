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

        public async Task<BusinessUsageResponseDto> GetMyUsageAsync(string ownerUserId)
        {
            var business = await _context.Businesses
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.OwnerUserId == ownerUserId);

            if (business == null)
            {
                throw new InvalidOperationException("Business not found for the current user.");
            }

            var usage = await GetOrCreateUsageAsync(business.Id);
            usage = await EnsurePeriodIsCurrentAsync(usage);

            return MapToDto(usage);
        }

        public async Task EnsureCanUseAiAsync(int businessId)
        {
            await EnsureSubscriptionIsActiveAsync(businessId);

            var usage = await GetOrCreateUsageAsync(businessId);
            usage = await EnsurePeriodIsCurrentAsync(usage);

            if (usage.AiRequestsUsed >= usage.AiRequestLimit)
            {
                throw new InvalidOperationException("AI request limit reached for the current billing period.");
            }
        }

        private async Task EnsureSubscriptionIsActiveAsync(int businessId)
        {
            var subscription = await _subscriptionService.GetOrCreateSubscriptionAsync(businessId);

            if (!string.Equals(subscription.Status, "active", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Subscription is not active.");
            }

            if (subscription.EndDateUtc.HasValue && subscription.EndDateUtc.Value <= DateTime.UtcNow)
            {
                throw new InvalidOperationException("Subscription has expired.");
            }
        }

        public async Task IncrementAiUsageAsync(int businessId)
        {
            var usage = await GetOrCreateUsageAsync(businessId);
            usage = await EnsurePeriodIsCurrentAsync(usage);

            usage.AiRequestsUsed += 1;
            usage.LastUpdatedAtUtc = DateTime.UtcNow;

            await _context.SaveChangesAsync();
        }

        public async Task<BusinessUsage> GetOrCreateUsageAsync(int businessId)
        {
            var usage = await _context.BusinessUsages
                .FirstOrDefaultAsync(x => x.BusinessId == businessId);

            if (usage != null)
            {
                return usage;
            }

            var freePlan = await _context.SubscriptionPlans
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Code == "free" && x.IsActive);

            if (freePlan == null)
            {
                throw new InvalidOperationException("Default free plan is not configured.");
            }

            var subscription = await _subscriptionService.GetOrCreateSubscriptionAsync(businessId);
            var nowUtc = DateTime.UtcNow;
            var currentPeriodStartUtc = GetCurrentPeriodStartUtc(subscription.StartDateUtc, nowUtc);

            usage = new BusinessUsage
            {
                BusinessId = businessId,
                PlanCode = freePlan.Code,
                CurrentPeriodStartUtc = currentPeriodStartUtc,
                AiRequestsUsed = 0,
                AiRequestLimit = freePlan.MonthlyAiRequestLimit,
                LastUpdatedAtUtc = nowUtc
            };

            _context.BusinessUsages.Add(usage);
            await _context.SaveChangesAsync();

            return usage;
        }

        private async Task<BusinessUsage> EnsurePeriodIsCurrentAsync(BusinessUsage usage)
        {
            var subscription = await _subscriptionService.GetOrCreateSubscriptionAsync(usage.BusinessId);
            var nowUtc = DateTime.UtcNow;
            var expectedPeriodStartUtc = GetCurrentPeriodStartUtc(subscription.StartDateUtc, nowUtc);

            if (usage.CurrentPeriodStartUtc == expectedPeriodStartUtc)
            {
                return usage;
            }

            var plan = await _context.SubscriptionPlans
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Code == usage.PlanCode && x.IsActive);

            if (plan == null)
            {
                throw new InvalidOperationException($"Active plan '{usage.PlanCode}' not found.");
            }

            usage.CurrentPeriodStartUtc = expectedPeriodStartUtc;
            //usage.AiRequestsUsed = 0;
            usage.AiRequestLimit = plan.MonthlyAiRequestLimit;
            usage.LastUpdatedAtUtc = nowUtc;

            await _context.SaveChangesAsync();

            return usage;
        }

        private static DateTime GetCurrentPeriodStartUtc(DateTime subscriptionStartUtc, DateTime nowUtc)
        {
            var anchorDay = subscriptionStartUtc.Day;

            var candidateDay = Math.Min(anchorDay, DateTime.DaysInMonth(nowUtc.Year, nowUtc.Month));
            var candidateStart = new DateTime(
                nowUtc.Year,
                nowUtc.Month,
                candidateDay,
                subscriptionStartUtc.Hour,
                subscriptionStartUtc.Minute,
                subscriptionStartUtc.Second,
                DateTimeKind.Utc);

            if (nowUtc >= candidateStart)
            {
                return candidateStart;
            }

            var previousMonth = nowUtc.AddMonths(-1);
            var previousDay = Math.Min(anchorDay, DateTime.DaysInMonth(previousMonth.Year, previousMonth.Month));

            return new DateTime(
                previousMonth.Year,
                previousMonth.Month,
                previousDay,
                subscriptionStartUtc.Hour,
                subscriptionStartUtc.Minute,
                subscriptionStartUtc.Second,
                DateTimeKind.Utc);
        }

        private static DateTime GetNextPeriodStartUtc(DateTime currentPeriodStartUtc)
        {
            var nextMonth = currentPeriodStartUtc.AddMonths(1);
            return new DateTime(
                nextMonth.Year,
                nextMonth.Month,
                Math.Min(currentPeriodStartUtc.Day, DateTime.DaysInMonth(nextMonth.Year, nextMonth.Month)),
                currentPeriodStartUtc.Hour,
                currentPeriodStartUtc.Minute,
                currentPeriodStartUtc.Second,
                DateTimeKind.Utc);
        }

        private static BusinessUsageResponseDto MapToDto(BusinessUsage usage)
        {
            return new BusinessUsageResponseDto
            {
                BusinessId = usage.BusinessId,
                PlanCode = usage.PlanCode,
                CurrentPeriodStartUtc = usage.CurrentPeriodStartUtc,
                AiRequestsUsed = usage.AiRequestsUsed,
                AiRequestLimit = usage.AiRequestLimit,
                RemainingAiRequests = Math.Max(usage.AiRequestLimit - usage.AiRequestsUsed, 0),
                LastUpdatedAtUtc = usage.LastUpdatedAtUtc
            };
        }
    }
}