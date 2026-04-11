using Google;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SaaSForge.Api.Data;
using SaaSForge.Api.Models;
using SaaSForge.Api.Models.Auth;
using SaaSForge.Api.Services.Common;
using SaaSForge.Api.Services.Interfaces;

public class PaymentActivationService : IPaymentActivationService
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEmailService _emailService;
    private readonly ILogger<PaymentActivationService> _logger;

    public PaymentActivationService(AppDbContext context, 
                                    IConfiguration configuration, 
                                    UserManager<ApplicationUser> userManager,
                                    IEmailService emailService,
                                    ILogger<PaymentActivationService> logger)
    {
        _context = context;
        _userManager = userManager;
        _emailService = emailService;
        _configuration = configuration;
        _logger = logger;
    }

    //public async Task ActivateSubscriptionFromOrderAsync(PaymentOrder order)
    //{
    //    var now = DateTime.UtcNow;

    //    var sub = await _context.BusinessSubscriptions
    //        .FirstOrDefaultAsync(x => x.BusinessId == order.BusinessId);

    //    var usage = await _context.BusinessUsages
    //        .FirstOrDefaultAsync(x => x.BusinessId == order.BusinessId);

    //    if (sub == null)
    //    {
    //        sub = new BusinessSubscription
    //        {
    //            BusinessId = order.BusinessId,
    //            PlanCode = "pro",
    //            Status = "active",
    //            StartDateUtc = now,
    //            EndDateUtc = now.AddDays(30),
    //            CreatedAtUtc = now,
    //            UpdatedAtUtc = now,
    //            PaymentProvider = "razorpay",
    //            ProviderOrderId = order.ProviderOrderId,
    //            ProviderPaymentId = order.ProviderPaymentId,
    //            AmountPaid = order.Amount,
    //            Currency = order.Currency
    //        };

    //        _context.BusinessSubscriptions.Add(sub);
    //    }
    //    else
    //    {
    //        var wasPro = sub.PlanCode == "pro";

    //        sub.PlanCode = "pro";
    //        sub.Status = "active";

    //        if (sub.EndDateUtc.HasValue && sub.EndDateUtc.Value > now)
    //        {
    //            // active pro renewed early -> extend
    //            sub.EndDateUtc = sub.EndDateUtc.Value.AddDays(30);
    //        }
    //        else
    //        {
    //            // expired/free -> fresh 30 days
    //            sub.StartDateUtc = now;
    //            sub.EndDateUtc = now.AddDays(30);
    //        }

    //        sub.PaymentProvider = "razorpay";
    //        sub.ProviderOrderId = order.ProviderOrderId;
    //        sub.ProviderPaymentId = order.ProviderPaymentId;
    //        sub.AmountPaid = order.Amount;
    //        sub.Currency = order.Currency;
    //        sub.UpdatedAtUtc = now;

    //        if (usage != null && wasPro)
    //        {
    //            // Pro -> Renew Pro => reset usage
    //            usage.AiRequestsUsed = 0;
    //        }
    //    }

    //    if (usage == null)
    //    {
    //        usage = new BusinessUsage
    //        {
    //            BusinessId = order.BusinessId,
    //            PlanCode = "pro",
    //            CurrentPeriodStartUtc = sub.StartDateUtc,
    //            AiRequestsUsed = 0,
    //            AiRequestLimit = 1000,
    //            LastUpdatedAtUtc = now
    //        };

    //        _context.BusinessUsages.Add(usage);
    //    }
    //    else
    //    {
    //        usage.PlanCode = "pro";
    //        usage.CurrentPeriodStartUtc = sub.StartDateUtc;
    //        usage.AiRequestLimit = 1000;
    //        usage.LastUpdatedAtUtc = now;
    //    }

    //    await _context.SaveChangesAsync();
    //}

    public async Task ActivateSubscriptionFromOrderAsync(PaymentOrder order)
    {
        var now = DateTime.UtcNow;

        var business = await _context.Businesses
            .FirstOrDefaultAsync(x => x.Id == order.BusinessId);

        if (business == null)
        {
            throw new InvalidOperationException("Business not found for payment order.");
        }

        var sub = await _context.BusinessSubscriptions
            .FirstOrDefaultAsync(x => x.BusinessId == order.BusinessId);

        var usage = await _context.BusinessUsages
            .FirstOrDefaultAsync(x => x.BusinessId == order.BusinessId);

        var isRenewal = false;

        if (sub == null)
        {
            sub = new BusinessSubscription
            {
                BusinessId = order.BusinessId,
                PlanCode = "pro",
                Status = "active",
                StartDateUtc = now,
                EndDateUtc = now.AddDays(30),
                CreatedAtUtc = now,
                UpdatedAtUtc = now,
                PaymentProvider = "razorpay",
                ProviderOrderId = order.ProviderOrderId,
                ProviderPaymentId = order.ProviderPaymentId,
                AmountPaid = order.Amount,
                Currency = order.Currency
            };

            _context.BusinessSubscriptions.Add(sub);
        }
        else
        {
            var wasPro = sub.PlanCode == "pro";

            sub.PlanCode = "pro";
            sub.Status = "active";

            if (sub.EndDateUtc.HasValue && sub.EndDateUtc.Value > now)
            {
                // active pro renewed early -> extend
                sub.EndDateUtc = sub.EndDateUtc.Value.AddDays(30);
                isRenewal = true;
            }
            else
            {
                // expired/free -> fresh 30 days
                sub.StartDateUtc = now;
                sub.EndDateUtc = now.AddDays(30);
                isRenewal = false;
            }

            sub.PaymentProvider = "razorpay";
            sub.ProviderOrderId = order.ProviderOrderId;
            sub.ProviderPaymentId = order.ProviderPaymentId;
            sub.AmountPaid = order.Amount;
            sub.Currency = order.Currency;
            sub.UpdatedAtUtc = now;

            if (usage != null && wasPro)
            {
                // Pro -> Renew Pro => reset usage
                usage.AiRequestsUsed = 0;
            }
        }

        if (usage == null)
        {
            usage = new BusinessUsage
            {
                BusinessId = order.BusinessId,
                PlanCode = "pro",
                CurrentPeriodStartUtc = sub.StartDateUtc,
                AiRequestsUsed = 0,
                AiRequestLimit = 1000,
                LastUpdatedAtUtc = now
            };

            _context.BusinessUsages.Add(usage);
        }
        else
        {
            usage.PlanCode = "pro";
            usage.CurrentPeriodStartUtc = sub.StartDateUtc;
            usage.AiRequestLimit = 1000;
            usage.LastUpdatedAtUtc = now;
        }

        await _context.SaveChangesAsync();

        // ✅ Send notification email only after successful save
        try
        {
            var ownerUser = await _userManager.FindByIdAsync(business.OwnerUserId);

            if (ownerUser != null && !string.IsNullOrWhiteSpace(ownerUser.Email))
            {
                var frontendBaseUrl = _configuration["ClientApp:BaseUrl"]?.TrimEnd('/');

                if (!string.IsNullOrWhiteSpace(frontendBaseUrl))
                {
                    var subject = isRenewal
                        ? "Your Pro plan has been renewed - LeadFlow AI"
                        : "Your Pro plan is now active - LeadFlow AI";

                    var heading = isRenewal
                        ? "Your Pro plan has been renewed"
                        : "Your Pro plan is active";

                    var message = isRenewal
                        ? $"Your Pro subscription has been renewed successfully. Your new expiry date is {sub.EndDateUtc:dd MMM yyyy}."
                        : $"Your subscription has been activated successfully. Your Pro plan is now active until {sub.EndDateUtc:dd MMM yyyy}.";

                    await _emailService.SendNotificationEmailAsync(
                        ownerUser.Email,
                        subject,
                        heading,
                        message,
                        $"{frontendBaseUrl}/billing",
                        "Open Billing");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Failed to send Pro activation/renewal notification email for BusinessId {BusinessId}",
                order.BusinessId);
        }
    }
}