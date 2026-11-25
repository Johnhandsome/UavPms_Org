using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using UavPms.OperationsService.Application.Common.Exceptions;
using UavPms.OperationsService.Application.Features.GeographicScopes.Commands.SetUserGeographicScopes;
using UavPms.OperationsService.Application.Features.GeographicScopes.Queries.GetUserGeographicScopes;
using UavPms.OperationsService.Domain.Entities;
using UavPms.OperationsService.Domain.Interfaces.Services;
using UavPms.OperationsService.Infrastructure.Persistence;
using UavPms.OperationsService.Infrastructure.Repositories;
using UavPms.Shared.Contracts.Constants;
using Xunit;

namespace UavPms.OperationsService.Tests.Features.Gis;

public class UserGeographicScopeTests
{
    private static Mock<ICurrentUserServices> CreateUserMock(Guid userId, params string[] roles)
    {
        var mock = new Mock<ICurrentUserServices>();
        mock.SetupGet(u => u.UserId).Returns(userId);
        mock.SetupGet(u => u.IsAuthenticated).Returns(true);
        mock.SetupGet(u => u.Roles).Returns(roles);
        return mock;
    }

    private static ApplicationDbContext CreateContext(ICurrentUserServices user)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(Microsoft.EntityFrameworkCore.Diagnostics.InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        return new ApplicationDbContext(options, user);
    }

    private static SetUserGeographicScopesCommandHandler CreateSetHandler(ApplicationDbContext db, ICurrentUserServices user)
        => new(new UserRepository(db), new GenericRepository<UserGeographicScope>(db), new GenericRepository<Region>(db), user, new UnitOfWork(db));

    private static GetUserGeographicScopesQueryHandler CreateGetHandler(ApplicationDbContext db, ICurrentUserServices user)
        => new(new UserRepository(db), new GenericRepository<UserGeographicScope>(db), new GenericRepository<Region>(db), user);

    private static Region NewRegion(string code) => new() { Id = Guid.NewGuid(), Code = code, Name = code, Type = "Province" };

    [Fact]
    public async Task SetScopes_ReplacesSet_SoftDeletesRemovedAndAddsNew()
    {
        var admin = CreateUserMock(Guid.NewGuid(), UserRoles.SystemAdmin);
        await using var db = CreateContext(admin.Object);
        var user = new User { Id = Guid.NewGuid(), Email = "inspector@x.vn", FullName = "Inspector" };
        var (a, b, c) = (NewRegion("A"), NewRegion("B"), NewRegion("C"));
        db.Users.Add(user);
        db.Regions.AddRange(a, b, c);
        db.UserGeographicScopes.AddRange(
            new UserGeographicScope { Id = Guid.NewGuid(), UserId = user.Id, RegionId = a.Id },
            new UserGeographicScope { Id = Guid.NewGuid(), UserId = user.Id, RegionId = b.Id });
        await db.SaveChangesAsync();

        var result = await CreateSetHandler(db, admin.Object)
            .Handle(new SetUserGeographicScopesCommand(user.Id, new[] { b.Id, c.Id, c.Id }), CancellationToken.None);

        result.Regions.Select(r => r.Code).Should().Equal("B", "C");
        var rows = await db.UserGeographicScopes.IgnoreQueryFilters().Where(s => s.UserId == user.Id).ToListAsync();
        rows.Should().HaveCount(3);
        rows.Single(s => s.RegionId == a.Id).IsDeleted.Should().BeTrue();
        rows.Where(s => !s.IsDeleted).Select(s => s.RegionId).Should().BeEquivalentTo(new[] { b.Id, c.Id });
    }

    [Fact]
    public async Task SetScopes_EmptyList_RemovesAll()
    {
        var admin = CreateUserMock(Guid.NewGuid(), UserRoles.SystemAdmin);
        await using var db = CreateContext(admin.Object);
        var user = new User { Id = Guid.NewGuid(), Email = "u@x.vn" };
        var a = NewRegion("A");
        db.Users.Add(user);
        db.Regions.Add(a);
        db.UserGeographicScopes.Add(new UserGeographicScope { Id = Guid.NewGuid(), UserId = user.Id, RegionId = a.Id });
        await db.SaveChangesAsync();

        var result = await CreateSetHandler(db, admin.Object)
            .Handle(new SetUserGeographicScopesCommand(user.Id, Array.Empty<Guid>()), CancellationToken.None);

        result.Regions.Should().BeEmpty();
        (await db.UserGeographicScopes.CountAsync(s => s.UserId == user.Id)).Should().Be(0);
    }

