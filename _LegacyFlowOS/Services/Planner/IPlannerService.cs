using SaaSForge.Api._LegacyFlowOS.DTOs.Plan;

namespace SaaSForge.Api._LegacyFlowOS.Services.Planner
{
    public interface IPlannerService
    {
        //Task<PlanResponseDto> GeneratePlanAsync(string userId, DateTime date, string? toneOverride = null, bool forceRegenerate = false);

        //Task<PlanResponseDto> GeneratePlanAsync(
        //                                    string userId,
        //                                    DateOnly date,
        //                                    string? toneOverride = null,
        //                                    bool forceRegenerate = false,
        //                                    DateTime? planStartUtc = null // ✅ add this
        //                                );

        Task<PlanResponseDto> GeneratePlanAsync(string userId, 
                                                DateOnly dateKey, 
                                                string? toneOverride = null, 
                                                bool forceRegenerate = false, 
                                                DateTime? planStartUtc = null);
    }
}
