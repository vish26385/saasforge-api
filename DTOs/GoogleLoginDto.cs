using System.ComponentModel.DataAnnotations;

namespace SaaSForge.Api.DTOs
{
    public class GoogleLoginDto
    {
        //[Required]
        //public string IdToken { get; set; } = string.Empty;

        [Required]
        public string Code { get; set; } = default!;
    }
}