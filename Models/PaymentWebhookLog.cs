namespace SaaSForge.Api.Models
{
    public class PaymentWebhookLog
    {
        public long Id { get; set; }
        public string EventId { get; set; } = string.Empty;
        public string EventType { get; set; } = string.Empty;
        public string PayloadJson { get; set; } = string.Empty;
        public DateTime ReceivedAtUtc { get; set; }
        public bool Processed { get; set; }
        public DateTime? ProcessedAtUtc { get; set; }
    }
}
