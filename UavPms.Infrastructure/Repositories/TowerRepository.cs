using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NetTopologySuite;
using NetTopologySuite.Geometries;
using UavPms.Core.Entities;
using UavPms.Core.Interfaces.Repositories;
using UavPms.Infrastructure.Persistence;

namespace UavPms.Infrastructure.Repositories;

public class TowerRepository : GenericRepository<Tower>, ITowerRepository
{
    public TowerRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<IReadOnlyList<Tower>> GetTowersInBoundingBoxAsync(double minLat, double minLng, double maxLat, double maxLng)
    {
        var geometryFactory = NtsGeometryServices.Instance.CreateGeometryFactory(srid: 4326);
        var envelope = new Envelope(minLng, maxLng, minLat, maxLat);
        var box = geometryFactory.ToGeometry(envelope);

        return await _context.Towers
            .Where(t => t.Geom != null && t.Geom.Within(box))
            .ToListAsync();
    }

    public async Task<IReadOnlyList<Tower>> GetTowersWithinDistanceAsync(double latitude, double longitude, double distanceInMeters)
    {
        return await _context.Towers
            .FromSqlInterpolated($"SELECT * FROM \"Towers\" WHERE \"IsDeleted\" = false AND ST_DWithin(\"Geom\"::geography, ST_SetSRID(ST_Point({longitude}, {latitude}), 4326)::geography, {distanceInMeters})")
            .ToListAsync();
    }
}