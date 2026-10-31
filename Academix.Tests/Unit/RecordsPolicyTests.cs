using Academix.Api.Middleware;
using Academix.Api.Services.Records;

namespace Academix.Tests.Unit;

public sealed class RecordsPolicyTests
{
    [Fact]
    public void PrerequisitesRejectCyclesAndSelfReferences()
    {
        var a = Guid.NewGuid(); var b = Guid.NewGuid(); var c = Guid.NewGuid();
        IReadOnlyDictionary<Guid, Guid[]> graph = new Dictionary<Guid, Guid[]> {
            [a] = [b], [b] = [c] };
        Assert.Throws<ApiFault>(() => RecordsPolicy.DistinctCourses(c, a, graph));
        Assert.Throws<ApiFault>(() => RecordsPolicy.DistinctCourses(a, a, graph));
        RecordsPolicy.DistinctCourses(a, c, new Dictionary<Guid, Guid[]>());
    }

    [Fact]
    public void PrerequisiteMustBePassedAndMeetMinimum()
    {
        Assert.False(RecordsPolicy.Satisfies(70m, [(90m, false)]));
        Assert.False(RecordsPolicy.Satisfies(70m, [(69m, true)]));
        Assert.True(RecordsPolicy.Satisfies(70m, [(60m, true), (75m, true)]));
        Assert.Throws<ApiFault>(() => RecordsPolicy.Percentage(101m));
        Assert.Throws<ApiFault>(() => RecordsPolicy.Percentage(70.123m));
        Assert.Equal(70.25m, RecordsPolicy.Percentage(70.25m));
    }

    [Fact]
    public void StandingHonorsFailuresAndMissingData()
    {
        Assert.Equal("InsufficientData", RecordsPolicy.Standing([]));
        Assert.Equal("Probation", RecordsPolicy.Standing([(95m, true), (30m, false)]));
        Assert.Equal("AtRisk", RecordsPolicy.Standing([(58m, true)]));
        Assert.Equal("GoodStanding", RecordsPolicy.Standing([(80m, true), (85m, true)]));
    }

    [Fact]
    public void DropAndWithdrawHaveDifferentWindows()
    {
        var start = new DateOnly(2026, 11, 1); var end = new DateOnly(2026, 12, 1);
        Assert.Equal("Dropped", RecordsPolicy.Transition(start, end, new DateOnly(2026, 10, 15), "Drop", false));
        Assert.Equal("Withdrawn", RecordsPolicy.Transition(start, end, new DateOnly(2026, 11, 15), "Withdraw", false));
        Assert.Throws<ApiFault>(() => RecordsPolicy.Transition(start, end, new DateOnly(2026, 11, 15), "Drop", false));
        Assert.Throws<ApiFault>(() => RecordsPolicy.Transition(start, end, new DateOnly(2026, 12, 2), "Withdraw", false));
        Assert.Throws<ApiFault>(() => RecordsPolicy.Transition(start, end, new DateOnly(2026, 11, 15), "Withdraw", true));
    }

    [Fact]
    public void ReasonsMustBeMeaningful()
    {
        Assert.Equal("Academic plan changed", RecordsPolicy.Reason("  Academic plan changed  "));
        Assert.Throws<ApiFault>(() => RecordsPolicy.Reason("x"));
    }
}
