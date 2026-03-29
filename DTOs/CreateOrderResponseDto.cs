namespace SaaSForge.Api.DTOs
{
    public class CreateOrderResponseDto
    {
        public string OrderId { get; set; } = string.Empty;
        public string Key { get; set; } = string.Empty;
        public int Amount { get; set; }
        public string Currency { get; set; } = "INR";
    }
}
