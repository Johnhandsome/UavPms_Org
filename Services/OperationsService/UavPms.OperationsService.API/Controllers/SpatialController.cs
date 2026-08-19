using Asp.Versioning;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using UavPms.OperationsService.Application.Features.Spatial.Queries.GetAssetsInBoundingBox;
using UavPms.OperationsService.Application.Features.Spatial.Queries.GetDefectsGeoJson;
using UavPms.Shared.Contracts.Constants;

namespace UavPms.OperationsService.API.Controllers;

[ApiController]
[Route("api/v{version:apiVersion}/spatial")]
[ApiVersion("1.0")]
[Authorize(Roles = UserRoles.AllAuthenticatedRoles)]
public class SpatialController : ControllerBase
{
    private readonly ISender _mediator;
    
    public SpatialController(ISender mediator)
    {
        _mediator = mediator;
    }

    /// <summary>
    /// Lấy danh sách tài sản (cột điện, trạm biến áp) nằm trong Viewport bản đồ (Bounding Bõx)
    /// </summary>
    [HttpGet("bounding-box")]
    public async Task<IActionResult> GetAssetsInBoundingBox(
        [FromQuery] double minLat,
        [FromQuery] double minLng,
        [FromQuery] double maxLat,
        [FromQuery] double maxLng,
        [FromQuery] Guid? lineAssetId = null,
        [FromQuery] bool includeTowers = true,
        [FromQuery] bool includeSubstations = true, 
        CancellationToken cancellationToken = default)
    {
        var query = new GetAssetsInBoundingBoxQuery(
            MinLat: minLat,
            MinLng: minLng,
            MaxLat: maxLat,
            MaxLng: maxLng,
            LineAssetId: lineAssetId,
            IncludeTowers: includeTowers,
            IncludeSubstations: includeSubstations);
        
        var result = await _mediator.Send(query, cancellationToken);
        return Ok(new ApiResponse(true, "Lấy danh sách tài sản theo khung nhìn bản đồ thành công.", result));
    }

    /// <summary>
    /// Lấy danh sách defect dưới định dạng GeoJSON chuẩn RFC 7946 cho LeafletJS render Heatmap/ Marker cluster.
    /// </summary>
    /// <param name="validationStatus"></param>
    /// <param name="cancellationToken"></param>
    /// <returns></returns>
    [HttpGet("defects-geojson")]
    public async Task<IActionResult> GetDefectsGeoJson([FromQuery] string? validationStatus = null,
        CancellationToken cancellationToken = default)
    {
        var query = new GetDefectsGeoJsonQuery(validationStatus);
        var result = await _mediator.Send(query, cancellationToken);
        return Ok(result);
    }
}