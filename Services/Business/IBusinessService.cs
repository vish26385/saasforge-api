using SaaSForge.Api.DTOs.Business;

namespace SaaSForge.Api.Services.Business
{
    public interface IBusinessService
    {
        Task<BusinessResponseDto> CreateAsync(string ownerUserId, CreateBusinessDto dto);
        Task<BusinessResponseDto?> GetMyBusinessAsync(string ownerUserId);
        Task<BusinessResponseDto?> UpdateMyBusinessAsync(string ownerUserId, UpdateBusinessDto dto);
    }
}