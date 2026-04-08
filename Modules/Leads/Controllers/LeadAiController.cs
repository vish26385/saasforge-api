using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaaSForge.Api.Modules.Leads.Dtos;
using SaaSForge.Api.Modules.Leads.Interfaces;
using System.Security.Claims;

namespace SaaSForge.Api.Controllers;

[ApiController]
[Route("api/lead-ai")]
[Authorize]
public class LeadAiController : ControllerBase
{
    private readonly ILeadAiService _leadAiService;

    public LeadAiController(ILeadAiService leadAiService)
    {
        _leadAiService = leadAiService;
    }

    [HttpPost("generate-reply")]
    public async Task<IActionResult> GenerateReply([FromBody] GenerateLeadReplyRequest request)
    {
        try
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var businessId = GetBusinessId();
            var userId = GetUserId();

            var result = await _leadAiService.GenerateReplyAsync(businessId, userId, request);

            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return Unauthorized(new { message = ex.Message });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new
            {
                message = "Something went wrong while generating reply.",
                error = ex.Message
            });
        }
    }

    // ------------------------
    // PRIVATE HELPERS
    // ------------------------

    private int GetBusinessId()
    {
        var businessIdValue =
            User.FindFirstValue("BusinessId") ??
            User.FindFirstValue("businessId") ??
            User.FindFirstValue("business_id");

        if (string.IsNullOrWhiteSpace(businessIdValue))
        {
            throw new UnauthorizedAccessException("BusinessId claim not found.");
        }

        if (!int.TryParse(businessIdValue, out var businessId))
        {
            throw new UnauthorizedAccessException("BusinessId claim is invalid.");
        }

        return businessId;
    }

    private string GetUserId()
    {
        var userId =
            User.FindFirstValue(ClaimTypes.NameIdentifier) ??
            User.FindFirstValue("sub");

        if (string.IsNullOrWhiteSpace(userId))
            throw new UnauthorizedAccessException("UserId claim missing.");

        return userId;
    }
}