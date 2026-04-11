//using Microsoft.Extensions.Options;
//using SaaSForge.Api.Configurations;
//using System.Net;
//using System.Net.Mail;
//using System.Text;
//using System.Text.Json;
//using System.Net.Http.Headers;

//namespace SaaSForge.Api.Services.Common
//{
//    public interface IEmailService
//    {
//        Task SendEmailAsync(string to, string subject, string body);
//        Task<bool> SendEmailVerificationAsync(string toEmail, string userName, string verificationLink);
//        Task<bool> SendPasswordResetAsync(string toEmail, string userName, string resetLink);
//    }

//    public class EmailService : IEmailService
//    {
//        private readonly IConfiguration _config;
//        private readonly HttpClient _httpClient;
//        private readonly ILogger<EmailService> _logger;
//        private readonly ResendSettings _settings;

//        public EmailService(IConfiguration config, HttpClient httpClient, ILogger<EmailService> logger, IOptions<ResendSettings> options)
//        {
//            _config = config;
//            _httpClient = httpClient;
//            _logger = logger;
//            _settings = options.Value;
//        }

//        public async Task SendEmailAsync(string to, string subject, string body)
//        {
//            try
//            {
//                var smtpSection = _config.GetSection("SmtpSettings");

//                using var client = new SmtpClient(smtpSection["Host"], int.Parse(smtpSection["Port"]))
//                {
//                    Credentials = new NetworkCredential(smtpSection["UserName"], smtpSection["Password"]),
//                    EnableSsl = bool.Parse(smtpSection["EnableSsl"])
//                };

//                var mail = new MailMessage
//                {
//                    From = new MailAddress(smtpSection["SenderEmail"], smtpSection["SenderName"]),
//                    Subject = subject,
//                    Body = body,
//                    IsBodyHtml = true
//                };
//                mail.To.Add(to);

//                await client.SendMailAsync(mail);
//            }
//            catch (Exception ex)
//            {
//                throw ex;
//            }
//        }

//        public async Task<bool> SendEmailVerificationAsync(string toEmail, string userName, string verificationLink)
//        {
//            var subject = "Verify your email - LeadFlow AI";

//            var html = $"""
//                <div style="font-family:Arial,sans-serif;line-height:1.6;color:#111;">
//                    <h2>Verify your email</h2>
//                    <p>Hello {HtmlEncode(userName)},</p>
//                    <p>Thanks for registering with <strong>LeadFlow AI</strong>.</p>
//                    <p>Please verify your email by clicking the button below:</p>
//                    <p style="margin:24px 0;">
//                        <a href="{verificationLink}" style="background:#111;color:#fff;padding:12px 20px;text-decoration:none;border-radius:6px;display:inline-block;">
//                            Verify Email
//                        </a>
//                    </p>
//                    <p>If the button does not work, copy and paste this link into your browser:</p>
//                    <p><a href="{verificationLink}">{verificationLink}</a></p>
//                    <p>If you did not create this account, you can ignore this email.</p>
//                    <br />
//                    <p>— LeadFlow AI</p>
//                </div>
//                """;

//            return await SendAsync(toEmail, subject, html);
//        }

//        public async Task<bool> SendPasswordResetAsync(string toEmail, string userName, string resetLink)
//        {
//            var subject = "Reset your password - LeadFlow AI";

//            var html = $"""
//                <div style="font-family:Arial,sans-serif;line-height:1.6;color:#111;">
//                    <h2>Reset your password</h2>
//                    <p>Hello {HtmlEncode(userName)},</p>
//                    <p>We received a request to reset your password for <strong>LeadFlow AI</strong>.</p>
//                    <p>Click the button below to set a new password:</p>
//                    <p style="margin:24px 0;">
//                        <a href="{resetLink}" style="background:#111;color:#fff;padding:12px 20px;text-decoration:none;border-radius:6px;display:inline-block;">
//                            Reset Password
//                        </a>
//                    </p>
//                    <p>If the button does not work, copy and paste this link into your browser:</p>
//                    <p><a href="{resetLink}">{resetLink}</a></p>
//                    <p>If you did not request this, you can ignore this email.</p>
//                    <br />
//                    <p>— LeadFlow AI</p>
//                </div>
//                """;

