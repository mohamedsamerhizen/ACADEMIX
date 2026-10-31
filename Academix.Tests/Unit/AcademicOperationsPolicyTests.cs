using Academix.Api.DTOs.AcademicOperations;
using Academix.Api.Middleware;
using Academix.Api.Services.AcademicOperations;

namespace Academix.Tests.Unit;

public sealed class AcademicOperationsPolicyTests
{
    [Theory]
    [InlineData(0, 540, 60)]
    [InlineData(8, 540, 60)]
    [InlineData(1, 1430, 60)]
    [InlineData(1, 600, 5)]
    [InlineData(3, 600, 601)]
    public void RejectInvalidSlot(int weekday, int start, int duration)
        => Assert.Throws<ApiFault>(() => AcademicOperationsPolicy.Slot(new SlotUpsert(weekday, start, duration, "B2")));

    [Fact]
    public void AcceptLastMinuteSlotWithBoundary()
        => AcademicOperationsPolicy.Slot(new SlotUpsert(7, 1425, 15, "Lecture Hall"));

    [Fact]
    public void RejectLocalOrUnorderedTimes()
    {
        var now = DateTime.UtcNow;
        Assert.Throws<ApiFault>(() => AcademicOperationsPolicy.TimeRange(DateTime.Now, now.AddDays(1)));
        Assert.Throws<ApiFault>(() => AcademicOperationsPolicy.Interval(now, now));
        Assert.Throws<ApiFault>(() => AcademicOperationsPolicy.TimeRange(now, now.AddDays(61)));
    }

    [Theory]
    [InlineData("Present")]
    [InlineData("Late")]
    [InlineData("Absent")]
    [InlineData("Excused")]
    public void ValidAttendanceStatuses(string status)
        => Assert.Equal(status, AcademicOperationsPolicy.AttendanceStatus(status));

    [Fact]
    public void RejectDuplicateAttendanceRows()
    {
        var id = Guid.NewGuid();
        var batch = new AttendanceBatch([
            new AttendanceUpsert(id, "Present", ""),
            new AttendanceUpsert(id, "Absent", "")]);
        Assert.Throws<ApiFault>(() => AcademicOperationsPolicy.AttendanceBatch(batch));
    }

    [Fact]
    public void ValidateAdministrativeDecisions()
    {
        Assert.Throws<ApiFault>(() => AcademicOperationsPolicy.Decision(new AcademicRequestDecision("Open", "No reason", 1)));
        Assert.Throws<ApiFault>(() => AcademicOperationsPolicy.Decision(new AcademicRequestDecision("Approved", "Ok", 0)));
        AcademicOperationsPolicy.Decision(new AcademicRequestDecision("Rejected", "Lacks documentation", 2));
    }

    [Fact]
    public void RejectShortStudentRequest()
        => Assert.Throws<ApiFault>(() => AcademicOperationsPolicy.Request(
            new AcademicRequestCreate(null, "General", "Hi", "short")));

    [Fact]
    public void CalendarCannotPublishBadAudience()
        => Assert.Throws<ApiFault>(() => AcademicOperationsPolicy.Calendar(new CalendarEventCreate(
            null, null, "Fall Semester", "Starts", DateTime.UtcNow,
            DateTime.UtcNow.AddDays(1), "Anonymous", true)));
}
