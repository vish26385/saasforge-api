using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaaSForge.Api.Modules.Leads.Dtos;
using SaaSForge.Api.Modules.Leads.Interfaces;
using System.Security.Claims;

namespace SaaSForge.Api.Modules.Leads.Controllers;

[ApiController]
[Authorize]
[Route("api/lead-tags")]
public class LeadTagsController : ControllerBase
{
    private readonly ILeadTagService _leadTagService;

    public LeadTagsController(ILeadTagService leadTagService)
    {
        _leadTagService = leadTagService;
    }

    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var businessId = GetBusinessId();
        var tags = await _leadTagService.GetAllAsync(businessId);
        return Ok(tags);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateLeadTagRequest request)
    {
        var businessId = GetBusinessId();
        var tagId = await _leadTagService.CreateAsync(businessId, request);
        return Ok(new { tagId });
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