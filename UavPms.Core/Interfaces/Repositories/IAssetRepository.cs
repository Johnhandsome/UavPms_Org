using UavPms.Core.Entities;

namespace UavPms.Core.Interfaces.Repositories;

public interface IAssetRepository : IGenericRepository<Asset>
{
    Task<IReadOnlyList<Asset>> GetAssetsInBoundingBoxAsync(double minLat, double minLng, double maxLat, double maxLng);
    Task<IReadOnlyList<Asset>> GetAssetsWithinDistanceAsync(double latitude, double longitude, double distanceInMeters);
}