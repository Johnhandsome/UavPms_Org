using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using UavPms.OperationsService.Application.Common.Exceptions;
using UavPms.OperationsService.Application.Features.Inspections.ChunkedUpload;
using UavPms.OperationsService.Infrastructure.Services;
using Xunit;

namespace UavPms.OperationsService.Tests.Features.Inspections;

public class ChunkedMediaUploadServiceTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), $"uavpms_chunk_test_{Guid.NewGuid():N}");

    private ChunkedMediaUploadService CreateService(long maxChunkBytes = 40L * 1024 * 1024, long maxTotalBytes = 500L * 1024 * 1024)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["InspectionMedia:MaxChunkSizeBytes"] = maxChunkBytes.ToString(),
                ["InspectionMedia:MaxVideoSizeBytes"] = maxTotalBytes.ToString()
            })
            .Build();

        return new ChunkedMediaUploadService(config, NullLogger<ChunkedMediaUploadService>.Instance, _tempRoot);
    }

    [Fact]
    public async Task FullChunkedUploadFlow_ShouldAssembleChunksByteForByte_AndCleanup()
    {
        var service = CreateService(maxChunkBytes: 1024, maxTotalBytes: 4096);
        var userId = Guid.NewGuid();
        var missionId = Guid.NewGuid();

        var chunk0 = Enumerable.Repeat((byte)0xAA, 1024).ToArray();
        var chunk1 = Enumerable.Repeat((byte)0xBB, 1024).ToArray();
        var chunk2 = Enumerable.Repeat((byte)0xCC, 512).ToArray();
        var expected = chunk0.Concat(chunk1).Concat(chunk2).ToArray();

        var init = await service.InitAsync(
            new InitChunkedUploadRequest(missionId, null, "corridor.mp4", "video/mp4", expected.Length, 3),
            userId);

        init.MaxChunkSizeBytes.Should().Be(1024);
        init.TotalChunks.Should().Be(3);

        (await service.UploadChunkAsync(init.UploadId, 0, new MemoryStream(chunk0), chunk0.Length, userId)).UploadedChunksCount.Should().Be(1);
        (await service.UploadChunkAsync(init.UploadId, 2, new MemoryStream(chunk2), chunk2.Length, userId)).UploadedChunksCount.Should().Be(2);

        // Completing before chunk 1 is uploaded -> MISSING_CHUNKS
        var missingAct = () => service.AssembleAsync(init.UploadId, userId);
        (await missingAct.Should().ThrowAsync<BusinessRuleException>()).Which.Code.Should().Be("MISSING_CHUNKS");

        (await service.UploadChunkAsync(init.UploadId, 1, new MemoryStream(chunk1), chunk1.Length, userId)).UploadedChunksCount.Should().Be(3);

        var assembled = await service.AssembleAsync(init.UploadId, userId);
        var actualBytes = await File.ReadAllBytesAsync(assembled.AssembledFilePath);
        actualBytes.Should().Equal(expected);

        await service.CleanupAsync(init.UploadId);
        File.Exists(assembled.AssembledFilePath).Should().BeFalse();
    }

    [Fact]
    public async Task ChunkedUpload_ShouldRejectChunkOver40Mb_AndTotalOver500Mb()
    {
        var service = CreateService(); // 40 MiB chunk limit, 500 MiB total limit
        var userId = Guid.NewGuid();
        var missionId = Guid.NewGuid();

        // Total > 500 MiB -> FILE_TOO_LARGE
        var tooLargeTotal = () => service.InitAsync(
            new InitChunkedUploadRequest(missionId, null, "huge.mp4", "video/mp4", 600L * 1024 * 1024, 15),
            userId);
        (await tooLargeTotal.Should().ThrowAsync<BusinessRuleException>()).Which.Code.Should().Be("FILE_TOO_LARGE");

        // 100 MiB file with only 2 chunks (50 MiB/chunk > 40 MiB limit) -> CHUNK_TOO_LARGE
        var tooFewChunks = () => service.InitAsync(
            new InitChunkedUploadRequest(missionId, null, "flight.mp4", "video/mp4", 100L * 1024 * 1024, 2),
            userId);
        (await tooFewChunks.Should().ThrowAsync<BusinessRuleException>()).Which.Code.Should().Be("CHUNK_TOO_LARGE");

        // Valid 3-chunk init for 100 MiB, then uploading a 41 MiB chunk -> CHUNK_TOO_LARGE
        var init = await service.InitAsync(
            new InitChunkedUploadRequest(missionId, null, "flight.mp4", "video/mp4", 100L * 1024 * 1024, 3),
            userId);

        var oversizedChunk = () => service.UploadChunkAsync(
            init.UploadId, 0, new MemoryStream(new byte[] { 1 }), 41L * 1024 * 1024, userId);
        (await oversizedChunk.Should().ThrowAsync<BusinessRuleException>()).Which.Code.Should().Be("CHUNK_TOO_LARGE");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }
}
