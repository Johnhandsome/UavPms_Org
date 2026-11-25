using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UavPms.OperationsService.Application.Common.Exceptions;
using UavPms.OperationsService.Application.Features.Inspections.ChunkedUpload;
using UavPms.OperationsService.Application.Features.Inspections.Commands.UploadImage;
using UavPms.OperationsService.Application.Features.Inspections.Queries.GetReportById;
using UavPms.OperationsService.Application.Features.Inspections.Queries.GetByMission;
using UavPms.OperationsService.Domain.Interfaces.Services;
using UavPms.Shared.Contracts.Constants;

namespace UavPms.OperationsService.API.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/inspections")]
[ApiVersion("1.0")]
[Authorize]
public class InspectionController : ControllerBase
{
    private readonly ISender _mediator;
    private readonly IChunkedMediaUploadService? _chunkedUploadService;
    private readonly ICurrentUserServices? _currentUser;

    public InspectionController(
        ISender mediator,
        IChunkedMediaUploadService? chunkedUploadService = null,
        ICurrentUserServices? currentUser = null)
    {
        _mediator = mediator;
        _chunkedUploadService = chunkedUploadService;
        _currentUser = currentUser;
    }

    /// <summary>
    /// Upload ảnh kiểm tra chuyến bay
    /// </summary>
    [HttpPost("upload")]
    [Authorize(Roles = UserRoles.AdminManagerInspector)]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadImage(
        [FromForm] Guid missionId,
        [FromForm] Guid? towerId,
        [FromForm] DateTime capturedAt,
        [FromForm] double? latitude,
        [FromForm] double? longitude,
        IFormFile file,
        CancellationToken cancellationToken = default)
    {
        await using var stream = file?.OpenReadStream() ?? Stream.Null;

        var command = new UploadInspectionImageCommand
        {
            MissionId = missionId,
            TowerId = towerId,
            CapturedAt = capturedAt,
            Latitude = latitude,
            Longitude = longitude,
            FileStream = stream,
            FileName = file?.FileName ?? string.Empty,
            ContentType = file?.ContentType ?? string.Empty,
        };

        var result = await _mediator.Send(command, cancellationToken);

        return Ok(new ApiResponse(true, "Image uploaded successfully.", result));
    }

    /// <summary>
    /// Khởi tạo phiên upload video theo từng chunk (~40MB/chunk) để mỗi request luôn dưới 50MB
    /// </summary>
    [HttpPost("upload/chunks/init")]
    [Authorize(Roles = UserRoles.AdminManagerInspector)]
    public async Task<IActionResult> InitChunkedUpload(
        [FromBody] InitChunkedUploadRequest request,
        CancellationToken cancellationToken = default)
    {
        if (_chunkedUploadService == null)
            return BadRequest(new ApiResponse(false, "Chunked upload service unavailable."));

        var result = await _chunkedUploadService.InitAsync(request, _currentUser?.UserId ?? Guid.Empty, cancellationToken);
        return Ok(new ApiResponse(true, "Chunked upload session initialized.", result));
    }

    /// <summary>
    /// Tải lên một chunk video (tối đa 40MB/chunk)
    /// </summary>
    [HttpPost("upload/chunks/{uploadId:guid}")]
    [Authorize(Roles = UserRoles.AdminManagerInspector)]
    [Consumes("multipart/form-data")]
    public async Task<IActionResult> UploadChunk(
        [FromRoute] Guid uploadId,
        [FromForm] int chunkIndex,
        IFormFile file,
        CancellationToken cancellationToken = default)
    {
        if (_chunkedUploadService == null)
            return BadRequest(new ApiResponse(false, "Chunked upload service unavailable."));

        await using var stream = file?.OpenReadStream() ?? Stream.Null;
        var result = await _chunkedUploadService.UploadChunkAsync(
            uploadId,
            chunkIndex,
            stream,
            file?.Length ?? 0,
            _currentUser?.UserId ?? Guid.Empty,
            cancellationToken);

        return Ok(new ApiResponse(true, $"Chunk {chunkIndex} uploaded successfully.", result));
    }

