namespace SaaSForge.Api.DTOs.Usage
{
    public class UsageResponseDto
    {
        public string Plan { get; set; } = string.Empty;
        public int Used { get; set; }
        public int Limit { get; set; }
        public string Type { get; set; } = string.Empty;

        public DateTime? CurrentPeriodStartUtc { get; set; }
        public DateTime? CurrentPeriodEndUtc { get; set; }
    }
}
