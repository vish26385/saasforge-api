using Google.Apis.Auth;

namespace SaaSForge.Api.Services.Auth
{
    public class GoogleTokenValidatorService
    {
        private readonly IConfiguration _config;

        public GoogleTokenValidatorService(IConfiguration config)
        {
            _config = config;
        }

        public async Task<GoogleJsonWebSignature.Payload> ValidateAsync(string idToken)
        {
            var clientId = _config["Authentication:Google:ClientId"];

            if (string.IsNullOrWhiteSpace(clientId))
            {
                throw new InvalidOperationException("Google ClientId is not configured.");
            }

            var settings = new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = new[] { clientId }
            };

            return await GoogleJsonWebSignature.ValidateAsync(idToken, settings);
        }
    }
}