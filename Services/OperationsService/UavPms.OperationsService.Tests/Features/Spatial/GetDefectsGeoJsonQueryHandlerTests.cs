using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using NetTopologySuite;
using NetTopologySuite.Geometries;
using UavPms.OperationsService.Application.Features.Spatial.Queries.GetDefectsGeoJson;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Interfaces.Repositories;
using Xunit;

namespace UavPms.OperationsService.Tests.Features.Spatial;

public class GetDefectsGeoJsonQueryHandlerTests
{
    private readonly Mock<IAnomalyRepository> _anomalyRepositoryMock;
    private readonly GetDefectsGeoJsonQueryHandler _handler;

    public GetDefectsGeoJsonQueryHandlerTests()
    {
        _anomalyRepositoryMock = new Mock<IAnomalyRepository>();
        _handler = new GetDefectsGeoJsonQueryHandler(_anomalyRepositoryMock.Object);
    }

    [Fact]
    public async Task Handle_ShouldReturnValidGeoJsonFeatureCollection_WhenAnomaliesExist()
    {
        // Arrange
        var gf = NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);
        var towerGeom = gf.CreatePoint(new Coordinate(106.65, 10.75));

        var tower = new Tower { Id = Guid.NewGuid(), TowerCode = "T-100", Geom = towerGeom };
        var asset = new Asset { Id = Guid.NewGuid(), AssetCode = "AST-01", Tower = tower };
        var category = new DefectCategory { Id = 1, CategoryCode = "CORR", CategoryName = "Corrosion", SeverityWeight = 0.8, IsEmergencyClass = false };

        var anomalies = new List<DetectedAnomaly>
        {
            new DetectedAnomaly
            {
                Id = Guid.NewGuid(),
                AssetId = asset.Id,
                Asset = asset,
                CategoryId = category.Id,
                Category = category,
                ConfidenceScore = 0.95,
                ValidationStatus = "Confirmed"
            }
        };

        _anomalyRepositoryMock
            .Setup(r => r.GetActiveAnomaliesWithSpatialLocationAsync())
            .ReturnsAsync(anomalies);

        var query = new GetDefectsGeoJsonQuery();

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.Type.Should().Be("FeatureCollection");
        result.Features.Should().HaveCount(1);
        result.Features[0].Geometry.Coordinates[0].Should().Be(106.65);
        result.Features[0].Geometry.Coordinates[1].Should().Be(10.75);
        result.Features[0].Properties["assetCode"].Should().Be("AST-01");
        result.Features[0].Properties["categoryCode"].Should().Be("CORR");
    }
}