//            return await SendAsync(toEmail, subject, html);
//        }

//        private async Task<bool> SendAsync(string toEmail, string subject, string html)
//        {
//            if (string.IsNullOrWhiteSpace(_settings.ApiKey))
//            {
//                _logger.LogError("Resend API key is missing.");
//                return false;
//            }

//            if (string.IsNullOrWhiteSpace(_settings.FromEmail))
//            {
//                _logger.LogError("Resend FromEmail is missing.");
//                return false;
//            }

//            try
//            {
//                using var request = new HttpRequestMessage(HttpMethod.Post, "emails");

//                request.Headers.Authorization =
//                    new AuthenticationHeaderValue("Bearer", _settings.ApiKey);

//                var payload = new
//                {
//                    from = $"{_settings.FromName} <{_settings.FromEmail}>",
//                    to = new[] { toEmail },
//                    subject = subject,
//                    html = html
//                };

//                var json = JsonSerializer.Serialize(payload);

//                request.Content = new StringContent(json, Encoding.UTF8);
//                request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

//                using var response = await _httpClient.SendAsync(request);
//                var responseBody = await response.Content.ReadAsStringAsync();

//                if (response.IsSuccessStatusCode)
//                {
//                    _logger.LogInformation("Email sent successfully via Resend to {Email}", toEmail);
//                    return true;
//                }

//                _logger.LogError(
//                    "Resend email failed. StatusCode: {StatusCode}, Response: {Response}",
//                    (int)response.StatusCode,
//                    responseBody);

//                return false;
//            }
//            catch (Exception ex)
//            {
//                _logger.LogError(ex, "Exception while sending email via Resend to {Email}", toEmail);
//                return false;
//            }
//        }

//        private static string HtmlEncode(string? value)
//        {
//            return System.Net.WebUtility.HtmlEncode(value ?? string.Empty);
//        }
//    }
//}

