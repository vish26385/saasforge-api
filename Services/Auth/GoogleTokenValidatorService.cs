//using Google.Apis.Auth;

//namespace SaaSForge.Api.Services.Auth
//{
//    public class GoogleTokenValidatorService
//    {
//        private readonly IConfiguration _config;

//        public GoogleTokenValidatorService(IConfiguration config)
//        {
//            _config = config;
//        }

//        public async Task<GoogleJsonWebSignature.Payload> ValidateAsync(string idToken)
//        {
//            var clientId = _config["Authentication:Google:ClientId"];

//            if (string.IsNullOrWhiteSpace(clientId))
//            {
//                throw new InvalidOperationException("Google ClientId is not configured.");
//            }

//            var settings = new GoogleJsonWebSignature.ValidationSettings
//            {
//                Audience = new[] { clientId }
//            };

//            return await GoogleJsonWebSignature.ValidateAsync(idToken, settings);
//        }
//    }
//}

using Google.Apis.Auth;
using System.Net.Http.Headers;
using System.Text.Json;

namespace SaaSForge.Api.Services.Auth
{
    public interface IGoogleTokenValidatorService
    {
        Task<GoogleJsonWebSignature.Payload> ValidateAuthorizationCodeAsync(string code);
    }

    public class GoogleTokenValidatorService : IGoogleTokenValidatorService
    {
        private readonly IConfiguration _config;
        private readonly HttpClient _httpClient;

        public GoogleTokenValidatorService(IConfiguration config, HttpClient httpClient)
        {
            _config = config;
            _httpClient = httpClient;
        }

        public async Task<GoogleJsonWebSignature.Payload> ValidateAuthorizationCodeAsync(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
            {
                throw new InvalidOperationException("Google authorization code is required.");
            }

            var clientId = _config["Authentication:Google:ClientId"];
            var clientSecret = _config["Authentication:Google:ClientSecret"];

            if (string.IsNullOrWhiteSpace(clientId))
            {
                throw new InvalidOperationException("Google ClientId is not configured.");
            }

            if (string.IsNullOrWhiteSpace(clientSecret))
            {
                throw new InvalidOperationException("Google ClientSecret is not configured.");
            }

            var formData = new Dictionary<string, string>
            {
                ["code"] = code,
                ["client_id"] = clientId,
                ["client_secret"] = clientSecret,
                ["redirect_uri"] = "postmessage",
                ["grant_type"] = "authorization_code"
            };

            using var request = new HttpRequestMessage(HttpMethod.Post, "https://oauth2.googleapis.com/token")
            {
                Content = new FormUrlEncodedContent(formData)
            };

            using var response = await _httpClient.SendAsync(request);
            var responseBody = await response.Content.ReadAsStringAsync();

            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"Google token exchange failed. Response: {responseBody}");
            }

            using var json = JsonDocument.Parse(responseBody);

            if (!json.RootElement.TryGetProperty("id_token", out var idTokenElement))
            {
                throw new InvalidOperationException("Google token response does not contain id_token.");
            }

            var idToken = idTokenElement.GetString();

            if (string.IsNullOrWhiteSpace(idToken))
            {
                throw new InvalidOperationException("Google id_token is empty.");
            }

            var settings = new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = new[] { clientId }
            };

            return await GoogleJsonWebSignature.ValidateAsync(idToken, settings);
        }
    }
}