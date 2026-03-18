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

        public DateTime CreatedAtUtc { get; set; }
        public DateTime UpdatedAtUtc { get; set; }
    }
}
