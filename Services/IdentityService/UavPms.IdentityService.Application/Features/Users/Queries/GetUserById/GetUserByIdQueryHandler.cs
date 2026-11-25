using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using UavPms.IdentityService.Application.Common.Exceptions;
using UavPms.IdentityService.Application.Features.Users.DTOs;
using UavPms.IdentityService.Domain.Interfaces.Repositories;
using UavPms.IdentityService.Domain.Interfaces.Services;
using UavPms.Shared.Contracts.Constants;

namespace UavPms.IdentityService.Application.Features.Users.Queries.GetUserById;

public class GetUserByIdQueryHandler : IRequestHandler<GetUserByIdQuery, UserDetailDto>
{
    private static readonly HashSet<string> OperationalRoles = new(StringComparer.OrdinalIgnoreCase)
    {
        UserRoles.Inspector,
        UserRoles.Analyst,
        UserRoles.Technician
    };

    private readonly IUserRepository _userRepository;
    private readonly ICurrentUserServices? _currentUserServices;

    public GetUserByIdQueryHandler(
        IUserRepository userRepository,
        ICurrentUserServices? currentUserServices = null)
    {
        _userRepository = userRepository;
        _currentUserServices = currentUserServices;
    }

    public async Task<UserDetailDto> Handle(GetUserByIdQuery request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByIdWithRolesAsync(request.Id);
        if (user == null)
        {
            throw new KeyNotFoundException("User not found");
        }

        var isSystemAdmin = _currentUserServices == null ||
            _currentUserServices.Roles.Any(r => string.Equals(r, UserRoles.SystemAdmin, StringComparison.OrdinalIgnoreCase));

        if (!isSystemAdmin && _currentUserServices != null && _currentUserServices.UserId != request.Id)
        {
            var targetRoles = user.UserRoles
                .Select(ur => ur.Role?.RoleName)
                .Where(r => !string.IsNullOrWhiteSpace(r))
                .ToList();

            if (targetRoles.Any(r => string.Equals(r, UserRoles.SystemAdmin, StringComparison.OrdinalIgnoreCase)))
            {
                throw new ForbiddenException("Managers cannot view SystemAdmin accounts.");
            }

            var managerRegions = await _userRepository.GetUserRegionIdsAsync(_currentUserServices.UserId);
            if (managerRegions.Count == 0)
            {
                throw new ForbiddenException("You have no assigned geographic regions.");
            }

            var targetRegions = await _userRepository.GetUserRegionIdsAsync(user.Id);
            var overlaps = targetRegions.Any(r => managerRegions.Contains(r));
            var isUnassignedOperational = targetRegions.Count == 0 && targetRoles.Any(r => OperationalRoles.Contains(r!));

            if (!overlaps && !isUnassignedOperational)
            {
                throw new ForbiddenException("You can only view users within your managed regions.");
            }
        }

        return new UserDetailDto
        {
            Id = user.Id,
            Email = user.Email,
            FullName = user.FullName,
            Phone = user.Phone,
            Status = user.Status.ToString(),
            CreatedAt = user.CreatedAt,
            Roles = user.UserRoles.Select(ur => ur.Role!.RoleName).ToList()
        };
    }
}