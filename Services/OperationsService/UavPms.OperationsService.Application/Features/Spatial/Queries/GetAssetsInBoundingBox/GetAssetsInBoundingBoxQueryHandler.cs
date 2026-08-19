using MediatR;
using UavPms.OperationsService.Application.Features.Spatial.DTOs;
using UavPms.OperationsService.Domain.Interfaces.Repositories;

namespace UavPms.OperationsService.Application.Features.Spatial.Queries.GetAssetsInBoundingBox;

public class GetAssetsInBoundingBoxQueryHandler : IRequestHandler<GetAssetsInBoundingBoxQuery, SpatialAssetResponseDto>
{
    private readonly ITowerRepository _towerRepository;
    private readonly ISubstationRepository _substationRepository;

    public GetAssetsInBoundingBoxQueryHandler(ITowerRepository towerRepository,
        ISubstationRepository substationRepository)
    {
        _towerRepository = towerRepository;
        _substationRepository = substationRepository;
    }
    
    public async Task<SpatialAssetResponseDto> Handle(GetAssetsInBoundingBoxQuery request, CancellationToken cancellationToken)
    {
        var resultList = new List<SpatialAssetDto>();

        if (request.IncludeTowers)
        {
            var towers = await _towerRepository.GetTowersInBoundingBoxAsync(
                request.MinLat, request.MinLng, request.MaxLat, request.MaxLng);

            foreach (var tower in towers)
            {
                if (request.LineAssetId.HasValue && tower.LineAssetId != request.LineAssetId.Value) continue;

                double lat = tower.Geom != null ? tower.Geom.Coordinate.Y : 0;
                double lng = tower.Geom != null ? tower.Geom.Coordinate.X : 0;
                
                resultList.Add(new SpatialAssetDto(
                    Id: tower.Id,
                    Code: tower.TowerCode,
                    Type: "Tower",
                    Latitude: lat,
                    Longitude: lng,
                    ParentId: tower.LineAssetId,
                    Status: "Active")
                );
            }
        }

        if (request.IncludeSubstations)
        {
            var substations = await _substationRepository.GetSubstationsInBoundingBoxAsync(
                request.MinLat, request.MinLng, request.MaxLat, request.MaxLng);

            foreach (var sub in substations)
            {
                double lat = sub.Geom != null ? sub.Geom.Coordinate.Y : 0;
                double lng = sub.Geom != null ? sub.Geom.Coordinate.X : 0;
                
                resultList.Add(new SpatialAssetDto(
                    Id: sub.Id,
                    Code: sub.SubstationName,
                    Type: "Substation",
                    Latitude: lat,
                    Longitude: lng,
                    ParentId: sub.RegionAssetId,
                    Status: "Active")
                );
            }
        }
        
        return new SpatialAssetResponseDto(resultList, resultList.Count);
    }
}