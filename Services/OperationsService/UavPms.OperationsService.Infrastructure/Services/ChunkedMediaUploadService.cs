using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using UavPms.OperationsService.Application.Common.Exceptions;
using UavPms.OperationsService.Application.Features.Inspections.ChunkedUpload;

namespace UavPms.OperationsService.Infrastructure.Services;

/// <summary>
/// Manages resumable 40 MiB chunked uploads on temporary local disk so every HTTP request stays under 50 MiB
/// while supporting assembled inspection videos up to 500 MiB without buffering the full file in RAM.
/// </summary>
public sealed class ChunkedMediaUploadService : IChunkedMediaUploadService
{
    public const long DefaultMaxChunkSizeBytes = 40L * 1024 * 1024;  // 40 MiB per chunk
    public const long DefaultMaxVideoSizeBytes = 500L * 1024 * 1024; // 500 MiB total assembled video
    private const string ManifestFileName = "manifest.json";
    private const string AssembledFileName = "assembled.bin";

    private readonly string _chunksRootPath;
    private readonly long _maxChunkSizeBytes;
    private readonly long _maxTotalSizeBytes;
    private readonly ILogger<ChunkedMediaUploadService> _logger;

    public ChunkedMediaUploadService(
        IConfiguration configuration,
        ILogger<ChunkedMediaUploadService> logger,
        string? customRootPath = null)
    {
        _logger = logger;
        _maxChunkSizeBytes = long.TryParse(configuration["InspectionMedia:MaxChunkSizeBytes"], out var chunkLimit) && chunkLimit > 0
            ? chunkLimit
            : DefaultMaxChunkSizeBytes;
        _maxTotalSizeBytes = long.TryParse(configuration["InspectionMedia:MaxVideoSizeBytes"], out var totalLimit) && totalLimit > 0
            ? totalLimit
            : DefaultMaxVideoSizeBytes;

        _chunksRootPath = !string.IsNullOrWhiteSpace(customRootPath)
            ? customRootPath
            : Path.Combine(Path.GetTempPath(), "uavpms_chunked_uploads");
    }

    public async Task<InitChunkedUploadResponse> InitAsync(
        InitChunkedUploadRequest request,
        Guid userId,
        CancellationToken ct = default)
    {
        if (request.MissionId == Guid.Empty)
            throw new BusinessRuleException("MISSION_ID_REQUIRED", "Mission ID is required.");
        if (string.IsNullOrWhiteSpace(request.FileName))
            throw new BusinessRuleException("FILE_NAME_REQUIRED", "File name is required.");
        if (string.IsNullOrWhiteSpace(request.ContentType))
            throw new BusinessRuleException("CONTENT_TYPE_REQUIRED", "Content type is required.");
        if (request.TotalSizeBytes <= 0)
            throw new BusinessRuleException("INVALID_FILE_SIZE", "TotalSizeBytes must be greater than 0.");
        if (request.TotalSizeBytes > _maxTotalSizeBytes)
            throw new BusinessRuleException("FILE_TOO_LARGE", $"Total file size exceeds the {_maxTotalSizeBytes / 1024 / 1024}MB maximum limit.");
        if (request.TotalChunks <= 0)
            throw new BusinessRuleException("INVALID_TOTAL_CHUNKS", "TotalChunks must be at least 1.");

        var minRequiredChunks = (int)Math.Ceiling((double)request.TotalSizeBytes / _maxChunkSizeBytes);
        if (request.TotalChunks < minRequiredChunks)
            throw new BusinessRuleException("CHUNK_TOO_LARGE", $"Each chunk must be at most {_maxChunkSizeBytes / 1024 / 1024}MB. At least {minRequiredChunks} chunks are required for this file.");

        TryPurgeExpiredSessions();

        var uploadId = Guid.NewGuid();
        var sessionDir = GetSessionDir(uploadId);
        Directory.CreateDirectory(sessionDir);

        var nowUtc = DateTime.UtcNow;
        var expiresAtUtc = nowUtc.AddHours(24);
        var manifest = new ChunkedUploadManifest(
            uploadId,
            userId,
            request.MissionId,
            request.TowerId,
            Path.GetFileName(request.FileName.Trim()),
            request.ContentType.Trim(),
            request.TotalSizeBytes,
            request.TotalChunks,
            request.CapturedAt is { } cap && cap != default ? cap : nowUtc,
            request.Latitude,
            request.Longitude,
            nowUtc,
            expiresAtUtc);

        var manifestJson = JsonSerializer.Serialize(manifest);
        await File.WriteAllTextAsync(Path.Combine(sessionDir, ManifestFileName), manifestJson, ct);

        return new InitChunkedUploadResponse(
            uploadId,
            _maxChunkSizeBytes,
            _maxChunkSizeBytes,
            _maxTotalSizeBytes,
            request.TotalChunks,
            expiresAtUtc);
    }

