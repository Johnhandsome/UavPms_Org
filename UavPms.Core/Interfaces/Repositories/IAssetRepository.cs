using UavPms.Core.Entities;

namespace UavPms.Core.Interfaces.Repositories;

public interface IAssetRepository : IGenericRepository<Asset>
{
    Task<IReadOnlyList<Asset>> GetAllAssetsInBoundingBoxAsync(double latitude, double longitude);
    Task<IReadOnlyList<Asset>> GetAssetsWithinDistance(double latitude, double longitude);
}