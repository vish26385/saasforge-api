using SaaSForge.Api.Models;
using SaaSForge.Api.Modules.Leads.Dtos;
using SaaSForge.Api.Modules.Leads.Models;

namespace SaaSForge.Api.Modules.Leads.Interfaces;

public interface ILeadService
{
    Task<PagedResult<LeadListItemDto>> GetLeadsAsync(int businessId, LeadListQuery query);
    Task<LeadDetailsDto?> GetByIdAsync(int businessId, Guid leadId);
    Task<Guid> CreateAsync(int businessId, CreateLeadRequest request);
    Task UpdateAsync(int businessId, Guid leadId, UpdateLeadRequest request);
    Task UpdateStatusAsync(int businessId, Guid leadId, string status);
    Task ScheduleFollowUpAsync(int businessId, Guid leadId, DateTime? nextFollowUpAtUtc);
    Task AddMessageAsync(int businessId, Guid leadId, AddLeadMessageRequest request);
    Task AddNoteAsync(int businessId, Guid leadId, AddLeadNoteRequest request);
    Task AddTagAsync(int businessId, Guid leadId, Guid tagId);
    Task RemoveTagAsync(int businessId, Guid leadId, Guid tagId);
    Task ArchiveAsync(int businessId, Guid leadId);
    Task<LeadDashboardSummaryDto> GetDashboardSummaryAsync(int businessId);
    Task MarkMessageSentAsync(int businessId, Guid leadId, Guid messageId, bool isSent);
    Task UpdateMessageAsync(
    int businessId,
    Guid leadId,
    Guid messageId,
    UpdateLeadMessageRequest request);

}