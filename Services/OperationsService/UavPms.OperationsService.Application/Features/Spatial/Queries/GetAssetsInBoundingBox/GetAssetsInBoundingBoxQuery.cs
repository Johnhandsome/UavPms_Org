using MediatR;
using UavPms.OperationsService.Application.Features.Spatial.DTOs;

namespace UavPms.OperationsService.Application.Features.Spatial.Queries.GetAssetsInBoundingBox;

public record GetAssetsInBoundingBoxQuery
(
    double MinLat,
    double MinLng,
    double MaxLat,
    double MaxLng,
    Guid? LineAssetId = null,
    bool IncludeTowers = true,
    bool IncludeSubstations = true
) : IRequest<SpatialAssetResponseDto>;