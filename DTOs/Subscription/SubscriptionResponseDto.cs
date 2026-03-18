namespace SaaSForge.Api.DTOs.Subscription
{
    public class SubscriptionResponseDto
    {
        public int BusinessId { get; set; }
        public string PlanCode { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public DateTime StartDateUtc { get; set; }
        public DateTime? EndDateUtc { get; set; }
    }
}
