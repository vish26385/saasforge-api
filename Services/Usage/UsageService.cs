using Microsoft.EntityFrameworkCore;
using SaaSForge.Api.Data;
using SaaSForge.Api.DTOs.Usage;
using SaaSForge.Api.Models;

namespace SaaSForge.Api.Services.Usage
{
    public class UsageService : IUsageService
    {
        private readonly AppDbContext _context;

        public UsageService(AppDbContext context)
        {
            _context = context;
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
            var usage = await GetOrCreateUsageAsync(businessId);
            usage = await EnsurePeriodIsCurrentAsync(usage);

            if (usage.AiRequestsUsed >= usage.AiRequestLimit)
            {
                throw new InvalidOperationException("AI request limit reached for the current billing period.");
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

            usage = new BusinessUsage
            {
                BusinessId = businessId,
                PlanCode = freePlan.Code,
                CurrentPeriodStartUtc = GetCurrentPeriodStartUtc(),
                AiRequestsUsed = 0,
                AiRequestLimit = freePlan.MonthlyAiRequestLimit,
                LastUpdatedAtUtc = DateTime.UtcNow
            };

            _context.BusinessUsages.Add(usage);
            await _context.SaveChangesAsync();

            return usage;
        }

        private async Task<BusinessUsage> EnsurePeriodIsCurrentAsync(BusinessUsage usage)
        {
            var currentPeriodStart = GetCurrentPeriodStartUtc();

            if (usage.CurrentPeriodStartUtc >= currentPeriodStart)
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

            usage.CurrentPeriodStartUtc = currentPeriodStart;
            usage.AiRequestsUsed = 0;
            usage.AiRequestLimit = plan.MonthlyAiRequestLimit;
            usage.LastUpdatedAtUtc = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return usage;
        }

        private static DateTime GetCurrentPeriodStartUtc()
        {
            var now = DateTime.UtcNow;
            return new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
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