using FluentAssertions;
using Moq;
using NetTopologySuite;
using NetTopologySuite.Geometries;
using UavPms.OperationsService.Application.Features.Spatial.Queries.GetAssetsInBoundingBox;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Interfaces.Repositories;
namespace UavPms.OperationsService.Tests.Features.Spatial;

public class GetAssetsInBoundingBoxQueryHandlerTests
{
    private readonly Mock<ITowerRepository> _towerRepositoryMock;
    private readonly Mock<ISubstationRepository> _substationRepositoryMock;
    private readonly GetAssetsInBoundingBoxQueryHandler _handler;

    public GetAssetsInBoundingBoxQueryHandlerTests()
    {
        _towerRepositoryMock = new Mock<ITowerRepository>();
        _substationRepositoryMock = new Mock<ISubstationRepository>();
        _handler = new GetAssetsInBoundingBoxQueryHandler(_towerRepositoryMock.Object, _substationRepositoryMock.Object);
    }

    [Fact]
    public async Task Handle_ShouldReturnAssets_WhenTowersAndSubstationsExistInBoundingBox()
    {
        // Arrange
        var gf = NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);
        var towerGeom = gf.CreatePoint(new Coordinate(106.65, 10.75));
        var subGeom = gf.CreatePoint(new Coordinate(106.68, 10.78));

        var towers = new List<Tower>
        {
            new Tower { Id = Guid.NewGuid(), TowerCode = "T01", LineAssetId = Guid.NewGuid(), Geom = towerGeom }
        };

        var substations = new List<Substation>
        {
            new Substation { Id = Guid.NewGuid(), SubstationName = "SUB01", RegionAssetId = Guid.NewGuid(), Geom = subGeom }
        };

        _towerRepositoryMock
            .Setup(r => r.GetTowersInBoundingBoxAsync(10.7, 106.6, 10.8, 106.7))
            .ReturnsAsync(towers);

        _substationRepositoryMock
            .Setup(r => r.GetSubstationsInBoundingBoxAsync(10.7, 106.6, 10.8, 106.7))
            .ReturnsAsync(substations);

        var query = new GetAssetsInBoundingBoxQuery(10.7, 106.6, 10.8, 106.7);

        // Act
        var result = await _handler.Handle(query, CancellationToken.None);

        // Assert
        result.Should().NotBeNull();
        result.TotalCount.Should().Be(2);
        result.Assets.Should().ContainSingle(a => a.Type == "Tower" && a.Code == "T01");
        result.Assets.Should().ContainSingle(a => a.Type == "Substation" && a.Code == "SUB01");
    }
}
