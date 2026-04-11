//public class EmailTemplateService
//{
//    private readonly IWebHostEnvironment _env;

//    public EmailTemplateService(IWebHostEnvironment env)
//    {
//        _env = env;
//    }

//    public string GetTemplate(string templateName, string actionUrl, string actionText)
//    {
//        var basePath = Path.Combine(_env.ContentRootPath, "EmailTemplates");

//        var baseHtml = File.ReadAllText(Path.Combine(basePath, "BaseTemplate.html"));
//        var contentHtml = File.ReadAllText(Path.Combine(basePath, templateName));

//        var finalHtml = baseHtml
//            .Replace("{{content}}", contentHtml)
//            .Replace("{{action_url}}", actionUrl ?? "")
//            .Replace("{{action_text}}", actionText ?? "")
//            .Replace("{{year}}", DateTime.UtcNow.Year.ToString());

//        return finalHtml;
//    }
//}

using System.Text;

namespace SaaSForge.Api.Services.Common
{
    public class EmailTemplateService
    {
        private readonly IWebHostEnvironment _env;

        public EmailTemplateService(IWebHostEnvironment env)
        {
            _env = env;
        }

        public string GetTemplate(
            string templateName,
            string actionUrl,
            string actionText,
            Dictionary<string, string>? replacements = null)
        {
            var basePath = Path.Combine(_env.ContentRootPath, "EmailTemplates");

            var baseHtml = File.ReadAllText(Path.Combine(basePath, "BaseTemplate.html"));
            var contentHtml = File.ReadAllText(Path.Combine(basePath, templateName));

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