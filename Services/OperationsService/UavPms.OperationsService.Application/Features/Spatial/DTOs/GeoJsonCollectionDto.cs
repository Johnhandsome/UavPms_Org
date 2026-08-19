namespace UavPms.OperationsService.Application.Features.Spatial.DTOs;

public class GeoJsonFeatureCollectionDto
{
    public string Type { get; set; } = "FeatureCollection";
    public List<GeoJsonFeatureDto> Features { get; set; } = new();
}

public class GeoJsonFeatureDto
{
    public string Type { get; set; } = "Feature";
    public GeoJsonGeometryDto Geometry { get; set; } = new();
    public Dictionary<string, object?> Properties { get; set; } = new();
}

public class GeoJsonGeometryDto
{
    public string Type { get; set; } = "Point";
    public double[] Coordinates { get; set; } = new double[2];
}