namespace SaaSForge.Api.Modules.Leads.Interfaces;

public interface ILeadActivityService
{
    Task AddAsync(int businessId, Guid leadId, string activityType, string title, string? description = null);
}