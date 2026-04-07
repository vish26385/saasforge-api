using Microsoft.EntityFrameworkCore;
using SaaSForge.Api.Data;
using SaaSForge.Api.Modules.Leads.Dtos;
using SaaSForge.Api.Modules.Leads.Entities;
using SaaSForge.Api.Modules.Leads.Interfaces;

namespace SaaSForge.Api.Modules.Leads.Services;

public class LeadTagService : ILeadTagService
{
    private readonly AppDbContext _context;

    public LeadTagService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<List<LeadTagDto>> GetAllAsync(int businessId)
    {
        return await _context.LeadTags
            .AsNoTracking()
            .Where(x => x.BusinessId == businessId)
            .OrderBy(x => x.Name)
            .Select(x => new LeadTagDto
            {
                Id = x.Id,
                Name = x.Name,
                Color = x.Color
            })
            .ToListAsync();
    }

    public async Task<Guid> CreateAsync(int businessId, CreateLeadTagRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name))
            throw new InvalidOperationException("Tag name is required.");

        var normalizedName = request.Name.Trim();

        var exists = await _context.LeadTags
            .AnyAsync(x => x.BusinessId == businessId && x.Name.ToLower() == normalizedName.ToLower());

        if (exists)
            throw new InvalidOperationException("Tag already exists.");

        var tag = new LeadTag
        {
            Id = Guid.NewGuid(),
            BusinessId = businessId,
            Name = normalizedName,
            Color = string.IsNullOrWhiteSpace(request.Color) ? null : request.Color.Trim(),
            CreatedAtUtc = DateTime.UtcNow
        };

        _context.LeadTags.Add(tag);
        await _context.SaveChangesAsync();

        return tag.Id;
    }
}