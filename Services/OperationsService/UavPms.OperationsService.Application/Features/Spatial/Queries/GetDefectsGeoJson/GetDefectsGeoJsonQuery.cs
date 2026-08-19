using MediatR;
using UavPms.OperationsService.Application.Features.Spatial.DTOs;

namespace UavPms.OperationsService.Application.Features.Spatial.Queries.GetDefectsGeoJson;

public record GetDefectsGeoJsonQuery(
    string? ValidationStatus = null) : IRequest<GeoJsonFeatureCollectionDto>;