using Microsoft.Extensions.Options;
using SaaSForge.Api.Configurations;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace SaaSForge.Api.Services.Common
{
    public interface IEmailService
    {
        Task<bool> SendEmailVerificationAsync(string toEmail, string userName, string verificationLink);
        Task<bool> SendPasswordResetAsync(string toEmail, string userName, string resetLink);
        Task<bool> SendPasswordResetSuccessAsync(string toEmail, string userName, string loginUrl);
        Task<bool> SendWelcomeEmailAsync(string toEmail, string userName, string loginUrl);
        Task<bool> SendNotificationEmailAsync(string toEmail, string subject, string heading, string message, string actionUrl, string actionText);
    }

    public class EmailService : IEmailService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<EmailService> _logger;
        private readonly ResendSettings _settings;
        private readonly EmailTemplateService _templateService;

        public EmailService(
            HttpClient httpClient,
            ILogger<EmailService> logger,
            IOptions<ResendSettings> options,
            EmailTemplateService templateService)
        {
            _httpClient = httpClient;
            _logger = logger;
            _settings = options.Value;
            _templateService = templateService;
        }

        public async Task<bool> SendEmailVerificationAsync(string toEmail, string userName, string verificationLink)
        {
            var subject = "Confirm your email to get started - LeadFlow AI";

            var html = _templateService.GetTemplate(
                "EmailVerification.html",
                verificationLink,
                "Confirm Email");

            var text =
                $"Hi {userName},\n\n" +
                "Thanks for signing up to LeadFlow AI.\n\n" +
                "Please confirm your email address to activate your account:\n" +
                $"{verificationLink}\n\n" +
                "If you did not create this account, you can ignore this email.";

            return await SendAsync(toEmail, subject, html, text, "EmailVerification");
        }

        public async Task<bool> SendPasswordResetAsync(string toEmail, string userName, string resetLink)
        {
            var subject = "Reset your password - LeadFlow AI";

            var html = _templateService.GetTemplate(
                "ForgotPassword.html",
                resetLink,
                "Reset Password");

            var text =
                $"Hi {userName},\n\n" +
                "We received a request to reset your password.\n\n" +
                $"Reset your password here:\n{resetLink}\n\n" +
                "If you did not request this, you can safely ignore this email.";

            return await SendAsync(toEmail, subject, html, text, "PasswordReset");
        }

        public async Task<bool> SendPasswordResetSuccessAsync(string toEmail, string userName, string loginUrl)
        {
            var subject = "Your password was reset successfully - LeadFlow AI";

            var html = _templateService.GetTemplate(
                "ResetPasswordSuccess.html",
                loginUrl,
                "Login Now");

            var text =
                $"Hi {userName},\n\n" +
                "Your password has been reset successfully.\n\n" +
                $"Login here:\n{loginUrl}\n\n" +
                "If this was not you, please contact support immediately.";

            return await SendAsync(toEmail, subject, html, text, "PasswordResetSuccess");
        }

        public async Task<bool> SendWelcomeEmailAsync(string toEmail, string userName, string loginUrl)
        {
            var subject = "You're all set - Welcome to LeadFlow AI";

            var html = _templateService.GetTemplate(
                "Welcome.html",
                loginUrl,
                "Open Dashboard");

            var text =
                $"Welcome to LeadFlow AI, {userName}.\n\n" +
                "Your email has been verified successfully.\n\n" +
                $"Open your account here:\n{loginUrl}";

            return await SendAsync(toEmail, subject, html, text, "WelcomeEmail");
        }

        public async Task<bool> SendNotificationEmailAsync(
            string toEmail,
            string subject,
            string heading,
            string message,
            string actionUrl,
            string actionText)
        {
            var replacements = new Dictionary<string, string>
            {
                ["{{heading}}"] = heading,
                ["{{message}}"] = message
            };

            var html = _templateService.GetTemplate(
                "Notification.html",
                actionUrl,
                actionText,
                replacements);

            var text =
                $"{heading}\n\n" +
                $"{message}\n\n" +
                $"{actionUrl}";

            return await SendAsync(toEmail, subject, html, text, "NotificationEmail");
        }

        private async Task<bool> SendAsync(
            string toEmail,
            string subject,
            string html,
            string text,
            string emailType)
        {
            if (string.IsNullOrWhiteSpace(_settings.ApiKey))
            {
                _logger.LogError(
                    "Resend API key is missing. EmailType: {EmailType}, To: {Email}",
                    emailType,
                    toEmail);

                return false;
            }

            if (string.IsNullOrWhiteSpace(_settings.FromEmail))
            {
                _logger.LogError(
                    "Resend FromEmail is missing. EmailType: {EmailType}, To: {Email}",
                    emailType,
                    toEmail);

                return false;
            }

            var fromName = string.IsNullOrWhiteSpace(_settings.FromName)
                ? "LeadFlow AI Team"
                : _settings.FromName;

            try
            {
                _logger.LogInformation(
                    "Sending email. Type: {EmailType}, To: {Email}, Subject: {Subject}",
                    emailType,
                    toEmail,
                    subject);

                using var request = new HttpRequestMessage(HttpMethod.Post, "emails");

                request.Headers.Authorization =
                    new AuthenticationHeaderValue("Bearer", _settings.ApiKey);

                var payload = new
                {
                    from = $"{fromName} <{_settings.FromEmail}>",
                    to = new[] { toEmail },
                    subject,
                    html,
                    text
                };

                var json = JsonSerializer.Serialize(payload);
                request.Content = new StringContent(json, Encoding.UTF8, "application/json");

                using var response = await _httpClient.SendAsync(request);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation(
                        "Email sent successfully. Type: {EmailType}, To: {Email}, Subject: {Subject}",
                        emailType,
                        toEmail,
                        subject);

                    return true;
                }

                _logger.LogError(
                    "Resend email failed. Type: {EmailType}, To: {Email}, Subject: {Subject}, StatusCode: {StatusCode}, Response: {Response}",
                    emailType,
                    toEmail,
                    subject,
                    (int)response.StatusCode,
                    responseBody);

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Exception while sending email. Type: {EmailType}, To: {Email}, Subject: {Subject}",
                    emailType,
                    toEmail,
                    subject);

                return false;
            }
        }
    }
}