    [Fact]
    public async Task SetScopes_UnknownRegion_ThrowsAndChangesNothing()
    {
        var admin = CreateUserMock(Guid.NewGuid(), UserRoles.SystemAdmin);
        await using var db = CreateContext(admin.Object);
        var user = new User { Id = Guid.NewGuid(), Email = "u@x.vn" };
        var a = NewRegion("A");
        db.Users.Add(user);
        db.Regions.Add(a);
        db.UserGeographicScopes.Add(new UserGeographicScope { Id = Guid.NewGuid(), UserId = user.Id, RegionId = a.Id });
        await db.SaveChangesAsync();

        var act = () => CreateSetHandler(db, admin.Object)
            .Handle(new SetUserGeographicScopesCommand(user.Id, new[] { Guid.NewGuid() }), CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
        (await db.UserGeographicScopes.CountAsync(s => s.UserId == user.Id)).Should().Be(1);
    }

    [Fact]
    public async Task SetScopes_ManagerOnOwnScope_IsForbidden()
    {
        var managerId = Guid.NewGuid();
        var manager = CreateUserMock(managerId, UserRoles.Manager);
        await using var db = CreateContext(manager.Object);
        var a = NewRegion("A");
        db.Users.Add(new User { Id = managerId, Email = "m@x.vn" });
        db.Regions.Add(a);
        await db.SaveChangesAsync();

        var act = () => CreateSetHandler(db, manager.Object)
            .Handle(new SetUserGeographicScopesCommand(managerId, new[] { a.Id }), CancellationToken.None);

        await act.Should().ThrowAsync<ForbiddenException>();
    }

    [Fact]
    public async Task GetScopes_NonManagerReadingAnotherUser_IsForbidden_ButOwnScopeIsAllowed()
    {
        var inspectorId = Guid.NewGuid();
        var inspector = CreateUserMock(inspectorId, UserRoles.Inspector);
        await using var db = CreateContext(inspector.Object);
        var other = new User { Id = Guid.NewGuid(), Email = "o@x.vn" };
        var a = NewRegion("A");
        db.Users.AddRange(new User { Id = inspectorId, Email = "i@x.vn" }, other);
        db.Regions.Add(a);
        db.UserGeographicScopes.Add(new UserGeographicScope { Id = Guid.NewGuid(), UserId = inspectorId, RegionId = a.Id });
        await db.SaveChangesAsync();
        var handler = CreateGetHandler(db, inspector.Object);

        var readOther = () => handler.Handle(new GetUserGeographicScopesQuery(other.Id), CancellationToken.None);
        await readOther.Should().ThrowAsync<ForbiddenException>();

        var own = await handler.Handle(new GetUserGeographicScopesQuery(inspectorId), CancellationToken.None);
        own.Regions.Should().ContainSingle(r => r.RegionId == a.Id && r.Code == "A");
    }

    [Fact]
    public async Task SetScopes_ManagerManyToMany_OverlapSafe_RoleGuard_AndFailClosed_ShouldEnforceAllRules()
    {
        var manager1Id = Guid.NewGuid();
        var manager2Id = Guid.NewGuid();
        var zeroScopeManagerId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var inspectorId = Guid.NewGuid();
        var unassignedTechId = Guid.NewGuid();

        var manager1Ctx = CreateUserMock(manager1Id, UserRoles.Manager);
        var zeroManagerCtx = CreateUserMock(zeroScopeManagerId, UserRoles.Manager);
        await using var db = CreateContext(manager1Ctx.Object);

        var rAdmin = new Role { Id = 1, RoleName = UserRoles.SystemAdmin, Description = "Admin" };
        var rManager = new Role { Id = 2, RoleName = UserRoles.Manager, Description = "Manager" };
        var rInspector = new Role { Id = 3, RoleName = UserRoles.Inspector, Description = "Inspector" };
        var rTech = new Role { Id = 5, RoleName = UserRoles.Technician, Description = "Technician" };
        db.Roles.AddRange(rAdmin, rManager, rInspector, rTech);

        var (regA, regB, regC) = (NewRegion("A"), NewRegion("B"), NewRegion("C"));
        db.Regions.AddRange(regA, regB, regC);

        User SeedUser(Guid id, string email, Role role)
        {
            var u = new User { Id = id, Email = email, FullName = email, Status = "Active" };
            u.UserRoles.Add(new UserRole { UserId = id, RoleId = role.Id, Role = role, User = u });
            return u;
        }

        db.Users.AddRange(
            SeedUser(adminId, "admin@x.vn", rAdmin),
            SeedUser(manager1Id, "m1@x.vn", rManager),
            SeedUser(manager2Id, "m2@x.vn", rManager),
            SeedUser(zeroScopeManagerId, "m0@x.vn", rManager),
            SeedUser(inspectorId, "insp@x.vn", rInspector),
            SeedUser(unassignedTechId, "tech@x.vn", rTech));

        // Manager 1 owns [A, B]; Manager 2 owns [B, C] (Region B is shared by both Managers);
        // Inspector initially has [A, C] (shared between Manager 1 and Manager 2).
        db.UserGeographicScopes.AddRange(
            new UserGeographicScope { Id = Guid.NewGuid(), UserId = manager1Id, RegionId = regA.Id },
            new UserGeographicScope { Id = Guid.NewGuid(), UserId = manager1Id, RegionId = regB.Id },
            new UserGeographicScope { Id = Guid.NewGuid(), UserId = manager2Id, RegionId = regB.Id },
            new UserGeographicScope { Id = Guid.NewGuid(), UserId = manager2Id, RegionId = regC.Id },
            new UserGeographicScope { Id = Guid.NewGuid(), UserId = inspectorId, RegionId = regA.Id },
            new UserGeographicScope { Id = Guid.NewGuid(), UserId = inspectorId, RegionId = regC.Id });
        await db.SaveChangesAsync();

        var m1Set = CreateSetHandler(db, manager1Ctx.Object);
        var m1Get = CreateGetHandler(db, manager1Ctx.Object);

        // 1. Manager 1 CANNOT change scopes of SystemAdmin or co-Manager 2
        await FluentActions.Awaiting(() => m1Set.Handle(new SetUserGeographicScopesCommand(adminId, new[] { regA.Id }), CancellationToken.None))
            .Should().ThrowAsync<ForbiddenException>();
        await FluentActions.Awaiting(() => m1Set.Handle(new SetUserGeographicScopesCommand(manager2Id, new[] { regB.Id }), CancellationToken.None))
            .Should().ThrowAsync<ForbiddenException>();

        // 2. Fail-Closed: Manager with 0 regions CANNOT assign or read scopes of others
        var m0Set = CreateSetHandler(db, zeroManagerCtx.Object);
        var m0Get = CreateGetHandler(db, zeroManagerCtx.Object);
        await FluentActions.Awaiting(() => m0Set.Handle(new SetUserGeographicScopesCommand(unassignedTechId, new[] { regA.Id }), CancellationToken.None))
            .Should().ThrowAsync<ForbiddenException>();
        await FluentActions.Awaiting(() => m0Get.Handle(new GetUserGeographicScopesQuery(inspectorId), CancellationToken.None))
            .Should().ThrowAsync<ForbiddenException>();

        // 3. Manager 1 CANNOT assign Region C (outside Manager 1's scope) to unassigned Technician
        await FluentActions.Awaiting(() => m1Set.Handle(new SetUserGeographicScopesCommand(unassignedTechId, new[] { regC.Id }), CancellationToken.None))
            .Should().ThrowAsync<ForbiddenException>();

        // 4. Manager 1 CAN assign Region A to unassigned Technician
        var techAssigned = await m1Set.Handle(new SetUserGeographicScopesCommand(unassignedTechId, new[] { regA.Id }), CancellationToken.None);
        techAssigned.Regions.Select(r => r.Code).Should().Equal("A");

        // 5. Overlap-Safe: Manager 1 updates Inspector from [A, C] to [B] (within M1's scope: removes A, adds B) -> Region C (owned by M2) MUST be preserved in DB!
        var updatedInspector = await m1Set.Handle(new SetUserGeographicScopesCommand(inspectorId, new[] { regB.Id }), CancellationToken.None);
        updatedInspector.Regions.Select(r => r.Code).Should().Equal("B"); // Manager 1 sees their own accessible region B
        var persistedInspectorRegions = await db.UserGeographicScopes
            .Where(s => s.UserId == inspectorId && !s.IsDeleted)
            .Select(s => s.RegionId)
            .ToListAsync();
        persistedInspectorRegions.Should().BeEquivalentTo(new[] { regB.Id, regC.Id });

        // 6. GetUserGeographicScopes: Manager 1 can view co-Manager 2 (shares Region B), but NOT SystemAdmin
        var m2Scopes = await m1Get.Handle(new GetUserGeographicScopesQuery(manager2Id), CancellationToken.None);
        m2Scopes.Regions.Select(r => r.Code).Should().Equal("B"); // Manager 1 sees the shared region B

        await FluentActions.Awaiting(() => m1Get.Handle(new GetUserGeographicScopesQuery(adminId), CancellationToken.None))
            .Should().ThrowAsync<ForbiddenException>();
    }
}
