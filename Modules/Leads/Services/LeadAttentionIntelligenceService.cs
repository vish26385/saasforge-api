using Microsoft.EntityFrameworkCore;
using SaaSForge.Api.Data;
using SaaSForge.Api.Modules.Leads.Dtos;
using SaaSForge.Api.Modules.Leads.Interfaces;
using SaaSForge.Api.Modules.Leads.Entities;

namespace SaaSForge.Api.Modules.Leads.Services;

public sealed class LeadAttentionIntelligenceService : ILeadAttentionIntelligenceService
{
    private readonly AppDbContext _db;

    private const int DefaultTakePerBucket = 8;
    private const int MaxTakePerBucket = 50;

    private const int StaleLeadThresholdHours = 48;
    private const int AwaitingReplyThresholdHours = 24;
    private const int NoFollowUpThresholdHours = 24;

    private static readonly string[] ClosedStatuses =
    [
        "Won",
        "Lost",
        "Closed"
    ];

    public LeadAttentionIntelligenceService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<LeadAttentionIntelligenceDto> GetAttentionIntelligenceAsync(
        int businessId,
        int takePerBucket = 8,
        CancellationToken cancellationToken = default)
    {
        if (takePerBucket <= 0)
        {
            takePerBucket = DefaultTakePerBucket;
        }

        if (takePerBucket > MaxTakePerBucket)
        {
            takePerBucket = MaxTakePerBucket;
        }

        var nowUtc = DateTime.UtcNow;
        var staleCutoffUtc = nowUtc.AddHours(-StaleLeadThresholdHours);
        var awaitingReplyCutoffUtc = nowUtc.AddHours(-AwaitingReplyThresholdHours);
        var noFollowUpCutoffUtc = nowUtc.AddHours(-NoFollowUpThresholdHours);

        var baseQuery = _db.Leads
            .AsNoTracking()
            .Where(x =>
                x.BusinessId == businessId &&
                !x.IsArchived &&
                !ClosedStatuses.Contains(x.Status));

        // 1. High Priority Needs Action (highest priority bucket)
        var highPriorityBaseQuery = baseQuery
            .Where(x =>
                x.Priority == "High" &&
                (
                    (x.NextFollowUpAtUtc.HasValue && x.NextFollowUpAtUtc.Value < nowUtc) ||
                    !x.NextFollowUpAtUtc.HasValue ||
                    (x.LastContactAtUtc.HasValue && x.LastContactAtUtc.Value < staleCutoffUtc) ||
                    (!x.LastContactAtUtc.HasValue && x.CreatedAtUtc < staleCutoffUtc)
                ))
            .OrderBy(x => x.NextFollowUpAtUtc ?? x.LastContactAtUtc ?? x.CreatedAtUtc);

        var highPriorityCount = await highPriorityBaseQuery.CountAsync(cancellationToken);

        var highPriorityEntities = await highPriorityBaseQuery
            .Take(takePerBucket)
            .ToListAsync(cancellationToken);

        var highPriorityItems = highPriorityEntities
            .Select(x =>
            {
                var reason =
                    x.NextFollowUpAtUtc.HasValue && x.NextFollowUpAtUtc.Value < nowUtc
                        ? "High priority lead has an overdue follow-up"
                        : !x.NextFollowUpAtUtc.HasValue
                            ? "High priority lead has no follow-up scheduled"
                            : "High priority lead needs action due to inactivity";

                return MapLeadAttentionItem(
                    x,
                    reason,
                    nowUtc,
                    x.NextFollowUpAtUtc ?? x.LastContactAtUtc ?? x.CreatedAtUtc);
            })
            .ToList();

        var highPriorityLeadIds = await highPriorityBaseQuery
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        // 2. Awaiting Reply
        var awaitingReplyBaseQuery = baseQuery
            .Where(x =>
                !highPriorityLeadIds.Contains(x.Id) &&
                x.LastContactAtUtc.HasValue &&
                x.LastContactAtUtc.Value < awaitingReplyCutoffUtc &&
                (
                    !x.LastIncomingAtUtc.HasValue ||
                    x.LastIncomingAtUtc.Value < x.LastContactAtUtc.Value
                ))
            .OrderBy(x => x.LastContactAtUtc);

        var awaitingReplyCount = await awaitingReplyBaseQuery.CountAsync(cancellationToken);

        var awaitingReplyEntities = await awaitingReplyBaseQuery
            .Take(takePerBucket)
            .ToListAsync(cancellationToken);

        var awaitingReplyItems = awaitingReplyEntities
            .Select(x => MapLeadAttentionItem(
                x,
                "Customer has not replied after your last contact",
                nowUtc,
                x.LastContactAtUtc ?? x.CreatedAtUtc))
            .ToList();

        var awaitingReplyLeadIds = await awaitingReplyBaseQuery
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        // 3. Stale Leads
        var staleLeadsBaseQuery = baseQuery
            .Where(x =>
                !highPriorityLeadIds.Contains(x.Id) &&
                !awaitingReplyLeadIds.Contains(x.Id) &&
                (
                    (x.LastContactAtUtc.HasValue && x.LastContactAtUtc.Value < staleCutoffUtc) ||
                    (!x.LastContactAtUtc.HasValue && x.CreatedAtUtc < staleCutoffUtc)
                ))
            .OrderBy(x => x.LastContactAtUtc ?? x.CreatedAtUtc);

        var staleLeadsCount = await staleLeadsBaseQuery.CountAsync(cancellationToken);

        var staleLeadEntities = await staleLeadsBaseQuery
            .Take(takePerBucket)
            .ToListAsync(cancellationToken);

        var staleLeadItems = staleLeadEntities
            .Select(x => MapLeadAttentionItem(
                x,
                x.LastContactAtUtc.HasValue
                    ? "No outbound contact in the last 48 hours"
                    : "Lead created but never contacted for 48+ hours",
                nowUtc,
                x.LastContactAtUtc ?? x.CreatedAtUtc))
            .ToList();

        var staleLeadIds = await staleLeadsBaseQuery
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        // 4. No Follow-up Scheduled
        var noFollowUpBaseQuery = baseQuery
            .Where(x =>
                !highPriorityLeadIds.Contains(x.Id) &&
                !awaitingReplyLeadIds.Contains(x.Id) &&
                !staleLeadIds.Contains(x.Id) &&
                !x.NextFollowUpAtUtc.HasValue &&
                (
                    x.LastIncomingAtUtc.HasValue ||
                    x.LastContactAtUtc.HasValue ||
                    x.CreatedAtUtc < noFollowUpCutoffUtc
                ))
            .OrderByDescending(x => x.Priority == "High")
            .ThenBy(x => x.LastIncomingAtUtc ?? x.LastContactAtUtc ?? x.CreatedAtUtc);

        var noFollowUpCount = await noFollowUpBaseQuery.CountAsync(cancellationToken);

        var noFollowUpEntities = await noFollowUpBaseQuery
            .Take(takePerBucket)
            .ToListAsync(cancellationToken);

        var noFollowUpItems = noFollowUpEntities
            .Select(x => MapLeadAttentionItem(
                x,
                "No follow-up is scheduled for this active lead",
                nowUtc,
                x.LastIncomingAtUtc ?? x.LastContactAtUtc ?? x.CreatedAtUtc))
            .ToList();

        return new LeadAttentionIntelligenceDto
        {
            GeneratedAtUtc = nowUtc,
            StaleLeadThresholdHours = StaleLeadThresholdHours,
            AwaitingReplyThresholdHours = AwaitingReplyThresholdHours,
            NoFollowUpThresholdHours = NoFollowUpThresholdHours,

            StaleLeads = new LeadAttentionBucketDto
            {
                Count = staleLeadsCount,
                Items = staleLeadItems
            },

            AwaitingReply = new LeadAttentionBucketDto
            {
                Count = awaitingReplyCount,
                Items = awaitingReplyItems
            },

            NoFollowUpScheduled = new LeadAttentionBucketDto
            {
                Count = noFollowUpCount,
                Items = noFollowUpItems
            },

            HighPriorityNeedsAction = new LeadAttentionBucketDto
            {
                Count = highPriorityCount,
                Items = highPriorityItems
            }
        };
    }

    private static LeadAttentionItemDto MapLeadAttentionItem(
        Lead lead,
        string reason,
        DateTime nowUtc,
        DateTime referenceUtc)
    {
        var ageHours = (int)Math.Max(0, Math.Floor((nowUtc - referenceUtc).TotalHours));

        return new LeadAttentionItemDto
        {
            LeadId = lead.Id,
            FullName = lead.FullName,
            Email = lead.Email,
            Phone = lead.Phone,
            CompanyName = lead.CompanyName,
            Source = lead.Source,
            Status = lead.Status,
            Priority = lead.Priority,
            EstimatedValue = lead.EstimatedValue,
            InquirySummary = lead.InquirySummary,
            LastIncomingMessagePreview = lead.LastIncomingMessagePreview,
            LastContactAtUtc = lead.LastContactAtUtc,
            LastReplyAtUtc = lead.LastReplyAtUtc,
            LastIncomingAtUtc = lead.LastIncomingAtUtc,
            NextFollowUpAtUtc = lead.NextFollowUpAtUtc,
            CreatedAtUtc = lead.CreatedAtUtc,
            UpdatedAtUtc = lead.UpdatedAtUtc,
            Reason = reason,
            AgeHours = ageHours
        };
    }
}