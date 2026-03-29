namespace SaaSForge.Api.Models
{
    public class BusinessSubscription
    {
        public long Id { get; set; }

        public int BusinessId { get; set; }
        public Business Business { get; set; } = null!;

        public string PlanCode { get; set; } = "free";
        public string Status { get; set; } = "active";

        public DateTime StartDateUtc { get; set; }
        public DateTime? EndDateUtc { get; set; }

        public string? PaymentProvider { get; set; }          // "razorpay"
        public string? ProviderOrderId { get; set; }          // Razorpay order id
        public string? ProviderPaymentId { get; set; }        // Razorpay payment id
        public decimal? AmountPaid { get; set; }              // 499, 999, etc
        public string? Currency { get; set; }                 // "INR"


        public DateTime CreatedAtUtc { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
    }
}
