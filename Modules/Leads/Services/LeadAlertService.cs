using Microsoft.EntityFrameworkCore;
using SaaSForge.Api.Data;
using SaaSForge.Api.Modules.Leads.Dtos;
using SaaSForge.Api.Modules.Leads.Entities;
using SaaSForge.Api.Modules.Leads.Interfaces;

namespace SaaSForge.Api.Modules.Leads.Services;

public sealed class LeadAlertService : ILeadAlertService
{
    private readonly AppDbContext _db;

    private static readonly string[] ClosedStatuses =
    [
        "Won",
        "Lost",
        "Closed"
    ];

    public LeadAlertService(AppDbContext db)
    {
        _db = db;
    }

    public async Task GenerateAlertsAsync(
        int businessId,
        CancellationToken cancellationToken = default)
    {
        var nowUtc = DateTime.UtcNow;
        var awaitingReplyCutoffUtc = nowUtc.AddHours(-24);
        var staleCutoffUtc = nowUtc.AddHours(-48);
        var noFollowUpCutoffUtc = nowUtc.AddHours(-24);

        var leads = await _db.Leads
            .Where(x =>
                x.BusinessId == businessId &&
                !x.IsArchived &&
                !ClosedStatuses.Contains(x.Status))
            .ToListAsync(cancellationToken);

        foreach (var lead in leads)
        {
            var isFollowUpMissed =
                lead.NextFollowUpAtUtc.HasValue &&
                lead.NextFollowUpAtUtc.Value < nowUtc;

            var isAwaitingReply =
                lead.LastContactAtUtc.HasValue &&
                lead.LastContactAtUtc.Value < awaitingReplyCutoffUtc &&
                (
                    !lead.LastIncomingAtUtc.HasValue ||
                    lead.LastIncomingAtUtc.Value < lead.LastContactAtUtc.Value
                );

            var isStaleLead =
                (lead.LastContactAtUtc.HasValue && lead.LastContactAtUtc.Value < staleCutoffUtc) ||
                (!lead.LastContactAtUtc.HasValue && lead.CreatedAtUtc < staleCutoffUtc);

            var isNoFollowUpScheduled =
                !lead.NextFollowUpAtUtc.HasValue &&
                (
                    lead.LastIncomingAtUtc.HasValue ||
                    lead.LastContactAtUtc.HasValue ||
                    lead.CreatedAtUtc < noFollowUpCutoffUtc
                );

            await SyncAlertAsync(
                lead,
                "FollowUpMissed",
                isFollowUpMissed,
                "Follow-up is overdue for this lead.",
                "High",
                cancellationToken);

            await SyncAlertAsync(
                lead,
                "AwaitingReply",
                isAwaitingReply,
                "Customer has not replied after your last contact.",
                lead.Priority == "High" ? "High" : "Medium",
                cancellationToken);

            await SyncAlertAsync(
                lead,
                "StaleLead",
                isStaleLead,
                lead.LastContactAtUtc.HasValue
                    ? "No outbound contact has happened for more than 48 hours."
                    : "Lead was created but never contacted for more than 48 hours.",
                lead.Priority == "High" ? "High" : "Medium",
                cancellationToken);

            await SyncAlertAsync(
                lead,
                "NoFollowUpScheduled",
                isNoFollowUpScheduled,
                "No follow-up is scheduled for this active lead.",
                lead.Priority == "High" ? "High" : "Medium",
                cancellationToken);
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    public async Task<LeadAlertsResponseDto> GetActiveAlertsAsync(
        int businessId,
        int take = 20,
        CancellationToken cancellationToken = default)
    {
        if (take <= 0)
        {
            take = 20;
        }

        if (take > 100)
        {
            take = 100;
        }

        var alerts = await _db.Set<LeadAlert>()
            .AsNoTracking()
            .Include(x => x.Lead)
            .Where(x =>
                x.BusinessId == businessId &&
                !x.IsResolved)
            .ToListAsync(cancellationToken);

        var grouped = alerts
            .GroupBy(x => x.LeadId)
            .Select(group =>
            {
                var lead = group.First().Lead;

                var orderedSignals = group
                    .OrderByDescending(x => GetSeverityRank(x.Severity))
                    .ThenByDescending(x => GetAlertTypeRank(x.Type))
                    .ThenByDescending(x => x.CreatedAtUtc)
                    .ToList();

                var primary = orderedSignals.First();

                return new LeadAlertGroupItemDto
                {
                    LeadId = lead.Id,
                    FullName = lead.FullName,
                    Email = lead.Email,
                    Phone = lead.Phone,
                    CompanyName = lead.CompanyName,
                    Status = lead.Status,
                    Priority = lead.Priority,
                    Source = lead.Source,

                    PrimaryType = primary.Type,
                    PrimaryMessage = primary.Message,
                    PrimarySeverity = primary.Severity,
                    PrimaryCreatedAtUtc = primary.CreatedAtUtc,

                    TotalSignals = orderedSignals.Count,
                    AdditionalSignalsCount = Math.Max(0, orderedSignals.Count - 1),

                    Signals = orderedSignals
                        .Select(x => new LeadAlertSignalDto
                        {
                            AlertId = x.Id,
                            Type = x.Type,
                            Message = x.Message,
                            Severity = x.Severity,
                            CreatedAtUtc = x.CreatedAtUtc
                        })
                        .ToList()
                };
            })
            .OrderByDescending(x => GetSeverityRank(x.PrimarySeverity))
            .ThenByDescending(x => GetAlertTypeRank(x.PrimaryType))
            .ThenByDescending(x => x.PrimaryCreatedAtUtc)
            .Take(take)
            .ToList();

        var highSeverityCount = grouped.Count(x =>
            string.Equals(x.PrimarySeverity, "High", StringComparison.OrdinalIgnoreCase));

        return new LeadAlertsResponseDto
        {
            GeneratedAtUtc = DateTime.UtcNow,
            Count = grouped.Count,
            HighSeverityCount = highSeverityCount,
            Items = grouped
        };
    }

    public async Task ResolveAlertAsync(
    int businessId,
    Guid alertId,
    CancellationToken cancellationToken = default)
    {
        var alert = await _db.Set<LeadAlert>()
            .FirstOrDefaultAsync(x =>
                x.Id == alertId &&
                x.BusinessId == businessId,
                cancellationToken);

        if (alert is null)
        {
            throw new InvalidOperationException("Lead alert not found.");
        }

        if (!alert.IsResolved)
        {
            alert.IsResolved = true;
            alert.ResolvedAtUtc = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task AcknowledgeAllAlertsForLeadAsync(
        int businessId,
        Guid leadId,
        int suppressHours = 12,
        CancellationToken cancellationToken = default)
    {
        if (suppressHours <= 0)
        {
            suppressHours = 12;
        }

        var alerts = await _db.Set<LeadAlert>()
            .Where(x =>
                x.BusinessId == businessId &&
                x.LeadId == leadId &&
                !x.IsResolved)
            .ToListAsync(cancellationToken);

        if (alerts.Count == 0)
        {
            return;
        }

        var nowUtc = DateTime.UtcNow;
        var suppressedUntilUtc = nowUtc.AddHours(suppressHours);

        foreach (var alert in alerts)
        {
            alert.IsResolved = true;
            alert.ResolvedAtUtc = nowUtc;
            alert.AcknowledgedAtUtc = nowUtc;
            alert.SuppressedUntilUtc = suppressedUntilUtc;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task SyncAlertAsync(
        Lead lead,
        string type,
        bool conditionIsTrue,
        string message,
        string severity,
        CancellationToken cancellationToken)
    {
        var nowUtc = DateTime.UtcNow;

        var activeAlert = await _db.Set<LeadAlert>()
            .FirstOrDefaultAsync(x =>
                x.LeadId == lead.Id &&
                x.Type == type &&
                !x.IsResolved,
                cancellationToken);

        if (!conditionIsTrue)
        {
            if (activeAlert is not null)
            {
                activeAlert.IsResolved = true;
                activeAlert.ResolvedAtUtc = nowUtc;
            }

            return;
        }

        if (activeAlert is not null)
        {
            if (activeAlert.Message != message)
            {
                activeAlert.Message = message;
            }

            if (activeAlert.Severity != severity)
            {
                activeAlert.Severity = severity;
            }

            return;
        }

        var suppressedAlertExists = await _db.Set<LeadAlert>().AnyAsync(x =>
            x.LeadId == lead.Id &&
            x.Type == type &&
            x.IsResolved &&
            x.SuppressedUntilUtc.HasValue &&
            x.SuppressedUntilUtc.Value > nowUtc,
            cancellationToken);

        if (suppressedAlertExists)
        {
            return;
        }

        _db.Set<LeadAlert>().Add(new LeadAlert
        {
            Id = Guid.NewGuid(),
            LeadId = lead.Id,
            BusinessId = lead.BusinessId,
            Type = type,
            Message = message,
            Severity = severity,
            IsResolved = false,
            CreatedAtUtc = nowUtc
        });
    }

    private static int GetSeverityRank(string severity)
    {
        return severity switch
        {
            "High" => 3,
            "Medium" => 2,
            "Low" => 1,
            _ => 0
        };
    }

    private static int GetAlertTypeRank(string type)
    {
        return type switch
        {
            "FollowUpMissed" => 4,
            "AwaitingReply" => 3,
            "StaleLead" => 2,
            "NoFollowUpScheduled" => 1,
            _ => 0
        };
    }
}