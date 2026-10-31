using System.Text.Json;
using Academix.Api.Data;
using Academix.Api.Domain.AcademicOperations;
using Academix.Api.DTOs.AcademicOperations;
using Academix.Api.Services.AcademicOperations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace Academix.Tests.Unit;

public sealed class Phase22TimetableRevisionTests
{
    [Fact]
    public void RevisionMigrationIsAdditiveAndPreservesEarlierPhase()
    {
        using var db = new AcademixDbContextFactory().CreateDbContext([]);
        var assembly = db.GetService<IMigrationsAssembly>();
        Assert.Contains("20261103100000_Phase22CampusFacilities", assembly.Migrations.Keys);
        const string revisionId = "20261103110000_Phase22TimetableRevisionHistory";
        Assert.True(assembly.Migrations.TryGetValue(revisionId, out var migrationType));
        var migration = assembly.CreateMigration(migrationType!, db.Database.ProviderName!);
        Assert.Single(migration.UpOperations.OfType<CreateTableOperation>(), x =>
            x.Name == "academic_timetable_slot_revisions");
        Assert.DoesNotContain(migration.UpOperations, x => x is DropTableOperation or DropColumnOperation or DeleteDataOperation);
        Assert.False(db.Database.HasPendingModelChanges());
    }

    [Fact]
    public void HistoryHasInstitutionBoundSlotAndActorReferences()
    {
        using var db = new AcademixDbContextFactory().CreateDbContext([]);
        var entity = db.Model.FindEntityType(typeof(TimetableSlotRevision))!;
        Assert.NotNull(entity);
        Assert.Contains(entity.GetForeignKeys(), fk => fk.PrincipalEntityType.ClrType == typeof(TimetableSlot) &&
            fk.Properties.Select(p => p.Name).SequenceEqual(new[] { "SlotId", "SectionId", "InstitutionId" }));
        Assert.Contains(entity.GetIndexes(), index => index.Properties.Select(p => p.Name)
            .SequenceEqual(new[] { "InstitutionId", "SectionId", "SlotId", "AtUtc" }));
    }

    [Fact]
    public void StateSnapshotsRoundTripWithRoomAndVersion()
    {
        var state = new TimetableSlotState(5, 600, 90, "North Hall", Guid.NewGuid(), true, 7);
        var restored = JsonSerializer.Deserialize<TimetableSlotState>(JsonSerializer.Serialize(state));
        Assert.Equal(state, restored);
        Assert.False(CampusSchedulingRules.SameSlot(state, state with { Room = "Another Room" }));
    }
}
