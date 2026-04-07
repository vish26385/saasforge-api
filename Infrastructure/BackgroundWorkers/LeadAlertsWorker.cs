using Microsoft.EntityFrameworkCore;
using SaaSForge.Api.Data;
using SaaSForge.Api.Modules.Leads.Interfaces;

namespace SaaSForge.Api.Infrastructure.BackgroundWorkers;

public sealed class LeadAlertsWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<LeadAlertsWorker> _logger;

    public LeadAlertsWorker(
        IServiceScopeFactory scopeFactory,
        ILogger<LeadAlertsWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("LeadAlertsWorker started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunCycleAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in LeadAlertsWorker cycle.");
            }

            // ⏱️ Run every 60 seconds
            await Task.Delay(TimeSpan.FromSeconds(60), stoppingToken);
        }

        _logger.LogInformation("LeadAlertsWorker stopped.");
    }

    private async Task RunCycleAsync(CancellationToken cancellationToken)
    {
        using var scope = _scopeFactory.CreateScope();

        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var alertService = scope.ServiceProvider.GetRequiredService<ILeadAlertService>();

        // get all active businesses
        var businessIds = await db.Businesses
            .AsNoTracking()
            .Select(x => x.Id)
            .ToListAsync(cancellationToken);

        foreach (var businessId in businessIds)
        {
            try
            {
                await alertService.GenerateAlertsAsync(businessId, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing alerts for business {businessId}");
            }
        }
    }
}