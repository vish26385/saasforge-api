namespace SaaSForge.Api.Services.Common
{
    public class EmailTemplateService
    {
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<EmailTemplateService> _logger;

        public EmailTemplateService(
            IWebHostEnvironment env,
            ILogger<EmailTemplateService> logger)
        {
            _env = env;
            _logger = logger;
        }

        public string GetTemplate(
            string templateName,
            string actionUrl,
            string actionText,
            Dictionary<string, string>? replacements = null)
        {
            var templatesPath = Path.Combine(_env.ContentRootPath, "EmailTemplates");
            var baseTemplatePath = Path.Combine(templatesPath, "BaseTemplate.html");
            var contentTemplatePath = Path.Combine(templatesPath, templateName);

            _logger.LogInformation("Email template root path: {Path}", templatesPath);
            _logger.LogInformation("Base template path: {Path}", baseTemplatePath);
            _logger.LogInformation("Content template path: {Path}", contentTemplatePath);

            if (!File.Exists(baseTemplatePath))
            {
                throw new FileNotFoundException($"Base email template not found: {baseTemplatePath}");
            }

            if (!File.Exists(contentTemplatePath))
            {
                throw new FileNotFoundException($"Email template not found: {contentTemplatePath}");
            }

            var baseHtml = File.ReadAllText(baseTemplatePath);
            var contentHtml = File.ReadAllText(contentTemplatePath);

            if (replacements != null)
            {
                foreach (var item in replacements)
                {
                    contentHtml = contentHtml.Replace(item.Key, item.Value ?? string.Empty);
                }
            }

            var finalHtml = baseHtml
                .Replace("{{content}}", contentHtml)
                .Replace("{{action_url}}", actionUrl ?? string.Empty)
                .Replace("{{action_text}}", actionText ?? string.Empty)
                .Replace("{{year}}", DateTime.UtcNow.Year.ToString());

            return finalHtml;
        }
    }
}