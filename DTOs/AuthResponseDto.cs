namespace SaaSForge.Api.DTOs
{
    public class AuthResponseDto
    {
        public string Token { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public bool IsNewUser { get; set; }
        public AuthUserDto User { get; set; } = new();
    }

    public class AuthUserDto
    {
        public string Id { get; set; } = string.Empty;
        public string? Email { get; set; }
        public string? UserName { get; set; }
    }
}