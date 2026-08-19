namespace UavPms.OperationsService.Application.Features.Spatial.DTOs;

public record SpatialAssetDto
(
    Guid Id,
    string Code,
    string Type,
    double Latitude,
    double Longitude,
    Guid? ParentId,
    string? Status
);

public record SpatialAssetResponseDto(
    IReadOnlyList<SpatialAssetDto> Assets,
    int TotalCount
);