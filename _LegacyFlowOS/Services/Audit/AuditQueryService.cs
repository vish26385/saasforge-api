using SaaSForge.Api.Data;
using Microsoft.EntityFrameworkCore;
using SaaSForge.Api._LegacyFlowOS.Models.Audit;

public interface IAuditQueryService
{
    Task<List<AiPlanAuditDto>> GetRecentAsync(int count = 20);
    Task<List<AiPlanAuditDto>> GetByUserAsync(string userId);
    Task<AuditStatsDto> GetStatsAsync();
}

public class AuditQueryService : IAuditQueryService
{
    private readonly AppDbContext _context;

    public AuditQueryService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<AiPlanAuditDto>> GetRecentAsync(int count = 20)
    {
        return await _context.AiPlanAudits
            .OrderByDescending(a => a.RequestedAt)
            .Take(count)
            .Select(a => new AiPlanAuditDto
            {
                Id = a.Id,
                UserId = a.UserId,
                RequestedAt = a.RequestedAt,
                LatencyMs = a.LatencyMs,
                ModelUsed = a.ModelUsed,
                WasRegenerated = a.WasRegenerated,
                AvgConfidence = a.AvgConfidence,
                CoveragePercent = a.CoveragePercent,
                AlignedTasksPercent = a.AlignedTasksPercent,
                OverlapCount = a.OverlapCount
            })
            .ToListAsync();
    }

    public async Task<List<AiPlanAuditDto>> GetByUserAsync(string userId)
    {
        return await _context.AiPlanAudits
            .Where(a => a.UserId == userId)
            .OrderByDescending(a => a.RequestedAt)
            .Take(50)
            .Select(a => new AiPlanAuditDto
            {
                Id = a.Id,
                UserId = a.UserId,
                RequestedAt = a.RequestedAt,
                LatencyMs = a.LatencyMs,
                ModelUsed = a.ModelUsed,
                WasRegenerated = a.WasRegenerated,
                AvgConfidence = a.AvgConfidence,
                CoveragePercent = a.CoveragePercent,
                AlignedTasksPercent = a.AlignedTasksPercent,
                OverlapCount = a.OverlapCount
            })
            .ToListAsync();
    }

    public async Task<AuditStatsDto> GetStatsAsync()
    {
        var total = await _context.AiPlanAudits.CountAsync();
        if (total == 0)
            return new AuditStatsDto();

        var avgLatency = await _context.AiPlanAudits.AverageAsync(a => a.LatencyMs);
        var regenRate = await _context.AiPlanAudits.AverageAsync(a => a.WasRegenerated ? 1 : 0);
        var avgConfidence = await _context.AiPlanAudits.AverageAsync(a => a.AvgConfidence);
        var avgCoverage = await _context.AiPlanAudits.AverageAsync(a => a.CoveragePercent);

        return new AuditStatsDto
        {
            TotalPlans = total,
            RegenerationRate = Math.Round(regenRate * 100, 2),
            AvgLatencyMs = (long)avgLatency,
            AvgConfidence = Math.Round(avgConfidence, 2),
            AvgCoverage = Math.Round(avgCoverage, 2)
        };
    }
}
