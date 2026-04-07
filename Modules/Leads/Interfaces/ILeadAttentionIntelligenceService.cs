using SaaSForge.Api.Modules.Leads.Dtos;

namespace SaaSForge.Api.Modules.Leads.Interfaces;

public interface ILeadAttentionIntelligenceService
{
    Task<LeadAttentionIntelligenceDto> GetAttentionIntelligenceAsync(
        int businessId,
        int takePerBucket = 8,
        CancellationToken cancellationToken = default);
}