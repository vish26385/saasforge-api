using SaaSForge.Api.Modules.Leads.Dtos;

namespace SaaSForge.Api.Modules.Leads.Interfaces;

public interface ILeadAiService
{
    Task<GenerateLeadReplyResponse> GenerateReplyAsync(int businessId, string userId, GenerateLeadReplyRequest request);
}