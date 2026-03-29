using Microsoft.EntityFrameworkCore;
using SaaSForge.Api.Data;
using SaaSForge.Api.Models;
using SaaSForge.Api.Services.Interfaces;

public class PaymentActivationService : IPaymentActivationService
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _configuration;

    public PaymentActivationService(AppDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
    }

    public async Task ActivateSubscriptionFromOrderAsync(PaymentOrder order)
    {
        var now = DateTime.UtcNow;

        var sub = await _context.BusinessSubscriptions
            .FirstOrDefaultAsync(x => x.BusinessId == order.BusinessId);

        var usage = await _context.BusinessUsages
            .FirstOrDefaultAsync(x => x.BusinessId == order.BusinessId);

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
            }
            else
            {
                // expired/free -> fresh 30 days
                sub.StartDateUtc = now;
                sub.EndDateUtc = now.AddDays(30);
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
    }
}