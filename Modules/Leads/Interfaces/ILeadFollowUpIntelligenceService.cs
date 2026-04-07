using SaaSForge.Api.Modules.Leads.Dtos;

namespace SaaSForge.Api.Modules.Leads.Interfaces;

public interface ILeadFollowUpIntelligenceService
{
    Task<LeadFollowUpIntelligenceDto> GetFollowUpIntelligenceAsync(
        int businessId,
        int takePerBucket = 10,
        CancellationToken cancellationToken = default);
}