using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SaaSForge.Api.Data;
using SaaSForge.Api.DTOs.Subscription;
using SaaSForge.Api.Models;
using SaaSForge.Api.Models.Auth;
using SaaSForge.Api.Services.Common;
using SaaSForge.Api.Services.Usage;

namespace SaaSForge.Api.Services.Subscription
{
    public class SubscriptionService : ISubscriptionService
    {
        private readonly AppDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEmailService _emailService;
        private readonly IConfiguration _config;
        private readonly ILogger<SubscriptionService> _logger;


        public SubscriptionService(AppDbContext context,
                                   UserManager<ApplicationUser> userManager,
                                   IEmailService emailService,
                                   IConfiguration config,
                                   ILogger<SubscriptionService> logger)
        {
            _context = context;
            _userManager = userManager;
            _emailService = emailService;
            _config = config;
            _logger = logger;
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

            await EnsureSubscriptionStateAsync(business.Id);

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

        //public async Task<ChangePlanResultDto> ChangePlanAsync(string ownerUserId, string planCode)
        //{
        //    if (string.IsNullOrWhiteSpace(planCode))
        //    {
        //        throw new InvalidOperationException("Plan code is required.");
        //    }

        //    var normalizedPlanCode = planCode.Trim().ToLowerInvariant();

        //    var business = await _context.Businesses
        //        .AsNoTracking()
        //        .FirstOrDefaultAsync(x => x.OwnerUserId == ownerUserId);

        //    if (business == null)
        //    {
        //        throw new InvalidOperationException("Business not found for the current user.");
        //    }

        //    var plan = await _context.SubscriptionPlans
        //        .AsNoTracking()
        //        .FirstOrDefaultAsync(x => x.Code == normalizedPlanCode && x.IsActive);

        //    if (plan == null)
        //    {
        //        throw new InvalidOperationException("Requested plan does not exist or is inactive.");
        //    }

        //    var subscription = await GetOrCreateSubscriptionAsync(business.Id);
        //    var currentPlanCode = (subscription.PlanCode ?? string.Empty).Trim().ToLowerInvariant();

        //    // Idempotent no-op
        //    if (string.Equals(currentPlanCode, normalizedPlanCode, StringComparison.OrdinalIgnoreCase))
        //    {
        //        var message = normalizedPlanCode == "pro"
        //            ? "You are already on the Pro plan"
        //            : $"You are already on the {plan.Name} plan";

        //        return new ChangePlanResultDto
        //        {
        //            Changed = false,
        //            Message = message,
        //            Subscription = MapToDto(subscription)
        //        };
        //    }

        //    var nowUtc = DateTime.UtcNow;

        //    await using var transaction = await _context.Database.BeginTransactionAsync();

        //    subscription.PlanCode = plan.Code;
        //    subscription.Status = "active";

        //    // Only set subscription dates when there is an actual billing-cycle reason.
        //    // free -> pro is a real paid upgrade event, so dates are set once here.
        //    if (string.Equals(currentPlanCode, "free", StringComparison.OrdinalIgnoreCase) &&
        //        string.Equals(normalizedPlanCode, "pro", StringComparison.OrdinalIgnoreCase))
        //    {
        //        subscription.StartDateUtc = nowUtc;
        //        subscription.EndDateUtc = nowUtc.AddMonths(1);
        //    }
        //    else if (string.Equals(normalizedPlanCode, "free", StringComparison.OrdinalIgnoreCase))
        //    {
        //        subscription.EndDateUtc = null;
        //    }

        //    subscription.UpdatedAtUtc = nowUtc;

        //    var usage = await _context.BusinessUsages
        //        .FirstOrDefaultAsync(x => x.BusinessId == business.Id);

        //    if (usage != null)
        //    {
        //        usage.PlanCode = plan.Code;

        //        // Preserve AiRequestsUsed — do NOT reset to 0
        //        usage.AiRequestLimit = plan.MonthlyAiRequestLimit;

        //        // If this is the actual free -> pro upgrade, align usage cycle to the new paid cycle
        //        // but keep the already used count.
        //        if (string.Equals(currentPlanCode, "free", StringComparison.OrdinalIgnoreCase) &&
        //            string.Equals(normalizedPlanCode, "pro", StringComparison.OrdinalIgnoreCase))
        //        {
        //            usage.CurrentPeriodStartUtc = subscription.StartDateUtc;
        //        }

        //        usage.LastUpdatedAtUtc = nowUtc;
        //    }

        //    await _context.SaveChangesAsync();
        //    await transaction.CommitAsync();

        //    return new ChangePlanResultDto
        //    {
        //        Changed = true,
        //        Message = string.Equals(normalizedPlanCode, "pro", StringComparison.OrdinalIgnoreCase)
        //            ? "Plan upgraded to Pro successfully."
        //            : $"Plan changed to {plan.Name} successfully.",
        //        Subscription = MapToDto(subscription)
        //    };
        //}

        public async Task<ChangePlanResultDto> ChangePlanAsync(string ownerUserId, string planCode)
        {
            if (string.IsNullOrWhiteSpace(planCode))
            {
                throw new InvalidOperationException("Plan code is required.");
            }

            var normalizedPlanCode = planCode.Trim().ToLowerInvariant();

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
            var currentPlanCode = (subscription.PlanCode ?? string.Empty).Trim().ToLowerInvariant();

            // Idempotent no-op
            if (string.Equals(currentPlanCode, normalizedPlanCode, StringComparison.OrdinalIgnoreCase))
            {
                var message = normalizedPlanCode == "pro"
                    ? "You are already on the Pro plan"
                    : $"You are already on the {plan.Name} plan";

                return new ChangePlanResultDto
                {
                    Changed = false,
                    Message = message,
                    Subscription = MapToDto(subscription)
                };
            }

            var nowUtc = DateTime.UtcNow;
            var isFreeToProUpgrade =
                string.Equals(currentPlanCode, "free", StringComparison.OrdinalIgnoreCase) &&
                string.Equals(normalizedPlanCode, "pro", StringComparison.OrdinalIgnoreCase);

            await using var transaction = await _context.Database.BeginTransactionAsync();

            subscription.PlanCode = plan.Code;
            subscription.Status = "active";

            // Only set subscription dates when there is an actual billing-cycle reason.
            // free -> pro is a real paid upgrade event, so dates are set once here.
            if (isFreeToProUpgrade)
            {
                subscription.StartDateUtc = nowUtc;
                subscription.EndDateUtc = nowUtc.AddMonths(1);
            }
            else if (string.Equals(normalizedPlanCode, "free", StringComparison.OrdinalIgnoreCase))
            {
                subscription.EndDateUtc = null;
            }

            subscription.UpdatedAtUtc = nowUtc;

            var usage = await _context.BusinessUsages
                .FirstOrDefaultAsync(x => x.BusinessId == business.Id);

            if (usage != null)
            {
                usage.PlanCode = plan.Code;

                // Preserve AiRequestsUsed — do NOT reset to 0
                usage.AiRequestLimit = plan.MonthlyAiRequestLimit;

                // If this is the actual free -> pro upgrade, align usage cycle to the new paid cycle
                // but keep the already used count.
                if (isFreeToProUpgrade)
                {
                    usage.CurrentPeriodStartUtc = subscription.StartDateUtc;
                }

                usage.LastUpdatedAtUtc = nowUtc;
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            // 🔥 Send plan activation notification email only after successful commit
            if (string.Equals(normalizedPlanCode, "pro", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    var user = await _userManager.FindByIdAsync(ownerUserId);

                    if (user != null && !string.IsNullOrWhiteSpace(user.Email))
                    {
                        var frontendBaseUrl = _config["ClientApp:BaseUrl"];

                        if (!string.IsNullOrWhiteSpace(frontendBaseUrl))
                        {
                            await _emailService.SendNotificationEmailAsync(
                                user.Email,
                                "Your Pro plan is now active - LeadFlow AI",
                                "Your Pro plan is active",
                                "Your subscription has been activated successfully. You can now access your upgraded LeadFlow AI features.",
                                $"{frontendBaseUrl.TrimEnd('/')}/billing",
                                "Open Billing");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        "Failed to send subscription activation email for ownerUserId {OwnerUserId}",
                        ownerUserId);
                }
            }

            return new ChangePlanResultDto
            {
                Changed = true,
                Message = string.Equals(normalizedPlanCode, "pro", StringComparison.OrdinalIgnoreCase)
                    ? "Plan upgraded to Pro successfully."
                    : $"Plan changed to {plan.Name} successfully.",
                Subscription = MapToDto(subscription)
            };
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

        private async Task EnsureSubscriptionStateAsync(int businessId)
        {
            var now = DateTime.UtcNow;

            var subscription = await _context.BusinessSubscriptions
                .FirstOrDefaultAsync(x => x.BusinessId == businessId);

            if (subscription == null)
                return;

            var usage = await _context.BusinessUsages
                  .FirstOrDefaultAsync(x => x.BusinessId == businessId);

            if (subscription.PlanCode == "pro" &&
                subscription.Status == "active" &&
                subscription.EndDateUtc.HasValue &&
                subscription.EndDateUtc.Value <= now &&
                usage != null &&
                usage.AiRequestsUsed <= 50)
            {
                subscription.PlanCode = "free";
                subscription.Status = "active";
                subscription.UpdatedAtUtc = now;

                if (usage != null)
                {
                    usage.PlanCode = "free";
                    usage.AiRequestLimit = 50;
                    usage.LastUpdatedAtUtc = now;
                }

                await _context.SaveChangesAsync();
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

                await _context.SaveChangesAsync();
            }
        }
    }
}