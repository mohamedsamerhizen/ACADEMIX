using Academix.Api.DTOs.Curriculum;
using Academix.Api.Services.Curriculum;

namespace Academix.Tests.Unit;

public sealed class LifecycleEquivalencyTests
{
    [Fact]
    public void ApprovedTransferSatisfiesCourseButNeverFabricatesGpaPoints()
    {
        var transferCourse = Guid.NewGuid(); var gradedCourse = Guid.NewGuid();
        var courses = new[] {
            new CurriculumCourseInput(transferCourse, "Required", 3m, 1),
            new CurriculumCourseInput(gradedCourse, "Required", 3m, 1)
        };
        var attempts = new[] { new TranscriptAttempt(gradedCourse, Guid.NewGuid(), "ENG102", 90m, true) };
        var bands = new[] {
            new CurriculumBandInput(0m, 0m, "F"),
            new CurriculumBandInput(60m, 2m, "C"),
            new CurriculumBandInput(90m, 4m, "A")
        };
        var without = CurriculumPolicy.Audit(courses, attempts, bands, 6m, 0m, 2m);
        Assert.Single(without.Blockers, b => b.Contains("Missing required"));
        Assert.Equal(3m, without.RequiredCredits);
        var with = CurriculumPolicy.Audit(courses, attempts, bands, 6m, 0m, 2m, [transferCourse]);
        Assert.Empty(with.Blockers);
        Assert.Equal(6m, with.RequiredCredits);
        Assert.Equal(4m, with.Gpa);
    }

    [Fact]
    public void SameCourseCreditIsCountedOnlyOnceWhenGradedAndTransferred()
    {
        var id = Guid.NewGuid();
        var courses = new[] { new CurriculumCourseInput(id, "Required", 3m, 1) };
        var attempts = new[] { new TranscriptAttempt(id, Guid.NewGuid(), "ENG101", 75m, true) };
        var bands = new[] { new CurriculumBandInput(0m, 0m, "F"), new CurriculumBandInput(60m, 2m, "C") };
        var result = CurriculumPolicy.Audit(courses, attempts, bands, 3m, 0m, 2m, [id]);
        Assert.Equal(3m, result.RequiredCredits);
        Assert.Empty(result.Blockers);
    }
}
