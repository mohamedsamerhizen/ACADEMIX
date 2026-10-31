using Academix.Api.Domain.AcademicOperations;
using Academix.Api.DTOs.AcademicOperations;
using Academix.Api.Middleware;
using Academix.Api.Services.AcademicOperations;
using Academix.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;

namespace Academix.Tests.Unit;

public sealed class Phase22PhysicalVenueTests
{
    private static readonly DateTime Now = new(2026, 11, 1, 10, 0, 0, DateTimeKind.Utc);
    private static CampusReservationCreate Maintenance(DateTime start, DateTime end) =>
        new(Guid.NewGuid(), Guid.NewGuid(), "Maintenance", start, end, "Room maintenance");
    private static CampusReservationCreate Exam(DateTime start, DateTime end) =>
        new(Guid.NewGuid(), Guid.NewGuid(), "Exam", start, end, "On-campus final exam",
            Guid.NewGuid(), Guid.NewGuid(), 30);

    [Fact] public void AValidMaintenanceWindowIsAccepted()
        => CampusReservationPolicy.Validate(Maintenance(Now.AddDays(1), Now.AddDays(4)), Now);

    [Fact] public void AValidPhysicalExamDoesNotChangeElectronicWindow()
        => CampusReservationPolicy.Validate(Exam(Now.AddDays(2), Now.AddDays(2).AddHours(2)), Now);

    [Theory]
    [InlineData(0)]
    [InlineData(-30)]
    public void NonFutureReservationIsRejected(int minutes)
        => Assert.Throws<ApiFault>(() => CampusReservationPolicy.Validate(
            Maintenance(Now.AddMinutes(minutes), Now.AddHours(3)), Now));

    [Fact] public void PastTimeWindowIsRejected()
        => Assert.Throws<ApiFault>(() => CampusReservationPolicy.Validate(
            Maintenance(Now.AddHours(2), Now.AddHours(1)), Now));

    [Fact] public void LocalClockTimesCannotBeUsedForPhysicalRooms()
        => Assert.Throws<ApiFault>(() => CampusReservationPolicy.Validate(
            Maintenance(DateTime.SpecifyKind(Now.AddHours(1), DateTimeKind.Local), Now.AddHours(2)), Now));

    [Fact] public void MaintenanceCannotClaimExamMetadata()
        => Assert.Throws<ApiFault>(() => CampusReservationPolicy.Validate(
            Maintenance(Now.AddDays(1), Now.AddDays(2)) with { SeatCount = 10 }, Now));

    [Fact] public void PhysicalExamRequiresExistingLinkageAndInvigilator()
    {
        var request = Exam(Now.AddDays(1), Now.AddDays(1).AddHours(1));
        Assert.Throws<ApiFault>(() => CampusReservationPolicy.Validate(request with { ExamId = null }, Now));
        Assert.Throws<ApiFault>(() => CampusReservationPolicy.Validate(request with { InvigilatorId = null }, Now));
        Assert.Throws<ApiFault>(() => CampusReservationPolicy.Validate(request with { SeatCount = 0 }, Now));
    }

    [Fact] public void ExamDurationIsBounded()
        => Assert.Throws<ApiFault>(() => CampusReservationPolicy.Validate(
            Exam(Now.AddDays(1), Now.AddDays(2)), Now));

    [Fact] public void MaintenanceMaximumDurationIsBounded()
        => Assert.Throws<ApiFault>(() => CampusReservationPolicy.Validate(
            Maintenance(Now.AddDays(1), Now.AddDays(33)), Now));

    [Fact] public void MissingRequestIdIsRejected()
        => Assert.Throws<ApiFault>(() => CampusReservationPolicy.Validate(
            Maintenance(Now.AddDays(1), Now.AddDays(2)) with { ClientRequestId = Guid.Empty }, Now));

    [Fact] public void IdempotencyRequiresIdenticalPayload()
    {
        var input = Exam(Now.AddDays(1), Now.AddDays(1).AddHours(1));
        var saved = new CampusRoomReservation { RoomId = input.RoomId, Kind = input.Kind,
            ExamId = input.ExamId, InvigilatorId = input.InvigilatorId, SeatCount = input.SeatCount,
            StartsAtUtc = input.StartsAtUtc, EndsAtUtc = input.EndsAtUtc, Reason = input.Reason };
        Assert.True(CampusReservationPolicy.Matches(input, saved));
        Assert.False(CampusReservationPolicy.Matches(input with { SeatCount = 31 }, saved));
        Assert.False(CampusReservationPolicy.Matches(input with { RoomId = Guid.NewGuid() }, saved));
    }

    [Fact] public void CancellationRequiresVersionAndAuditableReason()
    {
        CampusReservationPolicy.ValidateCancel(new(1, "Room repaired"));
        Assert.Throws<ApiFault>(() => CampusReservationPolicy.ValidateCancel(new(0, "Room repaired")));
        Assert.Throws<ApiFault>(() => CampusReservationPolicy.ValidateCancel(new(1, "")));
    }

    [Fact] public void HalfOpenIntervalAllowsAdjacentReservations()
    {
        Assert.False(CampusSchedulingRules.Overlaps(Now, Now.AddHours(1),
            Now.AddHours(1), Now.AddHours(2)));
        Assert.True(CampusSchedulingRules.Overlaps(Now, Now.AddHours(1),
            Now.AddMinutes(59), Now.AddHours(2)));
    }

    [Fact] public void Phase22FinalMigrationIsAdditiveAndTenantLinked()
    {
        using var db = new AcademixDbContextFactory().CreateDbContext([]);
        var assembly = db.GetService<IMigrationsAssembly>();
        const string id = "20261103140000_Phase22PhysicalVenueAllocations";
        Assert.True(assembly.Migrations.TryGetValue(id, out var migrationType));
        var migration = assembly.CreateMigration(migrationType!, db.Database.ProviderName!);
        Assert.Contains(migration.UpOperations.OfType<CreateTableOperation>(), op =>
            op.Name == "campus_room_reservations" &&
            op.ForeignKeys.Any(fk => fk.PrincipalTable == "campus_rooms"));
        Assert.DoesNotContain(migration.UpOperations, op => op is DropTableOperation or DropColumnOperation);
        var entity = db.Model.FindEntityType(typeof(CampusRoomReservation));
        Assert.NotNull(entity);
        Assert.Contains(entity!.GetForeignKeys(), x => x.PrincipalEntityType.ClrType == typeof(CampusRoom));
        Assert.Contains(entity.GetIndexes(), x => x.Properties.Any(p => p.Name == "ClientRequestId") && x.IsUnique);
        Assert.False(db.Database.HasPendingModelChanges());
    }
}