    /// <summary>
    /// Ghép toàn bộ các chunk thành video hoàn chỉnh, lưu lên MinIO và đưa vào hàng đợi AI Cloud
    /// </summary>
    [HttpPost("upload/chunks/{uploadId:guid}/complete")]
    [Authorize(Roles = UserRoles.AdminManagerInspector)]
    public async Task<IActionResult> CompleteChunkedUpload(
        [FromRoute] Guid uploadId,
        CancellationToken cancellationToken = default)
    {
        if (_chunkedUploadService == null)
            return BadRequest(new ApiResponse(false, "Chunked upload service unavailable."));

        var assembled = await _chunkedUploadService.AssembleAsync(uploadId, _currentUser?.UserId ?? Guid.Empty, cancellationToken);
        try
        {
            await using var fileStream = new FileStream(
                assembled.AssembledFilePath,
                FileMode.Open,
                FileAccess.Read,
                FileShare.Read,
                81920,
                useAsync: true);

            var command = new UploadInspectionImageCommand
            {
                MissionId = assembled.Manifest.MissionId,
                TowerId = assembled.Manifest.TowerId,
                CapturedAt = assembled.Manifest.CapturedAt,
                Latitude = assembled.Manifest.Latitude,
                Longitude = assembled.Manifest.Longitude,
                FileStream = fileStream,
                FileName = assembled.Manifest.FileName,
                ContentType = assembled.Manifest.ContentType
            };

            var result = await _mediator.Send(command, cancellationToken);
            return Ok(new ApiResponse(true, "Chunked media assembled and uploaded successfully.", result));
        }
        finally
        {
            await _chunkedUploadService.CleanupAsync(uploadId, cancellationToken);
        }
    }

    /// <summary>
    /// Hủy phiên upload chunk và xóa file tạm
    /// </summary>
    [HttpDelete("upload/chunks/{uploadId:guid}")]
    [Authorize(Roles = UserRoles.AdminManagerInspector)]
    public async Task<IActionResult> AbortChunkedUpload(
        [FromRoute] Guid uploadId,
        CancellationToken cancellationToken = default)
    {
        if (_chunkedUploadService == null)
            return BadRequest(new ApiResponse(false, "Chunked upload service unavailable."));

        await _chunkedUploadService.CleanupAsync(uploadId, cancellationToken);
        return Ok(new ApiResponse(true, "Chunked upload session aborted and cleaned up."));
    }

    /// <summary>
    /// Lấy báo cáo kiểm tra theo ID
    /// </summary>
    [HttpGet("report/{id:guid}")]
    [Authorize(Roles = UserRoles.AdminManagerInspectorAnalyst)]
    public async Task<IActionResult> GetReportById(Guid id, CancellationToken cancellationToken = default)
    {
        try
        {
            var result = await _mediator.Send(new GetInspectionReportByIdQuery(id), cancellationToken);
            return Ok(new ApiResponse(true, "Inspection report retrieved successfully.", result));
        }
        catch (NotFoundException exception)
        {
            return NotFound(new ApiResponse(false, exception.Message));
        }
    }

    /// <summary>
    /// Lấy lịch sử kiểm tra theo mã nhiệm vụ
    /// </summary>
    [HttpGet("mission/{missionId:guid}")]
    [Authorize(Roles = UserRoles.AdminManagerInspectorAnalyst)]
    public async Task<IActionResult> GetByMission(Guid missionId, CancellationToken cancellationToken = default)
    {
        var result = await _mediator.Send(new GetInspectionsByMissionQuery(missionId), cancellationToken);
        return Ok(new ApiResponse(true, "Mission inspection history retrieved successfully.", result));
    }
}
