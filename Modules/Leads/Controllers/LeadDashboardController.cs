using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaaSForge.Api.Modules.Leads.Dtos;
using SaaSForge.Api.Modules.Leads.Interfaces;
using System.Security.Claims;

namespace SaaSForge.Api.Modules.Leads.Controllers;

[ApiController]
[Authorize]
[Route("api/lead-dashboard")]
public class LeadDashboardController : ControllerBase
{
    private readonly ILeadService _leadService;
    private readonly ILeadAttentionIntelligenceService _leadAttentionIntelligenceService;
    private readonly ILeadAlertService _leadAlertService;

    public LeadDashboardController(
        ILeadService leadService,
        ILeadAttentionIntelligenceService leadAttentionIntelligenceService,
        ILeadAlertService leadAlertService)
    {
        _leadService = leadService;
        _leadAttentionIntelligenceService = leadAttentionIntelligenceService;
        _leadAlertService = leadAlertService;
    }

    // =========================
    // DASHBOARD SUMMARY
    // =========================
    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary(CancellationToken cancellationToken = default)
    {
        var businessId = GetBusinessId();

        var result = await _leadService.GetDashboardSummaryAsync(businessId);

        return Ok(result);
    }

    // =========================
    // ATTENTION INTELLIGENCE
    // =========================
    [HttpGet("attention-intelligence")]
    [ProducesResponseType(typeof(LeadAttentionIntelligenceDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAttentionIntelligence(
        [FromQuery] int takePerBucket = 8,
        CancellationToken cancellationToken = default)
    {
        var businessId = GetBusinessId();

        var result = await _leadAttentionIntelligenceService.GetAttentionIntelligenceAsync(
            businessId,
            takePerBucket,
            cancellationToken);

        return Ok(result);
    }

    // =========================
    // ALERTS (MANUAL GENERATE - OPTIONAL)
    // =========================
    [HttpPost("alerts/generate")]
    public async Task<IActionResult> GenerateAlerts(CancellationToken cancellationToken = default)
    {
        var businessId = GetBusinessId();

        await _leadAlertService.GenerateAlertsAsync(businessId, cancellationToken);

        return Ok(new { message = "Lead alerts generated successfully." });
    }

    // =========================
    // GET ALERTS (READ ONLY)
    // =========================
    [HttpGet("alerts")]
    [ProducesResponseType(typeof(LeadAlertsResponseDto), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetAlerts(
        [FromQuery] int take = 20,
        CancellationToken cancellationToken = default)
    {
        var businessId = GetBusinessId();

        // ❌ IMPORTANT: DO NOT GENERATE ALERTS HERE
        // Worker is responsible now

        var result = await _leadAlertService.GetActiveAlertsAsync(
            businessId,
            take,
            cancellationToken);

        return Ok(result);
    }

    // =========================
    // SINGLE ALERT RESOLVE
    // =========================
    [HttpPost("alerts/{alertId:guid}/resolve")]
    public async Task<IActionResult> ResolveAlert(
        Guid alertId,
        CancellationToken cancellationToken = default)
    {
        var businessId = GetBusinessId();

        await _leadAlertService.ResolveAlertAsync(
            businessId,
            alertId,
            cancellationToken);

        return Ok(new { message = "Lead alert resolved successfully." });
    }

    // =========================
    // LEAD-LEVEL ACKNOWLEDGE (WITH SUPPRESSION)
    // =========================
    [HttpPost("alerts/lead/{leadId:guid}/acknowledge")]
    public async Task<IActionResult> AcknowledgeLeadAlerts(
        Guid leadId,
        [FromQuery] int suppressHours = 12,
        CancellationToken cancellationToken = default)
    {
        var businessId = GetBusinessId();

        await _leadAlertService.AcknowledgeAllAlertsForLeadAsync(
            businessId,
            leadId,
            suppressHours,
            cancellationToken);

        return Ok(new
        {
            message = $"All active alerts for this lead were acknowledged for {suppressHours} hours."
        });
    }

    // =========================
    // PRIVATE: BUSINESS ID RESOLVER
    // =========================
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