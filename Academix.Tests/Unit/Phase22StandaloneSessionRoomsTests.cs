using Academix.Api.Data;
using Academix.Api.Domain.AcademicOperations;
using Academix.Api.DTOs.AcademicOperations;
using Academix.Api.Middleware;
using Academix.Api.Services.AcademicOperations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace Academix.Tests.Unit;

public sealed class Phase22StandaloneSessionRoomsTests
{
    [Fact]
    public void StandaloneSessionMayAllocateAnExplicitRoom()
    {
        var room = Guid.NewGuid();
        Assert.Equal(room, CampusSchedulingRules.ResolveSessionRoom(null, null, null, room, false));
    }

    [Fact]
    public void OmittedRoomPreservesExistingStandaloneRoom()
    {
        var room = Guid.NewGuid();
        Assert.Equal(room, CampusSchedulingRules.ResolveSessionRoom(null, null, room, null, false));
    }

    [Fact]
    public void StandaloneRoomCanBeExplicitlyReleased()
    {
        Assert.Null(CampusSchedulingRules.ResolveSessionRoom(null, null, Guid.NewGuid(), null, true));
    }

    [Fact]
    public void LinkedSessionInheritsSlotRoom()
    {
        var slot = Guid.NewGuid(); var room = Guid.NewGuid();
        Assert.Equal(room, CampusSchedulingRules.ResolveSessionRoom(slot, room, null, null, false));
        Assert.Null(CampusSchedulingRules.ResolveSessionRoom(slot, null, null, null, false));
    }

    [Fact]
    public void LinkedSessionCannotOverrideOrRemoveRoom()
    {
        var slot = Guid.NewGuid(); var room = Guid.NewGuid();
        Assert.Throws<ApiFault>(() => CampusSchedulingRules.ResolveSessionRoom(slot, room, room, Guid.NewGuid(), false));
        Assert.Throws<ApiFault>(() => CampusSchedulingRules.ResolveSessionRoom(slot, room, room, null, true));
        Assert.Throws<ApiFault>(() => CampusSchedulingRules.ResolveSessionRoom(null, null, room, Guid.NewGuid(), true));
    }

    [Fact]
    public void SessionRevisionTracksRoomAssignment()
    {
        var state = new ClassSessionState(DateTime.UtcNow.AddHours(1), DateTime.UtcNow.AddHours(2),
            "Seminar", "Planned", null, 1);
        Assert.False(CampusSchedulingRules.SameSession(state, state with { RoomId = Guid.NewGuid() }));
    }

    [Fact]
    public void ExistingSessionContractsRemainBackwardsCompatible()
    {
        var created = new SessionCreate(DateTime.UtcNow.AddHours(1), DateTime.UtcNow.AddHours(2), "Class");
        Assert.Null(created.RoomId);
        var view = new SessionView(Guid.NewGuid(), Guid.NewGuid(), created.StartsAtUtc,
            created.EndsAtUtc, created.Topic, "Planned", null, 1);
        Assert.Null(view.RoomId);
    }

    [Fact]
    public void MigrationIsAdditiveAndTenantScoped()
    {
        using var db = new AcademixDbContextFactory().CreateDbContext([]);
        var migrations = db.GetService<IMigrationsAssembly>();
        const string id = "20261103130000_Phase22StandaloneSessionRooms";
        Assert.True(migrations.Migrations.TryGetValue(id, out var type));
        var migration = migrations.CreateMigration(type!, db.Database.ProviderName!);
        Assert.Contains(migration.UpOperations.OfType<AddColumnOperation>(), op =>
            op.Table == "academic_class_sessions" && op.Name == "RoomId" && op.IsNullable);
        Assert.DoesNotContain(migration.UpOperations, op => op is DropTableOperation or DropColumnOperation);
        var entity = db.Model.FindEntityType(typeof(ClassSession));
        Assert.NotNull(entity);
        Assert.Contains(entity!.GetForeignKeys(), fk => fk.PrincipalEntityType.ClrType == typeof(CampusRoom) &&
            fk.Properties.Select(p => p.Name).SequenceEqual(new[] { "RoomId", "InstitutionId" }));
        Assert.False(db.Database.HasPendingModelChanges());
    }
}
