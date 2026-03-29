using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Org.BouncyCastle.Asn1.Ocsp;
using SaaSForge.Api.Data;
using SaaSForge.Api.Models;
using SaaSForge.Api.Services.Interfaces;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

[ApiController]
[Route("api/razorpay/webhook")]
public class RazorpayWebhookController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly IPaymentActivationService _paymentActivationService;

    public RazorpayWebhookController(AppDbContext context, IConfiguration configuration, IPaymentActivationService paymentActivationService)
    {
        _context = context;
        _configuration = configuration;
        _paymentActivationService = paymentActivationService;
    }

    [HttpPost]
    [AllowAnonymous]
    public async Task<IActionResult> Handle()
    {
        Request.EnableBuffering();

        string body;
        using (var reader = new StreamReader(Request.Body, Encoding.UTF8, leaveOpen: true))
        {
            body = await reader.ReadToEndAsync();
            Request.Body.Position = 0;
        }

        var signature = Request.Headers["X-Razorpay-Signature"].FirstOrDefault();
        var webhookSecret = _configuration["Razorpay:WebhookSecret"];

        if (string.IsNullOrWhiteSpace(signature) || string.IsNullOrWhiteSpace(webhookSecret))
            return Unauthorized();

        if (!IsValidWebhookSignature(body, signature, webhookSecret))
            return Unauthorized();

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;

        var eventType = root.GetProperty("event").GetString() ?? "";
        var eventId = root.GetProperty("payload").ToString() + "|" + eventType; // fallback if no top-level id

        var existingLog = await _context.Set<PaymentWebhookLog>()
            .FirstOrDefaultAsync(x => x.EventId == eventId);

        if (existingLog != null)
            return Ok();

        var log = new PaymentWebhookLog
        {
            EventId = eventId,
            EventType = eventType,
            PayloadJson = body,
            ReceivedAtUtc = DateTime.UtcNow,
            Processed = false
        };

        _context.Add(log);
        await _context.SaveChangesAsync();

        switch (eventType)
        {
            case "payment.captured":
            case "order.paid":
                await HandleSuccessfulPayment(root);
                break;

            case "payment.failed":
                await HandleFailedPayment(root);
                break;
        }

        log.Processed = true;
        log.ProcessedAtUtc = DateTime.UtcNow;
        await _context.SaveChangesAsync();

        return Ok();
    }

    private bool IsValidWebhookSignature(string payload, string actualSignature, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        var expectedSignature = BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
        return expectedSignature == actualSignature.ToLowerInvariant();
    }

    //private async Task HandleSuccessfulPayment(JsonElement root)
    //{
    //    var paymentEntity = root.GetProperty("payload").GetProperty("payment").GetProperty("entity");

    //    var paymentId = paymentEntity.GetProperty("id").GetString();
    //    var orderId = paymentEntity.GetProperty("order_id").GetString();
    //    var amount = paymentEntity.GetProperty("amount").GetInt32() / 100m;
    //    var currency = paymentEntity.GetProperty("currency").GetString();

    //    if (string.IsNullOrWhiteSpace(paymentId) || string.IsNullOrWhiteSpace(orderId))
    //        return;

    //    // already handled by verify-payment flow
    //    var existingSubByPayment = await _context.BusinessSubscriptions
    //        .FirstOrDefaultAsync(x => x.ProviderPaymentId == paymentId);

    //    if (existingSubByPayment != null)
    //        return;

    //    var order = await _context.PaymentOrders
    //        .FirstOrDefaultAsync(x => x.ProviderOrderId == orderId);

    //    if (order == null || order.Status == "paid")
    //        return;

    //    var now = DateTime.UtcNow;

    //    // mark payment order as paid
    //    order.Status = "paid";
    //    order.ProviderPaymentId = paymentId;
    //    order.PaidAtUtc = now;
    //    order.UpdatedAtUtc = now;

    //    // ===============================
    //    // ACTIVATE / RENEW SUBSCRIPTION
    //    // ===============================
    //    var sub = await _context.BusinessSubscriptions
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

    //        // Renewal logic
    //        if (sub.EndDateUtc.HasValue && sub.EndDateUtc.Value > now)
    //        {
    //            // active -> extend
    //            sub.EndDateUtc = sub.EndDateUtc.Value.AddDays(30);
    //        }
    //        else
    //        {
    //            // expired/free -> fresh
    //            sub.StartDateUtc = now;
    //            sub.EndDateUtc = now.AddDays(30);
    //        }

    //        sub.PaymentProvider = "razorpay";
    //        sub.ProviderOrderId = order.ProviderOrderId;
    //        sub.ProviderPaymentId = order.ProviderPaymentId;
    //        sub.AmountPaid = order.Amount;
    //        sub.Currency = order.Currency;
    //        sub.UpdatedAtUtc = now;

    //        // ===============================
    //        // UPDATE USAGE
    //        // ===============================
    //        var usage = await _context.BusinessUsages
    //            .FirstOrDefaultAsync(x => x.BusinessId == order.BusinessId);

    //        if (usage == null)
    //        {
    //            usage = new BusinessUsage
    //            {
    //                BusinessId = order.BusinessId,
    //                PlanCode = "pro",
    //                CurrentPeriodStartUtc = sub.StartDateUtc,
    //                AiRequestsUsed = 0,
    //                AiRequestLimit = 1000,
    //                LastUpdatedAtUtc = now
    //            };

    //            _context.BusinessUsages.Add(usage);
    //        }
    //        else
    //        {
    //            usage.PlanCode = "pro";
    //            usage.CurrentPeriodStartUtc = sub.StartDateUtc;
    //            usage.AiRequestLimit = 1000;
    //            usage.LastUpdatedAtUtc = now;

    //            // Free -> Pro => keep usage
    //            // Pro -> Renew => reset usage
    //            if (wasPro)
    //            {
    //                usage.AiRequestsUsed = 0;
    //            }
    //        }
    //    }

    //    await _context.SaveChangesAsync();
    //}

    private async Task HandleSuccessfulPayment(JsonElement root)
    {
        var paymentEntity = root.GetProperty("payload").GetProperty("payment").GetProperty("entity");

        var paymentId = paymentEntity.GetProperty("id").GetString();
        var orderId = paymentEntity.GetProperty("order_id").GetString();

        if (string.IsNullOrWhiteSpace(paymentId) || string.IsNullOrWhiteSpace(orderId))
            return;

        var existingSubByPayment = await _context.BusinessSubscriptions
            .FirstOrDefaultAsync(x => x.ProviderPaymentId == paymentId);

        if (existingSubByPayment != null)
            return;

        var order = await _context.PaymentOrders
            .FirstOrDefaultAsync(x => x.ProviderOrderId == orderId);

        if (order == null || order.Status == "paid")
            return;

        order.Status = "paid";
        order.ProviderPaymentId = paymentId;
        order.PaidAtUtc = DateTime.UtcNow;
        order.UpdatedAtUtc = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        await _paymentActivationService.ActivateSubscriptionFromOrderAsync(order);
    }

    private async Task HandleFailedPayment(JsonElement root)
    {
        var paymentEntity = root.GetProperty("payload").GetProperty("payment").GetProperty("entity");

        var paymentId = paymentEntity.GetProperty("id").GetString();
        var orderId = paymentEntity.GetProperty("order_id").GetString();

        string? failureReason = null;

        if (paymentEntity.TryGetProperty("error_description", out var errorDescription))
        {
            failureReason = errorDescription.GetString();
        }

        if (string.IsNullOrWhiteSpace(orderId))
            return;

        var order = await _context.PaymentOrders
            .FirstOrDefaultAsync(x => x.ProviderOrderId == orderId);

        if (order == null)
            return;

        order.Status = "failed";
        order.ProviderPaymentId = paymentId;
        order.FailureReason = failureReason;
        order.UpdatedAtUtc = DateTime.UtcNow;

        await _context.SaveChangesAsync();
    }
}