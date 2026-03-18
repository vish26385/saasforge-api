using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaaSForge.Api.DTOs.Business;
using SaaSForge.Api.Services.Business;

namespace SaaSForge.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class BusinessController : ControllerBase
    {
        private readonly IBusinessService _businessService;

        public BusinessController(IBusinessService businessService)
        {
            _businessService = businessService;
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateBusinessDto dto)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { message = "User is not authenticated." });
                }

                var result = await _businessService.CreateAsync(userId, dto);
                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        [HttpGet("me")]
        public async Task<IActionResult> GetMyBusiness()
        {
            var userId = GetCurrentUserId();
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { message = "User is not authenticated." });
            }

            var result = await _businessService.GetMyBusinessAsync(userId);
            if (result == null)
            {
                return NotFound(new { message = "Business not found." });
            }

            return Ok(result);
        }

        [HttpPut("me")]
        public async Task<IActionResult> UpdateMyBusiness([FromBody] UpdateBusinessDto dto)
        {
            try
            {
                var userId = GetCurrentUserId();
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized(new { message = "User is not authenticated." });
                }

                var result = await _businessService.UpdateMyBusinessAsync(userId, dto);
                if (result == null)
                {
                    return NotFound(new { message = "Business not found." });
                }

                return Ok(result);
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new { message = ex.Message });
            }
        }

        private string? GetCurrentUserId()
        {
            return User.FindFirstValue(ClaimTypes.NameIdentifier)
                   ?? User.FindFirstValue("id")
                   ?? User.FindFirstValue("sub");
        }
    }
}