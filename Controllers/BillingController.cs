using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Razorpay.Api;
using SaaSForge.Api.Data;
using SaaSForge.Api.DTOs;
using SaaSForge.Api.Models;
using SaaSForge.Api.Services.Interfaces;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

[Authorize]
[ApiController]
[Route("api/billing")]
public class BillingController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly IPaymentActivationService _paymentActivationService;

    public BillingController(AppDbContext context, IConfiguration configuration, IPaymentActivationService paymentActivationService)
    {
        _context = context;
        _configuration = configuration;
        _paymentActivationService = paymentActivationService;
    }

    // ===============================
    // ✅ CREATE ORDER
    // ===============================
    [HttpPost("create-order")]
    public async Task<IActionResult> CreateOrder()
    {
        var userId = GetUserId();

        var business = await _context.Businesses
            .FirstOrDefaultAsync(x => x.OwnerUserId == userId);

        if (business == null)
            return BadRequest(new { message = "Business not found." });

        var now = DateTime.UtcNow;

        // Check if already active PRO
        var activePro = await _context.BusinessSubscriptions
            .Where(x =>
                x.BusinessId == business.Id &&
                x.PlanCode == "pro" &&
                x.Status == "active" &&
                x.EndDateUtc != null &&
                x.EndDateUtc > now)
            .FirstOrDefaultAsync();

        if (activePro != null)
        {
            return BadRequest(new { message = "Pro plan is already active." });
        }

        var keyId = _configuration["Razorpay:KeyId"]!;
        var keySecret = _configuration["Razorpay:KeySecret"]!;
        var amount = int.Parse(_configuration["Razorpay:ProPlanAmountInPaise"]!);
        var currency = _configuration["Razorpay:Currency"] ?? "INR";

        var client = new RazorpayClient(keyId, keySecret);

        var options = new Dictionary<string, object>
        {
            { "amount", amount },
            { "currency", currency },
            { "receipt", $"biz_{business.Id}_{Guid.NewGuid():N}" },
            { "payment_capture", 1 }
        };

        var order = client.Order.Create(options);

        var paymentOrder = new PaymentOrder
        {
            BusinessId = business.Id,
            ProviderOrderId = order["id"].ToString()!,
            Amount = amount / 100m,
            Currency = currency,
            Status = "created",
            PlanCode = "pro",
            CreatedAtUtc = DateTime.UtcNow,
            UpdatedAtUtc = DateTime.UtcNow
        };

        _context.PaymentOrders.Add(paymentOrder);
        await _context.SaveChangesAsync();

        return Ok(new
        {
            orderId = order["id"].ToString(),
            amount = order["amount"],
            currency = currency,
            key = keyId
        });
    }   

    // ===============================
    // 🔐 SIGNATURE VERIFICATION
    // ===============================

    // ===============================
    // ✅ VERIFY PAYMENT
    // ===============================
    [HttpPost("verify-payment")]
    public async Task<IActionResult> VerifyPayment([FromBody] VerifyPaymentRequestDto dto)
    {
        var userId = GetUserId();

        var business = await _context.Businesses
            .FirstOrDefaultAsync(x => x.OwnerUserId == userId);

        if (business == null)
            return BadRequest(new { message = "Business not found." });

        var keySecret = _configuration["Razorpay:KeySecret"]!;

        // ✅ STEP 1: VERIFY SIGNATURE
        var isValid = VerifyRazorpaySignature(
            dto.RazorpayOrderId,
            dto.RazorpayPaymentId,
            dto.RazorpaySignature,
            keySecret
        );

        if (!isValid)
        {
            return BadRequest(new { message = "Invalid payment signature." });
        }

        // ✅ STEP 2: DUPLICATE PAYMENT CHECK
        var existing = await _context.BusinessSubscriptions
            .FirstOrDefaultAsync(x => x.ProviderPaymentId == dto.RazorpayPaymentId);

        if (existing != null)
        {
            return Ok(new
            {
                success = true,
                planCode = existing.PlanCode,
                startDateUtc = existing.StartDateUtc,
                endDateUtc = existing.EndDateUtc
            });
        }

        // ✅ STEP 3: FIND PAYMENT ORDER
        var order = await _context.PaymentOrders
            .FirstOrDefaultAsync(x => x.ProviderOrderId == dto.RazorpayOrderId);

        if (order == null)
        {
            return BadRequest(new { message = "Payment order not found." });
        }

        // ✅ STEP 4: IF ALREADY PAID, DO NOT PROCESS AGAIN
        if (order.Status == "paid")
        {
            var existingSubscription = await _context.BusinessSubscriptions
                .FirstOrDefaultAsync(x => x.BusinessId == order.BusinessId);

            return Ok(new
            {
                success = true,
                message = "Payment already processed.",
                planCode = existingSubscription?.PlanCode,
                startDateUtc = existingSubscription?.StartDateUtc,
                endDateUtc = existingSubscription?.EndDateUtc
            });
        }

        // ✅ STEP 5: MARK PAYMENT ORDER AS PAID
        order.Status = "paid";
        order.ProviderPaymentId = dto.RazorpayPaymentId;
        order.PaidAtUtc = DateTime.UtcNow;
        order.UpdatedAtUtc = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        // ✅ STEP 6: ACTIVATE / RENEW SUBSCRIPTION USING SHARED SERVICE
        await _paymentActivationService.ActivateSubscriptionFromOrderAsync(order);

        // ✅ STEP 7: RETURN UPDATED SUBSCRIPTION
        var subscription = await _context.BusinessSubscriptions
            .FirstOrDefaultAsync(x => x.BusinessId == order.BusinessId);

        return Ok(new
        {
            success = true,
            planCode = subscription?.PlanCode,
            startDateUtc = subscription?.StartDateUtc,
            endDateUtc = subscription?.EndDateUtc
        });
    }

    private static bool VerifyRazorpaySignature(
        string orderId,
        string paymentId,
        string razorpaySignature,
        string keySecret)
    {
        var payload = $"{orderId}|{paymentId}";

        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(keySecret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));

        var generatedSignature = BitConverter
            .ToString(hash)
            .Replace("-", "")
            .ToLower();

        return generatedSignature == razorpaySignature;
    }

    // ===============================
    // 🔑 GET USER ID
    // ===============================
    private string GetUserId()
    {
        return User.FindFirst("sub")?.Value
            ?? User.FindFirst(ClaimTypes.NameIdentifier)?.Value
            ?? throw new UnauthorizedAccessException();
    }
}