using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace UavPms.OperationsService.Application.Features.Inspections.ChunkedUpload;

public interface IChunkedMediaUploadService
{
    Task<InitChunkedUploadResponse> InitAsync(InitChunkedUploadRequest request, Guid userId, CancellationToken ct = default);
    Task<UploadChunkResponse> UploadChunkAsync(Guid uploadId, int chunkIndex, Stream chunkStream, long chunkLength, Guid userId, CancellationToken ct = default);
    Task<AssembledChunkedUpload> AssembleAsync(Guid uploadId, Guid userId, CancellationToken ct = default);
    Task CleanupAsync(Guid uploadId, CancellationToken ct = default);
}

public record InitChunkedUploadRequest(
    Guid MissionId,
    Guid? TowerId,
    string FileName,
    string ContentType,
    long TotalSizeBytes,
    int TotalChunks,
    DateTime? CapturedAt = null,
    double? Latitude = null,
    double? Longitude = null);

public record InitChunkedUploadResponse(
    Guid UploadId,
    long RecommendedChunkSizeBytes,
    long MaxChunkSizeBytes,
    long MaxTotalSizeBytes,
    int TotalChunks,
    DateTime ExpiresAtUtc);

public record UploadChunkResponse(
    Guid UploadId,
    int ChunkIndex,
    long ChunkSizeBytes,
    int UploadedChunksCount,
    int TotalChunks);

public record ChunkedUploadManifest(
    Guid UploadId,
    Guid UserId,
    Guid MissionId,
    Guid? TowerId,
    string FileName,
    string ContentType,
    long TotalSizeBytes,
    int TotalChunks,
    DateTime CapturedAt,
    double? Latitude,
    double? Longitude,
    DateTime CreatedAtUtc,
    DateTime ExpiresAtUtc);

public record AssembledChunkedUpload(
    ChunkedUploadManifest Manifest,
    string AssembledFilePath);
