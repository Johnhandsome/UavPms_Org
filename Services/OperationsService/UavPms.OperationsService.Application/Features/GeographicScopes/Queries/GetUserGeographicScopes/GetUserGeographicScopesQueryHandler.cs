using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using UavPms.OperationsService.Application.Common.Exceptions;
using UavPms.OperationsService.Application.Features.GeographicScopes.DTOs;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Interfaces.Repositories;
using UavPms.OperationsService.Domain.Interfaces.Services;
using UavPms.Shared.Contracts.Constants;

namespace UavPms.OperationsService.Application.Features.GeographicScopes.Queries.GetUserGeographicScopes;

public class GetUserGeographicScopesQueryHandler : IRequestHandler<GetUserGeographicScopesQuery, UserGeographicScopesDto>
{
    private static readonly HashSet<string> OperationalRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        UserRoles.Inspector,
        UserRoles.Analyst,
        UserRoles.Technician
    };

    private readonly IUserRepository _userRepository;
    private readonly IGenericRepository<UserGeographicScope> _scopeRepository;
    private readonly IGenericRepository<Region> _regionRepository;
    private readonly ICurrentUserServices _currentUser;

    public GetUserGeographicScopesQueryHandler(
        IUserRepository userRepository,
        IGenericRepository<UserGeographicScope> scopeRepository,
        IGenericRepository<Region> regionRepository,
        ICurrentUserServices currentUser)
    {
        _userRepository = userRepository;
        _scopeRepository = scopeRepository;
        _regionRepository = regionRepository;
        _currentUser = currentUser;
    }

    public async Task<UserGeographicScopesDto> Handle(GetUserGeographicScopesQuery request, CancellationToken cancellationToken)
    {
        var isSystemAdmin = _currentUser.Roles.Any(r => string.Equals(r, UserRoles.SystemAdmin, StringComparison.OrdinalIgnoreCase));
        var isManager = _currentUser.Roles.Any(r => string.Equals(r, UserRoles.Manager, StringComparison.OrdinalIgnoreCase));

        if (!isSystemAdmin && !isManager && request.UserId != _currentUser.UserId)
        {
            throw new ForbiddenException("You can only view your own geographic scope.");
        }

        var user = await _userRepository.GetByIdWithRolesAsync(request.UserId);
        if (user == null || user.IsDeleted)
        {
            throw new NotFoundException("User", request.UserId);
        }

        var scopes = await _scopeRepository.FindAsync(s => s.UserId == request.UserId);
        var regionIds = scopes.Select(s => s.RegionId).Distinct().ToList();

        if (!isSystemAdmin && isManager && request.UserId != _currentUser.UserId)
        {
            var targetRoles = user.UserRoles
                .Select(ur => ur.Role?.RoleName)
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .ToList();
            if (targetRoles.Any(r => string.Equals(r, UserRoles.SystemAdmin, StringComparison.OrdinalIgnoreCase)))
            {
                throw new ForbiddenException("Managers cannot view geographic scopes of SystemAdmin accounts.");
            }

            var managerDirectRegionIds = (await _scopeRepository.FindAsync(s => s.UserId == _currentUser.UserId))
                .Select(s => s.RegionId)
                .Distinct()
                .ToList();
            if (managerDirectRegionIds.Count == 0)
            {
                throw new ForbiddenException("You have no assigned geographic regions.");
            }

            var managerAccessibleRegions = await _regionRepository.FindAsync(r =>
                managerDirectRegionIds.Contains(r.Id) || (r.ParentId != null && managerDirectRegionIds.Contains(r.ParentId.Value)));
            var managerAllowedRegionIds = managerAccessibleRegions.Select(r => r.Id).ToHashSet();

            var overlapsManagerRegions = regionIds.Any(id => managerAllowedRegionIds.Contains(id));
            var isUnassignedOperationalUser = regionIds.Count == 0 && targetRoles.Any(r => OperationalRoles.Contains(r!));

            if (!overlapsManagerRegions && !isUnassignedOperationalUser)
            {
                throw new ForbiddenException("You can only view geographic scopes of users within your managed regions.");
            }
        }

        var regions = await _regionRepository.FindAsync(r => regionIds.Contains(r.Id));

        return new UserGeographicScopesDto(
            request.UserId,
            regions.OrderBy(r => r.Code)
                .Select(r => new UserGeographicScopeRegionDto(r.Id, r.Code, r.Name, r.Type))
                .ToList());
    }
}
