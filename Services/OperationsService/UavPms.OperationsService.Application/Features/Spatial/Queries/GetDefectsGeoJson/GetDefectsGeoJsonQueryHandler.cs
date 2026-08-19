using MediatR;
using UavPms.OperationsService.Application.Features.Spatial.DTOs;
using UavPms.OperationsService.Domain.Interfaces.Repositories;

namespace UavPms.OperationsService.Application.Features.Spatial.Queries.GetDefectsGeoJson;

public class GetDefectsGeoJsonQueryHandler : IRequestHandler<GetDefectsGeoJsonQuery, GeoJsonFeatureCollectionDto>
{
    private readonly IAnomalyRepository _anomalyRepository;

    public GetDefectsGeoJsonQueryHandler(IAnomalyRepository anomalyRepository)
    {
        _anomalyRepository = anomalyRepository;
    }
    
    public async Task<GeoJsonFeatureCollectionDto> Handle(GetDefectsGeoJsonQuery request, CancellationToken cancellationToken)
    {
        var anomalies = await _anomalyRepository.GetActiveAnomaliesWithSpatialLocationAsync();
        var collection = new GeoJsonFeatureCollectionDto();

        foreach (var anomaly in anomalies)
        {
            if(!string.IsNullOrEmpty(request.ValidationStatus) && !string.Equals(anomaly.ValidationStatus, request.ValidationStatus, StringComparison.OrdinalIgnoreCase)) continue;
            
            var tower = anomaly.Asset?.Tower;
            if (tower?.Geom == null)
            {
                continue;
            }

            double longtitude = tower.Geom.Coordinate.X;
            double latitude = tower.Geom.Coordinate.Y;

            var feature = new GeoJsonFeatureDto
            {
                Type = "Feature",
                Geometry = new GeoJsonGeometryDto
                {
                    Type = "Point",
                    Coordinates = new[] { longtitude, latitude }
                },
                Properties = new Dictionary<string, object?>
                {
                    { "anomalyId", anomaly.Id },
                    { "assetId", anomaly.AssetId },
                    { "assetCode", anomaly.Asset?.AssetCode ?? string.Empty },
                    { "towerId", tower.Id },
                    { "towerCode", tower.TowerCode },
                    { "categoryCode", anomaly.Category?.CategoryCode ?? string.Empty },
                    { "categoryName", anomaly.Category?.CategoryName ?? string.Empty },
                    { "severityWeight", anomaly.Category?.SeverityWeight ?? 0.0 },
                    { "isEmergencyClass", anomaly.Category?.IsEmergencyClass ?? false },
                    { "confidenceScore", anomaly.ConfidenceScore },
                    { "validationStatus", anomaly.ValidationStatus },
                    { "aiSource", anomaly.AiSource },
                    { "validatedAd", anomaly.ValidatedAt }
                }
            };
            
            collection.Features.Add(feature);
        }
        
        return collection;
    }
}