using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace UavPms.Infrastructure.Messaging;

public class MissionCreatedConsumer(ILogger<MissionCreatedConsumer> logger) : BackgroundService
{
    private readonly ILogger<MissionCreatedConsumer> _logger = logger;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("MissionCreatedConsumer background service is starting (Skeleton).");

        while (!stoppingToken.IsCancellationRequested)
        {
            _logger.LogInformation("MissionCreatedConsumer is running...");
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
