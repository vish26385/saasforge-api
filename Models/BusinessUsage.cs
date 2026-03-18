namespace SaaSForge.Api.Models
{
    public class BusinessUsage
    {
        public long Id { get; set; }

        public int BusinessId { get; set; }
        public Business Business { get; set; } = null!;

        public string PlanCode { get; set; } = "free";

        public DateTime CurrentPeriodStartUtc { get; set; }

        public int AiRequestsUsed { get; set; }
        public int AiRequestLimit { get; set; }

        public DateTime LastUpdatedAtUtc { get; set; }
    }
}
