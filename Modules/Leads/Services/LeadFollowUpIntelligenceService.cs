using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using SaaSForge.Api.Data;
using SaaSForge.Api.Modules.Leads.Dtos;
using SaaSForge.Api.Modules.Leads.Entities;
using SaaSForge.Api.Modules.Leads.Interfaces;

namespace SaaSForge.Api.Modules.Leads.Services;

public sealed class LeadFollowUpIntelligenceService : ILeadFollowUpIntelligenceService
{
    private readonly AppDbContext _context;

    public LeadFollowUpIntelligenceService(AppDbContext context)
    {
        _context = context;
    }

    public async Task<LeadFollowUpIntelligenceDto> GetFollowUpIntelligenceAsync(
    int businessId,
    int takePerBucket = 10,
    CancellationToken cancellationToken = default)
    {
        if (takePerBucket <= 0)
        {
            takePerBucket = 10;
        }

        if (takePerBucket > 50)
        {
            takePerBucket = 50;
        }

        var nowUtc = DateTime.UtcNow;

        var utcTodayStart = new DateTime(
            nowUtc.Year,
            nowUtc.Month,
            nowUtc.Day,
            0,
            0,
            0,
            DateTimeKind.Utc);

        var utcTomorrowStart = utcTodayStart.AddDays(1);

        var baseQuery = BuildBaseQuery(businessId);

        var overdueQuery = baseQuery
            .Where(x =>
                x.NextFollowUpAtUtc.HasValue &&
                x.NextFollowUpAtUtc.Value < nowUtc)
            .OrderBy(x => x.NextFollowUpAtUtc);

        var dueTodayQuery = baseQuery
            .Where(x =>
                x.NextFollowUpAtUtc.HasValue &&
                x.NextFollowUpAtUtc.Value >= nowUtc &&
                x.NextFollowUpAtUtc.Value < utcTomorrowStart)
            .OrderBy(x => x.NextFollowUpAtUtc);

        var upcomingQuery = baseQuery
            .Where(x =>
                x.NextFollowUpAtUtc.HasValue &&
                x.NextFollowUpAtUtc.Value >= utcTomorrowStart)
            .OrderBy(x => x.NextFollowUpAtUtc);

        var overdueCount = await overdueQuery.CountAsync(cancellationToken);
        var overdueItems = await overdueQuery
            .Take(takePerBucket)
            .Select(ToDto())
            .ToListAsync(cancellationToken);

        var dueTodayCount = await dueTodayQuery.CountAsync(cancellationToken);
        var dueTodayItems = await dueTodayQuery
            .Take(takePerBucket)
            .Select(ToDto())
            .ToListAsync(cancellationToken);

        var upcomingCount = await upcomingQuery.CountAsync(cancellationToken);
        var upcomingItems = await upcomingQuery
            .Take(takePerBucket)
            .Select(ToDto())
            .ToListAsync(cancellationToken);

        return new LeadFollowUpIntelligenceDto
        {
            GeneratedAtUtc = nowUtc,
            UtcTodayStart = utcTodayStart,
            UtcTomorrowStart = utcTomorrowStart,

            Overdue = new LeadFollowUpBucketDto
            {
                Count = overdueCount,
                Items = overdueItems
            },

            DueToday = new LeadFollowUpBucketDto
            {
                Count = dueTodayCount,
                Items = dueTodayItems
            },

            Upcoming = new LeadFollowUpBucketDto
            {
                Count = upcomingCount,
                Items = upcomingItems
            }
        };
    }

    private IQueryable<Lead> BuildBaseQuery(int businessId)
    {
        return _context.Leads
            .AsNoTracking()
            .Where(x =>
                x.BusinessId == businessId &&
                x.NextFollowUpAtUtc != null &&
                !x.IsArchived &&
                x.Status != "ClosedWon" &&
                x.Status != "ClosedLost");
    }

    private static Expression<Func<Lead, LeadFollowUpItemDto>> ToDto()
    {
        return lead => new LeadFollowUpItemDto
        {
            LeadId = lead.Id,
            LeadName = lead.FullName,
            CustomerName = lead.FullName,
            CustomerEmail = lead.Email,
            CustomerPhone = lead.Phone,
            Status = lead.Status,
            Priority = lead.Priority,
            LastActivityAtUtc = lead.LastContactAtUtc,
            NextFollowUpAtUtc = lead.NextFollowUpAtUtc!.Value,
            CreatedAtUtc = lead.CreatedAtUtc
        };
    }
}