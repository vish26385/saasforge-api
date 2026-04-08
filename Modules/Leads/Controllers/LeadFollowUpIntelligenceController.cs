using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaaSForge.Api.Modules.Leads.Dtos;
using SaaSForge.Api.Modules.Leads.Interfaces;

namespace SaaSForge.Api.Modules.Leads.Controllers;

[ApiController]
[Authorize]
[Route("api/leads/follow-up-intelligence")]
public sealed class LeadFollowUpIntelligenceController : ControllerBase
{
    private readonly ILeadFollowUpIntelligenceService _leadFollowUpIntelligenceService;

    public LeadFollowUpIntelligenceController(
        ILeadFollowUpIntelligenceService leadFollowUpIntelligenceService)
    {
        _leadFollowUpIntelligenceService = leadFollowUpIntelligenceService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(LeadFollowUpIntelligenceDto), StatusCodes.Status200OK)]
    public async Task<ActionResult<LeadFollowUpIntelligenceDto>> Get(
        [FromQuery] int takePerBucket = 10,
        CancellationToken cancellationToken = default)
    {
        var businessId = GetBusinessId();

        var result = await _leadFollowUpIntelligenceService.GetFollowUpIntelligenceAsync(
            businessId,
            takePerBucket,
            cancellationToken);

        return Ok(result);
    }

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
}