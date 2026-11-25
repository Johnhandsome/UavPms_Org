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

namespace UavPms.OperationsService.Application.Features.GeographicScopes.Commands.SetUserGeographicScopes;

public class SetUserGeographicScopesCommandHandler : IRequestHandler<SetUserGeographicScopesCommand, UserGeographicScopesDto>
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
    private readonly IUnitOfWork _unitOfWork;

    public SetUserGeographicScopesCommandHandler(
        IUserRepository userRepository,
        IGenericRepository<UserGeographicScope> scopeRepository,
        IGenericRepository<Region> regionRepository,
        ICurrentUserServices currentUser,
        IUnitOfWork unitOfWork)
    {
        _userRepository = userRepository;
        _scopeRepository = scopeRepository;
        _regionRepository = regionRepository;
        _currentUser = currentUser;
        _unitOfWork = unitOfWork;
    }

    public async Task<UserGeographicScopesDto> Handle(SetUserGeographicScopesCommand request, CancellationToken cancellationToken)
    {
        var isSystemAdmin = _currentUser.Roles.Any(r => string.Equals(r, UserRoles.SystemAdmin, StringComparison.OrdinalIgnoreCase));
        if (!isSystemAdmin && request.UserId == _currentUser.UserId)
        {
            throw new ForbiddenException("Only a SystemAdmin can change their own geographic scope.");
        }

        var user = await _userRepository.GetByIdWithRolesAsync(request.UserId);
        if (user == null || user.IsDeleted)
        {
            throw new NotFoundException("User", request.UserId);
        }

        var requested = (request.RegionIds ?? Array.Empty<Guid>()).Distinct().ToList();
        if (requested.Contains(Guid.Empty))
        {
            throw new BusinessRuleException("INVALID_REGION", "Region ID is invalid.");
        }

        var current = await _scopeRepository.FindAsync(s => s.UserId == request.UserId, track: true);
        var existingRegionIds = current.Select(s => s.RegionId).ToHashSet();

        if (!isSystemAdmin)
        {
            // Managers may only assign scopes for the 3 operational roles (Inspector, Analyst, Technician).
            var targetRoles = user.UserRoles
                .Select(ur => ur.Role?.RoleName)
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .ToList();

            var isHigherOrPeerRole = targetRoles.Any(r =>
                string.Equals(r, UserRoles.SystemAdmin, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(r, UserRoles.Manager, StringComparison.OrdinalIgnoreCase));
            var hasOperationalRole = targetRoles.Any(r => OperationalRoles.Contains(r!));

            if (isHigherOrPeerRole || !hasOperationalRole)
            {
                throw new ForbiddenException("Managers can only assign geographic scopes for Inspector, Analyst, or Technician accounts.");
            }

            // Fail-closed: a Manager with 0 assigned regions cannot delegate any region.
            var managerDirectRegionIds = (await _scopeRepository.FindAsync(s => s.UserId == _currentUser.UserId))
                .Select(s => s.RegionId)
                .Distinct()
                .ToList();
            if (managerDirectRegionIds.Count == 0)
            {
                throw new ForbiddenException("You have no assigned geographic regions to delegate.");
            }

            var managerAccessibleRegions = await _regionRepository.FindAsync(r =>
                managerDirectRegionIds.Contains(r.Id) || (r.ParentId != null && managerDirectRegionIds.Contains(r.ParentId.Value)));
            var managerAllowedRegionIds = managerAccessibleRegions.Select(r => r.Id).ToHashSet();

            // Manager cannot add any region outside their own managed regions.
            var unauthorizedAdditions = requested
                .Where(id => !managerAllowedRegionIds.Contains(id) && !existingRegionIds.Contains(id))
                .ToList();
            if (unauthorizedAdditions.Count > 0)
            {
                throw new ForbiddenException("Managers can only assign regions within their own geographic scope.");
            }

            // Overlap-safe: Manager only removes scopes within their own managed regions; scopes owned by other Managers are preserved.
            foreach (var scope in current.Where(s => managerAllowedRegionIds.Contains(s.RegionId) && !requested.Contains(s.RegionId)))
            {
                await _scopeRepository.DeleteAsync(scope);
            }

            foreach (var regionId in requested.Where(id => managerAllowedRegionIds.Contains(id) && !existingRegionIds.Contains(id)))
            {
                await _scopeRepository.AddAsync(new UserGeographicScope
                {
                    Id = Guid.NewGuid(),
                    UserId = request.UserId,
                    RegionId = regionId,
                    CreatedBy = _currentUser.UserId
                });
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            var finalRegionIds = existingRegionIds
                .Where(id => !managerAllowedRegionIds.Contains(id))
                .Concat(requested.Where(id => managerAllowedRegionIds.Contains(id)))
                .Distinct()
                .ToList();
            var finalRegions = await _regionRepository.FindAsync(r => finalRegionIds.Contains(r.Id));

            return new UserGeographicScopesDto(
                request.UserId,
                finalRegions.OrderBy(r => r.Code)
                    .Select(r => new UserGeographicScopeRegionDto(r.Id, r.Code, r.Name, r.Type))
                    .ToList());
        }

        var regions = await _regionRepository.FindAsync(r => requested.Contains(r.Id));
        var missing = requested.Except(regions.Select(r => r.Id)).ToList();
        if (missing.Count > 0)
        {
            throw new NotFoundException($"Region(s) not found: {string.Join(", ", missing)}");
        }

        // SystemAdmin: full replacement of target user's scopes.
        foreach (var scope in current.Where(s => !requested.Contains(s.RegionId)))
        {
            await _scopeRepository.DeleteAsync(scope);
        }

        foreach (var regionId in requested.Where(id => !existingRegionIds.Contains(id)))
        {
            await _scopeRepository.AddAsync(new UserGeographicScope
            {
                Id = Guid.NewGuid(),
                UserId = request.UserId,
                RegionId = regionId,
                CreatedBy = _currentUser.UserId
            });
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return new UserGeographicScopesDto(
            request.UserId,
            regions.OrderBy(r => r.Code)
                .Select(r => new UserGeographicScopeRegionDto(r.Id, r.Code, r.Name, r.Type))
                .ToList());
    }
}
