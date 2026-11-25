using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using UavPms.IdentityService.Application.Common.Exceptions;
using UavPms.IdentityService.Application.Features.Users.Queries.GetUserById;
using UavPms.IdentityService.Application.Features.Users.Queries.GetUsers;
using UavPms.IdentityService.Domain.Entities;
using UavPms.IdentityService.Domain.Enums;
using UavPms.IdentityService.Domain.Interfaces.Services;
using UavPms.IdentityService.Infrastructure.Persistence;
using UavPms.IdentityService.Infrastructure.Repositories;
using UavPms.Shared.Contracts.Constants;
using Xunit;

namespace UavPms.IdentityService.Tests.Features.Users.Queries;

public class ManagerRegionUserVisibilityTests
{
    private static ApplicationDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase($"identity-region-vis-{Guid.NewGuid():N}")
            .Options;
        return new ApplicationDbContext(options, Mock.Of<ICurrentUserServices>());
    }

    private static ICurrentUserServices UserContext(Guid userId, string role)
    {
        var mock = new Mock<ICurrentUserServices>();
        mock.SetupGet(u => u.UserId).Returns(userId);
        mock.SetupGet(u => u.IsAuthenticated).Returns(true);
        mock.SetupGet(u => u.Roles).Returns(new[] { role });
        return mock.Object;
    }

    [Fact]
    public async Task GetUsersAndGetUserById_ShouldEnforceAdminGlobalAccess_ManagerRegionScope_IncludeUnassigned_AndFailClosed()
    {
        await using var db = CreateContext();

        var rAdmin = new Role { Id = 1, RoleName = UserRoles.SystemAdmin, Description = "Admin" };
        var rManager = new Role { Id = 2, RoleName = UserRoles.Manager, Description = "Manager" };
        var rInspector = new Role { Id = 3, RoleName = UserRoles.Inspector, Description = "Inspector" };
        var rAnalyst = new Role { Id = 4, RoleName = UserRoles.Analyst, Description = "Analyst" };
        var rTech = new Role { Id = 5, RoleName = UserRoles.Technician, Description = "Tech" };
        db.Roles.AddRange(rAdmin, rManager, rInspector, rAnalyst, rTech);

        User NewUser(string email, Role role)
        {
            var u = new User
            {
                Id = Guid.NewGuid(),
                Email = email,
                FullName = email,
                Phone = "0900000000",
                Status = UserStatus.Active,
                IsEmailVerified = true
            };
            u.UserRoles.Add(new UserRole { UserId = u.Id, RoleId = role.Id, Role = role, User = u });
            return u;
        }

        var admin = NewUser("admin@uav.vn", rAdmin);
        var manager1 = NewUser("m1@uav.vn", rManager);
        var coManager2 = NewUser("m2@uav.vn", rManager);
        var outsideManager3 = NewUser("m3@uav.vn", rManager);
        var zeroScopeManager = NewUser("m0@uav.vn", rManager);
        var inspectorInA = NewUser("insp-a@uav.vn", rInspector);
        var analystOutsideC = NewUser("analyst-c@uav.vn", rAnalyst);
        var unassignedTech = NewUser("tech-new@uav.vn", rTech);

        db.Users.AddRange(admin, manager1, coManager2, outsideManager3, zeroScopeManager, inspectorInA, analystOutsideC, unassignedTech);

        var (regA, regB, regC) = (Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        // Manager 1 -> [A, B]; Co-Manager 2 -> [B, C]; Outside Manager 3 -> [C]
        // Inspector -> [A]; Analyst -> [C]; Tech -> [] (unassigned)
        db.UserGeographicScopes.AddRange(
            new UserGeographicScope { Id = Guid.NewGuid(), UserId = manager1.Id, RegionId = regA },
            new UserGeographicScope { Id = Guid.NewGuid(), UserId = manager1.Id, RegionId = regB },
            new UserGeographicScope { Id = Guid.NewGuid(), UserId = coManager2.Id, RegionId = regB },
            new UserGeographicScope { Id = Guid.NewGuid(), UserId = coManager2.Id, RegionId = regC },
            new UserGeographicScope { Id = Guid.NewGuid(), UserId = outsideManager3.Id, RegionId = regC },
            new UserGeographicScope { Id = Guid.NewGuid(), UserId = inspectorInA.Id, RegionId = regA },
            new UserGeographicScope { Id = Guid.NewGuid(), UserId = analystOutsideC.Id, RegionId = regC });
        await db.SaveChangesAsync();

        var repo = new UserRepository(db);

        // 1. SystemAdmin sees ALL 8 users
        var adminList = await new GetUsersQueryHandler(repo, UserContext(admin.Id, UserRoles.SystemAdmin))
            .Handle(new GetUsersQuery(1, 50, null), CancellationToken.None);
        adminList.Items.Should().HaveCount(8);

        // 2. Manager 1 (IncludeUnassigned = false) sees only users in [A, B] (excluding SystemAdmin): manager1, coManager2, inspectorInA
        var m1List = await new GetUsersQueryHandler(repo, UserContext(manager1.Id, UserRoles.Manager))
            .Handle(new GetUsersQuery(1, 50, null, IncludeUnassigned: false), CancellationToken.None);
        m1List.Items.Select(u => u.Email).Should().BeEquivalentTo(new[] { "m1@uav.vn", "m2@uav.vn", "insp-a@uav.vn" });

        // 3. Manager 1 (IncludeUnassigned = true) ALSO sees unassigned operational user (unassignedTech), still NOT admin or outside users or zeroScopeManager
        var m1ListWithUnassigned = await new GetUsersQueryHandler(repo, UserContext(manager1.Id, UserRoles.Manager))
            .Handle(new GetUsersQuery(1, 50, null, IncludeUnassigned: true), CancellationToken.None);
        m1ListWithUnassigned.Items.Select(u => u.Email).Should().BeEquivalentTo(new[] { "m1@uav.vn", "m2@uav.vn", "insp-a@uav.vn", "tech-new@uav.vn" });

        // 4. Fail-Closed: Manager with 0 regions sees empty list
        var m0List = await new GetUsersQueryHandler(repo, UserContext(zeroScopeManager.Id, UserRoles.Manager))
            .Handle(new GetUsersQuery(1, 50, null, IncludeUnassigned: true), CancellationToken.None);
        m0List.Items.Should().BeEmpty();

        // 5. GetUserById for Manager 1: allowed for inspectorInA, coManager2, unassignedTech; Forbidden for admin & analystOutsideC
        var m1GetById = new GetUserByIdQueryHandler(repo, UserContext(manager1.Id, UserRoles.Manager));
        (await m1GetById.Handle(new GetUserByIdQuery(inspectorInA.Id), CancellationToken.None)).Email.Should().Be("insp-a@uav.vn");
        (await m1GetById.Handle(new GetUserByIdQuery(coManager2.Id), CancellationToken.None)).Email.Should().Be("m2@uav.vn");
        (await m1GetById.Handle(new GetUserByIdQuery(unassignedTech.Id), CancellationToken.None)).Email.Should().Be("tech-new@uav.vn");

        await FluentActions.Awaiting(() => m1GetById.Handle(new GetUserByIdQuery(admin.Id), CancellationToken.None))
            .Should().ThrowAsync<ForbiddenException>();
        await FluentActions.Awaiting(() => m1GetById.Handle(new GetUserByIdQuery(analystOutsideC.Id), CancellationToken.None))
            .Should().ThrowAsync<ForbiddenException>();

        // Fail-Closed GetUserById for zeroScopeManager
        var m0GetById = new GetUserByIdQueryHandler(repo, UserContext(zeroScopeManager.Id, UserRoles.Manager));
        await FluentActions.Awaiting(() => m0GetById.Handle(new GetUserByIdQuery(inspectorInA.Id), CancellationToken.None))
            .Should().ThrowAsync<ForbiddenException>();
    }
}
