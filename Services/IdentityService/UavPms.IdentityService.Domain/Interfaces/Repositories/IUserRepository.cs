using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using UavPms.IdentityService.Domain.Entities;

namespace UavPms.IdentityService.Domain.Interfaces.Repositories;

public interface IUserRepository : IGenericRepository<User>
{
    Task<User?> GetByEmailWithRolesAsync(string email);
    Task<List<User>> GetUsersByRoleAsync(string roleName);
    Task<User?> GetByIdWithRolesAsync(Guid id);
    Task<(List<User> Items, int TotalItems)> GetUsersPagedAsync(int page, int pageSize, string? search);
    Task<List<Guid>> GetUserRegionIdsAsync(Guid userId);
    Task<(List<User> Items, int TotalItems)> GetUsersInRegionsPagedAsync(
        int page,
        int pageSize,
        string? search,
        IReadOnlyCollection<Guid> regionIds,
        bool includeUnassignedOperationalUsers = false);
}