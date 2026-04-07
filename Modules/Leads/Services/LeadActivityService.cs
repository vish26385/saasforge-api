using SaaSForge.Api.Data;
using SaaSForge.Api.Modules.Leads.Entities;
using SaaSForge.Api.Modules.Leads.Interfaces;

namespace SaaSForge.Api.Modules.Leads.Services;

public class LeadActivityService : ILeadActivityService
{
    private readonly AppDbContext _context;

    public LeadActivityService(AppDbContext context)
    {
        _context = context;
    }

    public async Task AddAsync(int businessId, Guid leadId, string activityType, string title, string? description = null)
    {
        var activity = new LeadActivity
        {
            Id = Guid.NewGuid(),
            BusinessId = businessId,
            LeadId = leadId,
            ActivityType = activityType,
            Title = title,
            Description = description,
            CreatedAtUtc = DateTime.UtcNow
        };

        _context.LeadActivities.Add(activity);
        await _context.SaveChangesAsync();
    }
}