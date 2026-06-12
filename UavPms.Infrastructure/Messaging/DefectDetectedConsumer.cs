using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace UavPms.Infrastructure.Messaging;

public class DefectDetectedConsumer(ILogger<DefectDetectedConsumer> logger) : BackgroundService
{
    private readonly ILogger<DefectDetectedConsumer> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("DefectDetectedConsumer background service is starting (Skeleton).");

        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogDebug("DefectDetectedConsumer skeleton is running...");
            try
            {
                await Task.Delay(15000, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }
}