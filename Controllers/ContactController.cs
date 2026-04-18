using Microsoft.AspNetCore.Mvc;
using SaaSForge.Api.Models.Contact;
using SaaSForge.Api.Services.Common;

namespace SaaSForge.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ContactController : ControllerBase
    {
        private readonly IEmailService _emailService;
        private readonly ILogger<ContactController> _logger;

        public ContactController(IEmailService emailService, ILogger<ContactController> logger)
        {
            _emailService = emailService;
            _logger = logger;
        }

        [HttpPost]
        public async Task<IActionResult> Send([FromBody] ContactRequestDto request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new ContactResponseDto
                {
                    Success = false,
                    Message = "Please fill all fields correctly."
                });
            }

            try
            {
                await _emailService.SendContactEmailAsync(
                    request.Name.Trim(),
                    request.Email.Trim(),
                    request.Subject.Trim(),
                    request.Message.Trim()
                );

                return Ok(new ContactResponseDto
                {
                    Success = true,
                    Message = "Your message has been sent successfully."
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error while sending contact form email.");

                return StatusCode(500, new ContactResponseDto
                {
                    Success = false,
                    Message = "Something went wrong while sending your message."
                });
            }
        }
    }
}
