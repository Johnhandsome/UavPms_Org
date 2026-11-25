using Microsoft.EntityFrameworkCore;
using System.Threading.Tasks;
using UavPms.IdentityService.Domain.Entities;
using UavPms.IdentityService.Domain.Interfaces.Repositories;
using UavPms.IdentityService.Infrastructure.Persistence;

namespace UavPms.IdentityService.Infrastructure.Repositories;

public class UserRepository : GenericRepository<User>, IUserRepository
{
    public UserRepository(ApplicationDbContext context) : base(context)
    {
    }

    public async Task<User?> GetByEmailWithRolesAsync(string email)
    {
        if (string.IsNullOrWhiteSpace(email))
        {
            return null;
        }

        var normalizedEmail = email.Trim().ToLower();

        return await _context.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Email.ToLower() == normalizedEmail);
    }

    public async Task<List<User>> GetUsersByRoleAsync(string roleName)
    {
        return await _context.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .Where(u => u.UserRoles.Any(ur => ur.Role!.RoleName == roleName))
            .ToListAsync();
    }

    public async Task<User?> GetByIdWithRolesAsync(Guid id)
    {
        return await _context.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Id == id);
    }

    public async Task<(List<User> Items, int TotalItems)> GetUsersPagedAsync(int page, int pageSize, string? search)
    {
        var query = _context.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .AsQueryable();
            
        if(!string.IsNullOrWhiteSpace(search))
        {
            var lowerSearch = search.ToLower();
            query = query.Where(u =>
                u.Phone.ToLower().Contains(lowerSearch) ||
                u.Email.ToLower().Contains(lowerSearch) ||
                u.FullName.ToLower().Contains(lowerSearch)
            );
        }

        var totalItems = await query.CountAsync();

        var items = await query
        .OrderByDescending(u => u.CreatedAt)
        .Skip((page - 1) * pageSize)
        .Take(pageSize)
        .ToListAsync();

        return (items, totalItems);
    }

    public async Task<List<Guid>> GetUserRegionIdsAsync(Guid userId)
    {
        return await _context.UserGeographicScopes
            .AsNoTracking()
            .Where(s => s.UserId == userId)
            .Select(s => s.RegionId)
            .Distinct()
            .ToListAsync();
    }

    public async Task<(List<User> Items, int TotalItems)> GetUsersInRegionsPagedAsync(
        int page,
        int pageSize,
        string? search,
        IReadOnlyCollection<Guid> regionIds,
        bool includeUnassignedOperationalUsers = false)
    {
        if (regionIds == null || regionIds.Count == 0)
        {
            return (new List<User>(), 0);
        }

        var operationalRoles = new[]
        {
            UavPms.Shared.Contracts.Constants.UserRoles.Inspector,
            UavPms.Shared.Contracts.Constants.UserRoles.Analyst,
            UavPms.Shared.Contracts.Constants.UserRoles.Technician
        };

        var query = _context.Users
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .Where(u => !u.UserRoles.Any(ur => ur.Role!.RoleName == UavPms.Shared.Contracts.Constants.UserRoles.SystemAdmin))
            .Where(u =>
                _context.UserGeographicScopes.Any(s => s.UserId == u.Id && regionIds.Contains(s.RegionId)) ||
                (includeUnassignedOperationalUsers &&
                 !_context.UserGeographicScopes.Any(s => s.UserId == u.Id) &&
                 u.UserRoles.Any(ur => operationalRoles.Contains(ur.Role!.RoleName))));

        if (!string.IsNullOrWhiteSpace(search))
        {
            var lowerSearch = search.ToLower();
            query = query.Where(u =>
                u.Phone.ToLower().Contains(lowerSearch) ||
                u.Email.ToLower().Contains(lowerSearch) ||
                u.FullName.ToLower().Contains(lowerSearch)
            );
        }

        var totalItems = await query.CountAsync();

        var items = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return (items, totalItems);
    }
}
