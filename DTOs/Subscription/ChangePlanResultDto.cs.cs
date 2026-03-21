namespace SaaSForge.Api.DTOs.Subscription
{
    public class ChangePlanResultDto
    {
        public bool Changed { get; set; }
        public string Message { get; set; } = string.Empty;
        public SubscriptionResponseDto Subscription { get; set; } = null!;
    }
}