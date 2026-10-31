using System.Text.Json;
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

public sealed class Phase22SessionReschedulingTests
{
    private static readonly DateTime Now = new(2026, 11, 5, 8, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void ValidRescheduleAcceptsFutureUtcAppointmentAndReason() =>
        CampusSchedulingRules.Reschedule(Now, Now.AddDays(1), Now.AddDays(1).AddHours(2),
            "Distributed systems", "Instructor was unavailable");

    [Theory]
    [InlineData(-1, 60)]
    [InlineData(1, 0)]
    [InlineData(1, 721)]
    public void InvalidTimeWindowRejected(int hoursFromNow, int durationMinutes)
    {
        var start = Now.AddHours(hoursFromNow);
        Assert.Throws<ApiFault>(() => CampusSchedulingRules.Reschedule(Now, start,
            start.AddMinutes(durationMinutes), "Lecture", "Room changed"));
    }

    [Fact]
    public void NonUtcSessionTimesAreRejected() =>
        Assert.Throws<ApiFault>(() => CampusSchedulingRules.Reschedule(Now,
            DateTime.SpecifyKind(Now.AddDays(1), DateTimeKind.Unspecified),
            Now.AddDays(1).AddMinutes(50), "Seminar", "Room changed"));

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    [InlineData("ab")]
    public void RescheduleRequiresMeaningfulReason(string reason) =>
        Assert.Throws<ApiFault>(() => CampusSchedulingRules.Reschedule(Now,
            Now.AddDays(1), Now.AddDays(1).AddHours(1), "Seminar", reason));

    [Fact]
    public void SessionNoOpIgnoresOnlyVersion()
    {
        var a = new ClassSessionState(Now.AddDays(1), Now.AddDays(1).AddHours(1),
            "Seminar", "Planned", Guid.NewGuid(), 1);
        Assert.True(CampusSchedulingRules.SameSession(a, a with { Version = 2 }));
        Assert.False(CampusSchedulingRules.SameSession(a, a with { StartsAtUtc = a.StartsAtUtc.AddMinutes(1) }));
        Assert.False(CampusSchedulingRules.SameSession(a, a with { Topic = "Updated" }));
    }

    [Fact]
    public void SessionSnapshotPreservesTimesSlotAndVersion()
    {
        var state = new ClassSessionState(Now.AddDays(1), Now.AddDays(1).AddHours(2),
            "Lecture", "Planned", Guid.NewGuid(), 4);
        Assert.Equal(state, JsonSerializer.Deserialize<ClassSessionState>(JsonSerializer.Serialize(state)));
    }

    [Fact]
    public void OccupancyRequiresBoundedUtcInterval()
    {
        CampusSchedulingRules.OccupancyInterval(Now, Now.AddDays(31));
        Assert.Throws<ApiFault>(() => CampusSchedulingRules.OccupancyInterval(Now, Now.AddDays(32)));
        Assert.Throws<ApiFault>(() => CampusSchedulingRules.OccupancyInterval(Now, Now));
        Assert.Throws<ApiFault>(() => CampusSchedulingRules.OccupancyInterval(
            DateTime.SpecifyKind(Now, DateTimeKind.Local), Now.AddDays(1)));
    }

    [Fact]
    public void MigrationIsAdditiveAndModelDefinesTenantBoundImmutableHistory()
    {
        using var db = new AcademixDbContextFactory().CreateDbContext([]);
        const string id = "20261103120000_Phase22ClassSessionRescheduling";
        var migrations = db.GetService<IMigrationsAssembly>();
        Assert.True(migrations.Migrations.TryGetValue(id, out var type));
        var migration = migrations.CreateMigration(type!, db.Database.ProviderName!);
        Assert.Single(migration.UpOperations.OfType<CreateTableOperation>(), x =>
            x.Name == "academic_class_session_revisions");
        Assert.DoesNotContain(migration.UpOperations, x =>
            x is DropColumnOperation or DropTableOperation or DeleteDataOperation);
        var entity = db.Model.FindEntityType(typeof(ClassSessionRevision));
        Assert.NotNull(entity);
        Assert.Contains(entity!.GetForeignKeys(), fk => fk.PrincipalEntityType.ClrType == typeof(ClassSession) &&
            fk.Properties.Select(p => p.Name).SequenceEqual(new[] { "SessionId", "SectionId", "InstitutionId" }));
        Assert.False(db.Database.HasPendingModelChanges());
    }
}
