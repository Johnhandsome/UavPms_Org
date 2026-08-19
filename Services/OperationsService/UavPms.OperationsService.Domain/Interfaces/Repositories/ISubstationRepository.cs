using UavPms.OperationsService.Domain.Entities;

namespace UavPms.OperationsService.Domain.Interfaces.Repositories;

public interface ISubstationRepository : IGenericRepository<Substation>
{
Task<(IReadOnlyList<Substation> Items, int TotalCount)> GetSubstationsPagedAsync(
        int page,
        int pageSize,
        Guid? regionAssetId,
        string? searchTerm
    );

    Task<IReadOnlyList<Substation>> GetSubstationsInBoundingBoxAsync(double minLat, double minLng, double maxLat,
        double maxLng);
}