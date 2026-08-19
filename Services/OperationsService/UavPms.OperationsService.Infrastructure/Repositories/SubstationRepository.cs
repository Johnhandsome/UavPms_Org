using Microsoft.EntityFrameworkCore;
using NetTopologySuite;
using NetTopologySuite.Geometries;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Interfaces.Repositories;
using UavPms.OperationsService.Infrastructure.Persistence;

namespace UavPms.OperationsService.Infrastructure.Repositories;

public class SubstationRepository : GenericRepository<Substation>, ISubstationRepository
{
    public SubstationRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<(IReadOnlyList<Substation> Items, int TotalCount)> GetSubstationsPagedAsync(int page, int pageSize, Guid? regionAssetId, string? searchTerm)
    {
        var query = _context.Substations.Where(s => !s.IsDeleted);

        if (regionAssetId.HasValue)
        {
            query = query.Where(s => s.RegionAssetId == regionAssetId.Value);
        }

        if (!string.IsNullOrEmpty(searchTerm))
        {
            query = query.Where(s => s.SubstationName.Contains(searchTerm));
        }

        int totalCount = await query.CountAsync();

        var items = await query
            .OrderBy(s => s.SubstationName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();
        
        return (items, totalCount);
    }

    public async Task<IReadOnlyList<Substation>> GetSubstationsInBoundingBoxAsync(double minLat, double minLng, double maxLat, double maxLng)
    {
        var geometryFactory = NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);
        var envelope = new Envelope(minLng, maxLng, minLat, maxLat);
        var box = geometryFactory.ToGeometry(envelope);
        
        return await _context.Substations
            .Where(s => !s.IsDeleted && s.Geom != null && s.Geom.Within(box))
            .ToListAsync();
    }
}