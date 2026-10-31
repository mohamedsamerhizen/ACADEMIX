using Academix.Api.DTOs.Curriculum;
using Academix.Api.Middleware;
using Academix.Api.Services.Curriculum;

namespace Academix.Tests.Unit;

public sealed class CurriculumPolicyTests
{
    private static readonly IReadOnlyList<CurriculumBandInput> Scale =
    [
        new(0m, 0m, "F"), new(50m, 1m, "D"), new(60m, 2m, "C"),
        new(70m, 3m, "B"), new(85m, 4m, "A")
    ];

    [Fact]
    public void ValidatesCreditHoursAndRejectsDuplicateCourse()
    {
        Assert.Equal(3.5m, CurriculumPolicy.ValidateCredits(3.5m));
        Assert.Throws<ApiFault>(() => CurriculumPolicy.ValidateCredits(0m));
        Assert.Throws<ApiFault>(() => CurriculumPolicy.ValidateCredits(31m));
        Assert.Throws<ApiFault>(() => CurriculumPolicy.ValidateCredits(3.123m));
        var id = Guid.NewGuid();
        Assert.Throws<ApiFault>(() => CurriculumPolicy.ValidateCourses(
            [new(id, "Required", 3, 1), new(id, "Elective", 2, 2)]));
    }

    [Fact]
    public void GradeBandsRejectInvalidThresholdsAndPointOrder()
    {
        CurriculumPolicy.ValidateScale(Scale);
        Assert.Throws<ApiFault>(() => CurriculumPolicy.ValidateScale(
            [new(10m, 0m, "F"), new(60m, 2m, "C")]));
        Assert.Throws<ApiFault>(() => CurriculumPolicy.ValidateScale(
            [new(0m, 0m, "F"), new(60m, 3m, "B"), new(80m, 2m, "C")]));
        Assert.Throws<ApiFault>(() => CurriculumPolicy.ValidateScale(
            [new(0m, 0m, "F"), new(60m, 2m, "C"), new(60m, 3m, "B")]));
        Assert.Equal(3m, CurriculumPolicy.Band(84m, Scale).GradePoints);
        Assert.Equal(4m, CurriculumPolicy.Band(100m, Scale).GradePoints);
    }

    [Fact]
    public void PlanRequiresValidCatalogAndGpaLimits()
    {
        CurriculumPolicy.ValidatePlan(new CreateCurriculumPlan(2026, 1, "Engineering", 120m, 20m, 2m));
        Assert.Throws<ApiFault>(() => CurriculumPolicy.ValidatePlan(
            new CreateCurriculumPlan(2026, 1, "Engineering", 120m, 20m, 4.01m)));
    }

    [Fact]
    public void GpaUsesCreditWeightsAndReturnsNullWithNoGrades()
    {
        Assert.Null(CurriculumPolicy.WeightedGpa([]));
        Assert.Equal(3.25m, CurriculumPolicy.WeightedGpa([(3m, 4m), (1m, 1m)]));
    }

    [Fact]
    public void RepeatedCourseCountsOnceAtBestResult()
    {
        var course = Guid.NewGuid(); var term1 = Guid.NewGuid(); var term2 = Guid.NewGuid();
        var selected = CurriculumPolicy.BestAttempts([
            new(course, term1, "CS101", 45m, false),
            new(course, term2, "CS101", 90m, true)
        ]);
        Assert.Single(selected);
        Assert.Equal(90m, selected[0].Percentage);
    }

    [Fact]
    public void GraduationAuditRejectsMissingRequiredAndUnmetElectiveCredits()
    {
        var core = Guid.NewGuid(); var elective = Guid.NewGuid();
        IReadOnlyList<CurriculumCourseInput> courses =
            [new(core, "Required", 3m, 1), new(elective, "Elective", 2m, 2)];
        var result = CurriculumPolicy.Audit(courses, [new(elective, Guid.NewGuid(), "CS202", 80m, true)],
            Scale, 5m, 2m, 2m);
        Assert.Equal(1, result.MissingRequired);
        Assert.Contains("Missing required courses.", result.Blockers);
        Assert.Contains("Insufficient total credits.", result.Blockers);
    }

    [Fact]
    public void GraduationAuditRequiresPassingGradesAndMinimumGpa()
    {
        var core = Guid.NewGuid(); var elective = Guid.NewGuid();
        IReadOnlyList<CurriculumCourseInput> courses =
            [new(core, "Required", 3m, 1), new(elective, "Elective", 2m, 2)];
        var attempts = new[] { new TranscriptAttempt(core, Guid.NewGuid(), "CS101", 72m, true),
            new TranscriptAttempt(elective, Guid.NewGuid(), "CS201", 87m, true) };
        var result = CurriculumPolicy.Audit(courses, attempts, Scale, 5m, 2m, 3.25m);
        Assert.Empty(result.Blockers);
        Assert.Equal(3m, result.RequiredCredits);
        Assert.Equal(2m, result.ElectiveCredits);
        Assert.Equal(3.40m, result.Gpa);
        var tooHigh = CurriculumPolicy.Audit(courses, attempts, Scale, 5m, 2m, 3.5m);
        Assert.Contains("Graduation GPA requirement not met.", tooHigh.Blockers);
        var failed = CurriculumPolicy.Audit(courses,
            [new(core, Guid.NewGuid(), "CS101", 90m, false), attempts[1]], Scale, 5m, 2m, 2m);
        Assert.Equal(1, failed.MissingRequired);
    }
}
