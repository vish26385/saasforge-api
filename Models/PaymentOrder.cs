namespace SaaSForge.Api.Models
{
    public class PaymentOrder
    {
        public int Id { get; set; }

        public int BusinessId { get; set; }

        // Razorpay Order Id (created from your backend)
        public string ProviderOrderId { get; set; } = string.Empty;

        // Razorpay Payment Id (after payment)
        public string? ProviderPaymentId { get; set; }

        // Payment info
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "INR";

        // Status: created | paid | failed
        public string Status { get; set; } = "created";

        // Failure reason (if any)
        public string? FailureReason { get; set; }

        // Plan info (IMPORTANT)
        public string PlanCode { get; set; } = "pro";

        // Audit
        public DateTime CreatedAtUtc { get; set; }
        public DateTime? PaidAtUtc { get; set; }
        public DateTime? UpdatedAtUtc { get; set; }
    }
}
