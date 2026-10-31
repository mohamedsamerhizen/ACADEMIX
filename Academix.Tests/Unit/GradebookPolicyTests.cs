using Academix.Api.Middleware;
using Academix.Api.Services.Assessment;

namespace Academix.Tests.Unit;

public sealed class GradebookPolicyTests
{
    [Fact]
    public void WeightedComponentsAcrossCategoriesProduceDeterministicFinalPercentage()
    {
        var result = GradebookPolicy.Calculate([
            (40m, new (decimal Weight, decimal Earned, decimal Maximum)[] { (50m, 18m, 20m), (50m, 9m, 10m) }),
            (60m, new (decimal Weight, decimal Earned, decimal Maximum)[] { (100m, 70m, 100m) })
        ]);
        Assert.Equal(78m, result);
    }

    [Theory]
    [InlineData(90, "A")]
    [InlineData(89, "B")]
    [InlineData(70, "C")]
    [InlineData(59, "F")]
    public void LettersFollowPublishedPercentage(int value, string expected)
        => Assert.Equal(expected, GradebookPolicy.Letter(value));

    [Fact]
    public void InvalidWeightsAndRangesAreRejected()
    {
        Assert.Throws<ApiFault>(() => GradebookPolicy.Weight(0));
        Assert.Throws<ApiFault>(() => GradebookPolicy.Weight(101));
        Assert.Throws<ApiFault>(() => GradebookPolicy.Percent(101, 100));
        Assert.Throws<ApiFault>(() => GradebookPolicy.Calculate([
            (60m, new (decimal Weight, decimal Earned, decimal Maximum)[] { (100m, 60m, 100m) })
        ]));
        Assert.Throws<ApiFault>(() => GradebookPolicy.Calculate([
            (100m, new (decimal Weight, decimal Earned, decimal Maximum)[] { (60m, 60m, 100m) })
        ]));
    }

    [Fact]
    public void SourcesRequireValidKindAndId()
    {
        GradebookPolicy.ValidateSource("Manual", null);
        GradebookPolicy.ValidateSource("Exam", Guid.NewGuid());
        Assert.Throws<ApiFault>(() => GradebookPolicy.ValidateSource("Manual", Guid.NewGuid()));
        Assert.Throws<ApiFault>(() => GradebookPolicy.ValidateSource("Exam", null));
        Assert.Throws<ApiFault>(() => GradebookPolicy.ValidateSource("External", Guid.NewGuid()));
    }
}
