using SaaSForge.Api.Modules.Leads.Dtos;

namespace SaaSForge.Api.Modules.Leads.Interfaces;

public interface ILeadAlertService
{
    Task GenerateAlertsAsync(
        int businessId,
        CancellationToken cancellationToken = default);

    Task<LeadAlertsResponseDto> GetActiveAlertsAsync(
        int businessId,
        int take = 20,
        CancellationToken cancellationToken = default);

    Task ResolveAlertAsync(
        int businessId,
        Guid alertId,
        CancellationToken cancellationToken = default);

    Task AcknowledgeAllAlertsForLeadAsync(
        int businessId,
        Guid leadId,
        int suppressHours = 12,
        CancellationToken cancellationToken = default);
}