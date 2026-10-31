using Academix.Api.DTOs.Workspaces;
using Academix.Api.Middleware;
using Academix.Api.Services.Workspaces;

namespace Academix.Tests.Unit;

public sealed class WorkspacePolicyTests
{
    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, 20)]
    [InlineData(10000, 199980)]
    public void PaginationIsDeterministicAndBounded(int page, int expectedOffset)
        => Assert.Equal(expectedOffset, WorkspacePolicy.Offset(page));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(10001)]
    public void InvalidPagesAreRejected(int page)
        => Assert.Equal(400, Assert.Throws<ApiFault>(() => WorkspacePolicy.Offset(page)).StatusCode);

    [Theory]
    [InlineData(0)]
    [InlineData(61)]
    public void CalendarRejectsUnboundedRanges(int days)
        => Assert.Throws<ApiFault>(() => WorkspacePolicy.CalendarEnd(DateTime.UnixEpoch, days));

    [Fact]
    public void CalendarRangeUsesUtcAnchor()
        => Assert.Equal(DateTime.UnixEpoch.AddDays(30), WorkspacePolicy.CalendarEnd(DateTime.UnixEpoch, 30));

    [Fact]
    public void InstitutionInputIsNormalizedAndValidated()
    {
        var value = WorkspacePolicy.InstitutionFields("  uni-05 ", "  Engineering College  ");
        Assert.Equal("UNI-05", value.Code);
        Assert.Equal("Engineering College", value.Name);
        Assert.Throws<ApiFault>(() => WorkspacePolicy.InstitutionFields("bad code", "Valid"));
        Assert.Throws<ApiFault>(() => WorkspacePolicy.InstitutionFields("GOOD", ""));
    }

    [Fact]
    public void PublicStudentAndPerformanceContractsCannotExposeAnswerKeys()
    {
        var exposed = new[] { typeof(StudentOverview), typeof(StudentCourseProgress),
            typeof(StudentCalendarItem), typeof(StudentExamResult), typeof(SectionPerformance),
            typeof(SectionStudent), typeof(InstitutionOverview), typeof(InstitutionCoursePerformance) };
        Assert.All(exposed, dto => Assert.DoesNotContain(dto.GetProperties(), property =>
            property.Name.Contains("AnswerKey", StringComparison.OrdinalIgnoreCase) ||
            property.Name.Contains("Password", StringComparison.OrdinalIgnoreCase)));
    }
}
