using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SaaSForge.Api.Modules.Leads.Dtos;
using SaaSForge.Api.Modules.Leads.Interfaces;
using SaaSForge.Api.Modules.Leads.Models;
using System.Security.Claims;

namespace SaaSForge.Api.Modules.Leads.Controllers;

[ApiController]
[Authorize]
[Route("api/leads")]
public class LeadsController : ControllerBase
{
    private readonly ILeadService _leadService;

    public LeadsController(ILeadService leadService)
    {
        _leadService = leadService;
    }

    [HttpGet]
    public async Task<IActionResult> GetLeads([FromQuery] LeadListQuery query)
    {
        var businessId = GetBusinessId();
        var result = await _leadService.GetLeadsAsync(businessId, query);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id)
    {
        var businessId = GetBusinessId();
        var lead = await _leadService.GetByIdAsync(businessId, id);

        if (lead is null)
            return NotFound();

        return Ok(lead);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateLeadRequest request)
    {
        var businessId = GetBusinessId();
        var leadId = await _leadService.CreateAsync(businessId, request);
        return Ok(new { leadId });
    }

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateLeadRequest request)
    {
        var businessId = GetBusinessId();
        await _leadService.UpdateAsync(businessId, id, request);
        return NoContent();
    }

    [HttpPut("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateLeadStatusRequest request)
    {
        var businessId = GetBusinessId();
        await _leadService.UpdateStatusAsync(businessId, id, request.Status);
        return NoContent();
    }

    [HttpPut("{id:guid}/follow-up")]
    public async Task<IActionResult> ScheduleFollowUp(Guid id, [FromBody] ScheduleFollowUpRequest request)
    {
        var businessId = GetBusinessId();
        await _leadService.ScheduleFollowUpAsync(businessId, id, request.NextFollowUpAtUtc);
        return NoContent();
    }

    [HttpPost("{id:guid}/messages")]
    public async Task<IActionResult> AddMessage(Guid id, [FromBody] AddLeadMessageRequest request)
    {
        var businessId = GetBusinessId();
        await _leadService.AddMessageAsync(businessId, id, request);
        return NoContent();
    }

    [HttpPost("{id:guid}/notes")]
    public async Task<IActionResult> AddNote(Guid id, [FromBody] AddLeadNoteRequest request)
    {
        var businessId = GetBusinessId();
        await _leadService.AddNoteAsync(businessId, id, request);
        return NoContent();
    }

    [HttpPost("{id:guid}/tags/{tagId:guid}")]
    public async Task<IActionResult> AddTag(Guid id, Guid tagId)
    {
        var businessId = GetBusinessId();
        await _leadService.AddTagAsync(businessId, id, tagId);
        return NoContent();
    }

    [HttpDelete("{id:guid}/tags/{tagId:guid}")]
    public async Task<IActionResult> RemoveTag(Guid id, Guid tagId)
    {
        var businessId = GetBusinessId();
        await _leadService.RemoveTagAsync(businessId, id, tagId);
        return NoContent();
    }

    [HttpPost("{id:guid}/archive")]
    public async Task<IActionResult> Archive(Guid id)
    {
        var businessId = GetBusinessId();
        await _leadService.ArchiveAsync(businessId, id);
        return NoContent();
    }

    private int GetBusinessId()
    {
        var claims = User.Claims.Select(c => new { c.Type, c.Value }).ToList();

        var businessIdValue = User.FindFirstValue("businessId");

        if (string.IsNullOrWhiteSpace(businessIdValue))
            throw new UnauthorizedAccessException("BusinessId claim not found.");

        return int.Parse(businessIdValue);
    }

    [HttpPut("{id:guid}/messages/{messageId:guid}/sent")]
    public async Task<IActionResult> MarkMessageSent(
    Guid id,
    Guid messageId,
    [FromBody] MarkLeadMessageSentRequest request)
    {
        var businessId = GetBusinessId();
        await _leadService.MarkMessageSentAsync(businessId, id, messageId, request.IsSent);
        return NoContent();
    }

    [HttpPut("{id:guid}/messages/{messageId:guid}")]
    public async Task<IActionResult> UpdateMessage(
    Guid id,
    Guid messageId,
    [FromBody] UpdateLeadMessageRequest request)
    {
        var businessId = GetBusinessId();
        await _leadService.UpdateMessageAsync(businessId, id, messageId, request);
        return NoContent();
    }
}