    public async Task<UploadChunkResponse> UploadChunkAsync(
        Guid uploadId,
        int chunkIndex,
        Stream chunkStream,
        long chunkLength,
        Guid userId,
        CancellationToken ct = default)
    {
        var manifest = await LoadManifestAsync(uploadId, userId, ct);

        if (chunkIndex < 0 || chunkIndex >= manifest.TotalChunks)
            throw new BusinessRuleException("INVALID_CHUNK_INDEX", $"ChunkIndex must be between 0 and {manifest.TotalChunks - 1}.");
        if (chunkStream == null || chunkLength <= 0)
            throw new BusinessRuleException("EMPTY_CHUNK", "Chunk file is empty.");
        if (chunkLength > _maxChunkSizeBytes)
            throw new BusinessRuleException("CHUNK_TOO_LARGE", $"Chunk size exceeds the {_maxChunkSizeBytes / 1024 / 1024}MB limit per request.");

        var sessionDir = GetSessionDir(uploadId);
        var partPath = Path.Combine(sessionDir, $"{chunkIndex:D6}.part");

        await using (var fs = new FileStream(partPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
        {
            await chunkStream.CopyToAsync(fs, ct);
        }

        var writtenLength = new FileInfo(partPath).Length;
        if (writtenLength > _maxChunkSizeBytes)
        {
            File.Delete(partPath);
            throw new BusinessRuleException("CHUNK_TOO_LARGE", $"Chunk size exceeds the {_maxChunkSizeBytes / 1024 / 1024}MB limit per request.");
        }

        var uploadedCount = Directory.GetFiles(sessionDir, "*.part").Length;
        return new UploadChunkResponse(uploadId, chunkIndex, writtenLength, uploadedCount, manifest.TotalChunks);
    }

    public async Task<AssembledChunkedUpload> AssembleAsync(Guid uploadId, Guid userId, CancellationToken ct = default)
    {
        var manifest = await LoadManifestAsync(uploadId, userId, ct);
        var sessionDir = GetSessionDir(uploadId);

        var missingChunks = Enumerable.Range(0, manifest.TotalChunks)
            .Where(i => !File.Exists(Path.Combine(sessionDir, $"{i:D6}.part")))
            .ToList();

        if (missingChunks.Count > 0)
            throw new BusinessRuleException("MISSING_CHUNKS", $"Missing chunk(s): {string.Join(", ", missingChunks)}.");

        var assembledPath = Path.Combine(sessionDir, AssembledFileName);
        long totalWritten = 0;

        await using (var dest = new FileStream(assembledPath, FileMode.Create, FileAccess.Write, FileShare.None, 81920, useAsync: true))
        {
            for (var i = 0; i < manifest.TotalChunks; i++)
            {
                var partPath = Path.Combine(sessionDir, $"{i:D6}.part");
                await using var src = new FileStream(partPath, FileMode.Open, FileAccess.Read, FileShare.Read, 81920, useAsync: true);
                totalWritten += src.Length;
                if (totalWritten > _maxTotalSizeBytes)
                {
                    throw new BusinessRuleException("FILE_TOO_LARGE", $"Assembled file size exceeds the {_maxTotalSizeBytes / 1024 / 1024}MB maximum limit.");
                }
                await src.CopyToAsync(dest, ct);
            }
        }

        if (totalWritten != manifest.TotalSizeBytes)
            throw new BusinessRuleException("SIZE_MISMATCH", $"Assembled size ({totalWritten} bytes) does not match declared TotalSizeBytes ({manifest.TotalSizeBytes} bytes).");

        return new AssembledChunkedUpload(manifest, assembledPath);
    }

    public Task CleanupAsync(Guid uploadId, CancellationToken ct = default)
    {
        var sessionDir = GetSessionDir(uploadId);
        if (Directory.Exists(sessionDir))
        {
            try
            {
                Directory.Delete(sessionDir, recursive: true);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to clean up chunked upload session directory {SessionDir}", sessionDir);
            }
        }
        return Task.CompletedTask;
    }

    private async Task<ChunkedUploadManifest> LoadManifestAsync(Guid uploadId, Guid userId, CancellationToken ct)
    {
        var manifestPath = Path.Combine(GetSessionDir(uploadId), ManifestFileName);
        if (!File.Exists(manifestPath))
            throw new NotFoundException("ChunkedUpload", uploadId);

        var json = await File.ReadAllTextAsync(manifestPath, ct);
        var manifest = JsonSerializer.Deserialize<ChunkedUploadManifest>(json)
            ?? throw new NotFoundException("ChunkedUpload", uploadId);

        if (manifest.ExpiresAtUtc < DateTime.UtcNow)
        {
            await CleanupAsync(uploadId, ct);
            throw new BusinessRuleException("UPLOAD_SESSION_EXPIRED", "Chunked upload session has expired. Please start a new upload.");
        }

        if (userId != Guid.Empty && manifest.UserId != Guid.Empty && manifest.UserId != userId)
            throw new ForbiddenException("You do not own this chunked upload session.");

        return manifest;
    }

    private string GetSessionDir(Guid uploadId) => Path.Combine(_chunksRootPath, uploadId.ToString("N"));

    private void TryPurgeExpiredSessions()
    {
        try
        {
            if (!Directory.Exists(_chunksRootPath)) return;
            var cutoff = DateTime.UtcNow.AddHours(-24);
            foreach (var dir in Directory.GetDirectories(_chunksRootPath))
            {
                if (Directory.GetCreationTimeUtc(dir) < cutoff)
                {
                    Directory.Delete(dir, recursive: true);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogDebug(ex, "Ignored non-fatal error while purging expired chunked upload directories.");
        }
    }
}
