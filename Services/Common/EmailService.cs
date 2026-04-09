using Microsoft.Extensions.Options;
using SaaSForge.Api.Configurations;
using System.Net;
using System.Net.Mail;
using System.Text;
using System.Text.Json;
using System.Net.Http.Headers;

namespace SaaSForge.Api.Services.Common
{
    public interface IEmailService
    {
        Task SendEmailAsync(string to, string subject, string body);
        Task<bool> SendEmailVerificationAsync(string toEmail, string userName, string verificationLink);
        Task<bool> SendPasswordResetAsync(string toEmail, string userName, string resetLink);
    }

    public class EmailService : IEmailService
    {
        private readonly IConfiguration _config;
        private readonly HttpClient _httpClient;
        private readonly ILogger<EmailService> _logger;
        private readonly ResendSettings _settings;

        public EmailService(IConfiguration config, HttpClient httpClient, ILogger<EmailService> logger, IOptions<ResendSettings> options)
        {
            _config = config;
            _httpClient = httpClient;
            _logger = logger;
            _settings = options.Value;
        }

        public async Task SendEmailAsync(string to, string subject, string body)
        {
            try
            {           
            var smtpSection = _config.GetSection("SmtpSettings");

            using var client = new SmtpClient(smtpSection["Host"], int.Parse(smtpSection["Port"]))
            {
                Credentials = new NetworkCredential(smtpSection["UserName"], smtpSection["Password"]),
                EnableSsl = bool.Parse(smtpSection["EnableSsl"])
            };

            var mail = new MailMessage
            {
                From = new MailAddress(smtpSection["SenderEmail"], smtpSection["SenderName"]),
                Subject = subject,
                Body = body,
                IsBodyHtml = true
            };
            mail.To.Add(to);

            await client.SendMailAsync(mail);
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        public async Task<bool> SendEmailVerificationAsync(string toEmail, string userName, string verificationLink)
        {
            var subject = "Verify your email - LeadFlow AI";

            var html = $"""
                <div style="font-family:Arial,sans-serif;line-height:1.6;color:#111;">
                    <h2>Verify your email</h2>
                    <p>Hello {HtmlEncode(userName)},</p>
                    <p>Thanks for registering with <strong>LeadFlow AI</strong>.</p>
                    <p>Please verify your email by clicking the button below:</p>
                    <p style="margin:24px 0;">
                        <a href="{verificationLink}" style="background:#111;color:#fff;padding:12px 20px;text-decoration:none;border-radius:6px;display:inline-block;">
                            Verify Email
                        </a>
                    </p>
                    <p>If the button does not work, copy and paste this link into your browser:</p>
                    <p><a href="{verificationLink}">{verificationLink}</a></p>
                    <p>If you did not create this account, you can ignore this email.</p>
                    <br />
                    <p>— LeadFlow AI</p>
                </div>
                """;

            return await SendAsync(toEmail, subject, html);
        }

        public async Task<bool> SendPasswordResetAsync(string toEmail, string userName, string resetLink)
        {
            var subject = "Reset your password - LeadFlow AI";

            var html = $"""
                <div style="font-family:Arial,sans-serif;line-height:1.6;color:#111;">
                    <h2>Reset your password</h2>
                    <p>Hello {HtmlEncode(userName)},</p>
                    <p>We received a request to reset your password for <strong>LeadFlow AI</strong>.</p>
                    <p>Click the button below to set a new password:</p>
                    <p style="margin:24px 0;">
                        <a href="{resetLink}" style="background:#111;color:#fff;padding:12px 20px;text-decoration:none;border-radius:6px;display:inline-block;">
                            Reset Password
                        </a>
                    </p>
                    <p>If the button does not work, copy and paste this link into your browser:</p>
                    <p><a href="{resetLink}">{resetLink}</a></p>
                    <p>If you did not request this, you can ignore this email.</p>
                    <br />
                    <p>— LeadFlow AI</p>
                </div>
                """;

            return await SendAsync(toEmail, subject, html);
        }

        private async Task<bool> SendAsync(string toEmail, string subject, string html)
        {
            if (string.IsNullOrWhiteSpace(_settings.ApiKey))
            {
                _logger.LogError("Resend API key is missing.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(_settings.FromEmail))
            {
                _logger.LogError("Resend FromEmail is missing.");
                return false;
            }

            try
            {
                using var request = new HttpRequestMessage(HttpMethod.Post, "emails");

                request.Headers.Authorization =
                    new AuthenticationHeaderValue("Bearer", _settings.ApiKey);

                var payload = new
                {
                    from = $"{_settings.FromName} <{_settings.FromEmail}>",
                    to = new[] { toEmail },
                    subject = subject,
                    html = html
                };

                var json = JsonSerializer.Serialize(payload);

                request.Content = new StringContent(json, Encoding.UTF8);
                request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

                using var response = await _httpClient.SendAsync(request);
                var responseBody = await response.Content.ReadAsStringAsync();

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Email sent successfully via Resend to {Email}", toEmail);
                    return true;
                }

                _logger.LogError(
                    "Resend email failed. StatusCode: {StatusCode}, Response: {Response}",
                    (int)response.StatusCode,
                    responseBody);

                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception while sending email via Resend to {Email}", toEmail);
                return false;
            }
        }

        private static string HtmlEncode(string? value)
        {
            return System.Net.WebUtility.HtmlEncode(value ?? string.Empty);
        }
    }
}
