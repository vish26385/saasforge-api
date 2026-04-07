using SaaSForge.Api.Modules.Leads.Dtos;

namespace SaaSForge.Api.Modules.Leads.Interfaces;

public interface ILeadTagService
{
    Task<List<LeadTagDto>> GetAllAsync(int businessId);
    Task<Guid> CreateAsync(int businessId, CreateLeadTagRequest request);
}