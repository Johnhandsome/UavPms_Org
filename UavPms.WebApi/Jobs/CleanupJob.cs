using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace UavPms.WebApi.Jobs;

public class CleanupJob(ILogger<CleanupJob> logger, IConfiguration configuration)
{
    private readonly ILogger<CleanupJob> _logger = logger;
    private readonly IConfiguration _configuration = configuration;

    public Task Execute()
    {
        _logger.LogInformation("Auto-Cleanup job started: Purging temporary files and logs older than 30 days...");

        var imagePath = _configuration["FileStorage:AlertImagesPath"] ?? Path.Combine(Directory.GetCurrentDirectory(), "uav_storage", "images");
        
        try
        {
            if (!string.IsNullOrEmpty(_configuration["FileStorage:AlertImagesPath"]))
            {
                var dir = new DirectoryInfo(imagePath);

                _ = dir.GetFiles();
            }
        }
        catch
        {
            imagePath = Path.Combine(Directory.GetCurrentDirectory(), "uav_storage", "images");
        }

        try
        {
            if (Directory.Exists(imagePath))
            {
                var directoryInfo = new DirectoryInfo(imagePath);
                var thresholdDate = DateTime.UtcNow.AddDays(-30);
                int deletedCount = 0;

                foreach (var file in directoryInfo.GetFiles())
                {
                    if (file.CreationTimeUtc < thresholdDate)
                    {
                        file.Delete();
                        deletedCount++;
                    }
                }

                _logger.LogInformation("Auto-Cleanup complete. Purged {Count} files older than 30 days.", deletedCount);
            }
            else
            {
                _logger.LogWarning("Auto-Cleanup: Storage directory '{Path}' does not exist. Skipping file purge.", imagePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error occurred during Auto-Cleanup job execution.");
        }

        return Task.CompletedTask;
    }
}
