using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using NetTopologySuite;
using NetTopologySuite.Geometries;
using UavPms.Core.Entities;
using UavPms.Core.Interfaces.Repositories;
using UavPms.Infrastructure.Persistence;

namespace UavPms.Infrastructure.Repositories;

public class AssetRepository : GenericRepository<Asset>, IAssetRepository
{
    public AssetRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<Asset>> GetAssetsInBoundingBoxAsync(double minLat, double minLng, double maxLat, double maxLng)
    {
        var geometryFactory = NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);
        var envelope = new Envelope(minLng, maxLng, minLat, maxLat);
        var box = geometryFactory.ToGeometry(envelope);

        return await _context.Assets
            .Include(a => a.Tower)
            .Where(a => a.Tower != null && a.Tower.Geom != null && a.Tower.Geom.Within(box))
            .ToListAsync();
    }

    public async Task<IReadOnlyList<Asset>> GetAssetsWithinDistanceAsync(double latitude, double longitude, double distanceInMeters)
    {
        return await _context.Assets
            .FromSqlInterpolated($@"
                SELECT a.* 
                FROM ""Assets"" a 
                JOIN ""Towers"" t ON a.""TowerId"" = t.""Id"" 
                WHERE a.""IsDeleted"" = false 
                  AND t.""IsDeleted"" = false 
                  AND ST_DWithin(t.""Geom""::geography, ST_SetSRID(ST_Point({longitude}, {latitude}), 4326)::geography, {distanceInMeters})")
            .Include(a => a.Tower)
            .ToListAsync();
    }
}