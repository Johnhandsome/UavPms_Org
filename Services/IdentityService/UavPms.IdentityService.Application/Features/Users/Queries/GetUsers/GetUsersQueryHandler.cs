using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MediatR;
using UavPms.IdentityService.Application.Common.DTOs;
using UavPms.IdentityService.Application.Features.Users.DTOs;
using UavPms.IdentityService.Domain.Entities;
using UavPms.IdentityService.Domain.Interfaces.Repositories;
using UavPms.IdentityService.Domain.Interfaces.Services;
using UavPms.Shared.Contracts.Constants;

namespace UavPms.IdentityService.Application.Features.Users.Queries.GetUsers;

public class GetUsersQueryHandler : IRequestHandler<GetUsersQuery, PaginatedUsersResponse>
{
    private readonly IUserRepository _userRepository;
    private readonly ICurrentUserServices? _currentUserServices;

    public GetUsersQueryHandler(
        IUserRepository userRepository,
        ICurrentUserServices? currentUserServices = null)
    {
        _userRepository = userRepository;
        _currentUserServices = currentUserServices;
    }

    public async Task<PaginatedUsersResponse> Handle(GetUsersQuery request, CancellationToken cancellationToken)
    {
        List<User> users;
        int totalItems;

        var isSystemAdmin = _currentUserServices == null ||
            _currentUserServices.Roles.Any(r => string.Equals(r, UserRoles.SystemAdmin, StringComparison.OrdinalIgnoreCase));

        if (!isSystemAdmin && _currentUserServices != null && _currentUserServices.UserId != Guid.Empty)
        {
            var managerRegions = await _userRepository.GetUserRegionIdsAsync(_currentUserServices.UserId);
            if (managerRegions.Count == 0)
            {
                return new PaginatedUsersResponse(
                    new List<UserDetailDto>(),
                    new PaginationMetaData(request.Page, request.PageSize, 0, 0));
            }

            (users, totalItems) = await _userRepository.GetUsersInRegionsPagedAsync(
                request.Page,
                request.PageSize,
                request.Search,
                managerRegions,
                request.IncludeUnassigned);
        }
        else
        {
            (users, totalItems) =
                await _userRepository.GetUsersPagedAsync(request.Page, request.PageSize, request.Search);
        }

        var userDtos = users.Select(u => new UserDetailDto
        {
            Id = u.Id,
            CreatedAt = u.CreatedAt,
            Email = u.Email,
            FullName = u.FullName,
            Phone = u.Phone,
            Roles = u.UserRoles.Select(ur => ur.Role!.RoleName).ToList(),
            Status = u.Status.ToString(),
        });

        var totalPages = (int)Math.Ceiling((double)totalItems / request.PageSize);
        var metaData = new PaginationMetaData(request.Page, request.PageSize, totalItems, totalPages);

        return new PaginatedUsersResponse(userDtos.ToList(), metaData);
    }
}