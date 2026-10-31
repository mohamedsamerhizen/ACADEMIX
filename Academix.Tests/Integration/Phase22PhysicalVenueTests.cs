using System.Security.Claims;
using Academix.Api.Data;
using Academix.Api.Domain.Academic;
using Academix.Api.Domain.AcademicOperations;
using Academix.Api.Domain.Identity;
using Academix.Api.DTOs.AcademicOperations;
using Academix.Api.Middleware;
using Academix.Api.Services.AcademicOperations;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Academix.Tests.Integration;

/// <summary>Real PostgreSQL fixture scenarios. Not run during the container-disabled feature phase.</summary>
public sealed class Phase22PhysicalVenueTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private static ClaimsPrincipal Principal(Guid id, Guid institutionId)
        => new(new ClaimsIdentity([new(ClaimTypes.NameIdentifier, id.ToString()),
            new("institution_id", institutionId.ToString())], "test"));

    private async Task<(Guid Institution, Guid Room, ClaimsPrincipal Admin)> Setup()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AcademixDbContext>();
        var institution = new Institution { Code = Guid.NewGuid().ToString("N")[..12], Name = "Venue test institution" };
        db.Institutions.Add(institution);
        await db.SaveChangesAsync();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var mail = Guid.NewGuid().ToString("N") + "@example.test";
        var admin = new ApplicationUser { Id = Guid.NewGuid(), InstitutionId = institution.Id,
            UserName = mail, Email = mail };
        Assert.True((await users.CreateAsync(admin, "Safe!VenueTests2026")).Succeeded);
        Assert.True((await users.AddToRoleAsync(admin, AcademixRoles.InstitutionAdministrator)).Succeeded);
        var building = new CampusBuilding { InstitutionId = institution.Id, Code = "B", Name = "Main" };
        var room = new CampusRoom { InstitutionId = institution.Id, BuildingId = building.Id,
            Code = "A1", Name = "Exam Hall", Kind = "LectureHall", Capacity = 80 };
        db.CampusBuildings.Add(building);
        db.CampusRooms.Add(room);
        await db.SaveChangesAsync();
        return (institution.Id, room.Id, Principal(admin.Id, institution.Id));
    }

    [Fact]
    public async Task MaintenanceIsIdempotentBlocksOverlapsAndCanBeCancelled()
    {
        var (institution, room, admin) = await Setup();
        await using var scope = fixture.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<CampusReservationService>();
        var start = DateTime.UtcNow.AddDays(2);
        var create = new CampusReservationCreate(Guid.NewGuid(), room, "Maintenance",
            start, start.AddHours(2), "Scheduled electrical repair");
        var first = await service.Create(admin, institution, create, default);
        Assert.Equal(first.Id, (await service.Create(admin, institution, create, default)).Id);
        await Assert.ThrowsAsync<ApiFault>(() => service.Create(admin, institution,
            create with { ClientRequestId = Guid.NewGuid() }, default));
        var cancelled = await service.Cancel(admin, institution, first.Id,
            new(first.Version, "Repair no longer required"), default);
        Assert.Equal("Cancelled", cancelled.Status);
        var after = await service.Create(admin, institution,
            create with { ClientRequestId = Guid.NewGuid() }, default);
        Assert.Equal("Active", after.Status);
    }

    [Fact]
    public async Task ReservationCannotBeReadFromAnotherInstitution()
    {
        var (institution, room, admin) = await Setup();
        var (otherInstitution, _, _) = await Setup();
        await using var scope = fixture.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<CampusReservationService>();
        var from = DateTime.UtcNow.AddDays(1);
        await service.Create(admin, institution, new(Guid.NewGuid(), room, "Maintenance",
            from, from.AddHours(1), "Air conditioning repair"), default);
        await Assert.ThrowsAsync<ApiFault>(() => service.List(admin, otherInstitution, null,
            from.AddMinutes(-10), from.AddHours(2), 1, default));
    }
}
