using Academix.Api.DTOs.AdvancedLearning;
using Academix.Api.Middleware;
using Academix.Api.Services.AdvancedLearning;

namespace Academix.Tests.Unit;

public sealed class RubricPolicyTests
{
    [Fact]
    public void ValidRubricHasBoundedDistinctPositions()
    {
        RubricPolicy.Validate([
            new RubricCriterionInput(1, "Research", "Evidence", 60),
            new RubricCriterionInput(2, "Presentation", "Clarity", 40)
        ]);
    }

    [Fact]
    public void DuplicatePositionsAreRejected()
    {
        Assert.Throws<ApiFault>(() => RubricPolicy.Validate([
            new RubricCriterionInput(1, "Research", "Evidence", 60),
            new RubricCriterionInput(1, "Presentation", "Clarity", 40)
        ]));
    }

    [Fact]
    public void EmptyOrOversizedRubricIsRejected()
    {
        Assert.Throws<ApiFault>(() => RubricPolicy.Validate([]));
        Assert.Throws<ApiFault>(() => RubricPolicy.Validate([
            new RubricCriterionInput(1, "Quality", "", 10001)
        ]));
    }

    [Fact]
    public void CompleteCriterionScoresMustMatchRubricMaximums()
    {
        var a = Guid.NewGuid(); var b = Guid.NewGuid();
        var rubric = new[] { (a, 70m), (b, 30m) };
        Assert.Equal(85m, RubricPolicy.Total(rubric, [
            new CriterionScoreInput(a, 60, "Strong"),
            new CriterionScoreInput(b, 25, "Clear")
        ]));
        Assert.Throws<ApiFault>(() => RubricPolicy.Total(rubric, [new CriterionScoreInput(a, 60, "") ]));
        Assert.Throws<ApiFault>(() => RubricPolicy.Total(rubric, [
            new CriterionScoreInput(a, 71, ""), new CriterionScoreInput(b, 25, "")
        ]));
        Assert.Throws<ApiFault>(() => RubricPolicy.Total(rubric, [
            new CriterionScoreInput(a, 60, ""), new CriterionScoreInput(a, 25, "")
        ]));
    }

    [Fact]
    public void CrossRubricCriterionRejected()
    {
        var a = Guid.NewGuid(); var b = Guid.NewGuid();
        Assert.Throws<ApiFault>(() => RubricPolicy.Total([(a, 100m)], [new CriterionScoreInput(b, 50, "") ]));
    }
}
