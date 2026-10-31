using Academix.Api.Services.AcademicOperations;
using Academix.Api.Middleware;

namespace Academix.Tests.Unit;

public sealed class Phase22CampusSchedulingTests
{
    [Theory]
    [InlineData(540, 60, 600, 60, false)]
    [InlineData(540, 60, 599, 60, true)]
    [InlineData(600, 30, 540, 60, false)]
    [InlineData(600, 30, 540, 61, true)]
    [InlineData(600, 60, 600, 60, true)]
    public void WeeklyIntervalsUseHalfOpenBoundaries(int a, int durationA, int b, int durationB, bool conflict)
        => Assert.Equal(conflict, CampusSchedulingRules.Overlaps(a, durationA, b, durationB));

    [Fact] public void UtcIntervalsUseHalfOpenBoundaries()
    {
        var start = new DateTime(2026, 11, 1, 10, 0, 0, DateTimeKind.Utc);
        Assert.False(CampusSchedulingRules.Overlaps(start, start.AddHours(1), start.AddHours(1), start.AddHours(2)));
        Assert.True(CampusSchedulingRules.Overlaps(start, start.AddHours(1), start.AddMinutes(59), start.AddHours(2)));
    }

    [Theory]
    [InlineData("Classroom", 30)]
    [InlineData("Laboratory", 60)]
    [InlineData("LectureHall", 500)]
    [InlineData("SeminarRoom", 1)]
    public void SupportedRoomKindsAndCapacitiesAreAccepted(string kind, int capacity)
        => CampusSchedulingRules.Room("R1", "Room One", kind, capacity);

    [Theory]
    [InlineData("Classroom", 0)]
    [InlineData("Auditorium", 30)]
    [InlineData("Laboratory", 10001)]
    public void InvalidRoomsAreRejected(string kind, int capacity)
        => Assert.Throws<ApiFault>(() => CampusSchedulingRules.Room("R1", "Room One", kind, capacity));

    [Theory]
    [InlineData("  Lab relocation  ", "Lab relocation")]
    [InlineData(null, "Timetable updated")]
    public void RevisionReasonIsNormalized(string? entered, string expected)
        => Assert.Equal(expected, CampusSchedulingRules.RevisionReason(entered, "Timetable updated"));

    [Theory]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("  ")]
    public void InvalidRevisionReasonsAreRejected(string entered)
        => Assert.Throws<ApiFault>(() => CampusSchedulingRules.RevisionReason(entered, "Updated"));

    [Fact] public void RevisionReasonIsLengthBounded()
        => Assert.Throws<ApiFault>(() => CampusSchedulingRules.RevisionReason(new string('x', 501), "Updated"));

    [Fact] public void SlotNoOpComparisonIgnoresOnlyOptimisticVersion()
    {
        var initial = new Academix.Api.DTOs.AcademicOperations.TimetableSlotState(1, 540, 60, "A1", null, true, 1);
        Assert.True(CampusSchedulingRules.SameSlot(initial, initial with { Version = 2 }));
        Assert.False(CampusSchedulingRules.SameSlot(initial, initial with { StartsAtMinute = 600 }));
        Assert.False(CampusSchedulingRules.SameSlot(initial, initial with { IsActive = false }));
        Assert.False(CampusSchedulingRules.SameSlot(initial, initial with { RoomId = Guid.NewGuid() }));
    }

    [Fact] public void BuildingCodeAndNameAreValidated()
    {
        CampusSchedulingRules.Building("BLD", "Engineering Building");
        Assert.Throws<ApiFault>(() => CampusSchedulingRules.Building("", "Engineering Building"));
        Assert.Throws<ApiFault>(() => CampusSchedulingRules.Building("BLD", ""));
    }
}
