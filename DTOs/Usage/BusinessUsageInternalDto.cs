namespace SaaSForge.Api.DTOs.Usage
{
    public class BusinessUsageInternalDto
    {
        public int BusinessId { get; set; }
        public string PlanCode { get; set; } = string.Empty;
        public DateTime CurrentPeriodStartUtc { get; set; }
        public int AiRequestsUsed { get; set; }
        public int AiRequestLimit { get; set; }
        public int RemainingAiRequests { get; set; }
        public DateTime LastUpdatedAtUtc { get; set; }
    }
}
