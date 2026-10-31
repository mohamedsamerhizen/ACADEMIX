using Academix.Api.Middleware;
using Academix.Api.Services.Quality;

namespace Academix.Tests.Unit;

public sealed class Phase24QualityRulesTests
{
    [Theory]
    [InlineData(" plo_01 ", "PLO_01")]
    [InlineData("abc-12", "ABC-12")]
    public void OutcomeCodeNormalizes(string supplied, string expected)
        => Assert.Equal(expected, QualityRules.Code(supplied));

    [Theory]
    [InlineData("")]
    [InlineData("a")]
    [InlineData("contains space")]
    [InlineData("invalid/char")]
    public void InvalidOutcomeCodesAreRejected(string supplied)
        => Assert.Throws<ApiFault>(() => QualityRules.Code(supplied));

    [Fact]
    public void NoMappingsCannotClaimOutcomesWereAchieved()
    {
        var evidence = QualityRules.Evidence(Guid.NewGuid(), "PLO1", 70, 0, 0, 0);
        Assert.Equal("Unmapped", evidence.AssessmentStatus);
        Assert.Null(evidence.PassingRate);
    }

    [Fact]
    public void MissingPublishedGradesAreNotReportedAsFailure()
    {
        var evidence = QualityRules.Evidence(Guid.NewGuid(), "PLO1", 70, 3, 0, 0);
        Assert.Equal("NoPublishedEvidence", evidence.AssessmentStatus);
        Assert.Null(evidence.PassingRate);
    }

    [Theory]
    [InlineData(10, 7, 70, "ProxyTargetMet")]
    [InlineData(10, 6, 70, "ProxyBelowTarget")]
    [InlineData(8, 3, 40, "ProxyBelowTarget")]
    public void ProxyThresholdUsesGradeRecordsNotStudentClaims(int count, int passed, int target, string expected)
    {
        var evidence = QualityRules.Evidence(Guid.NewGuid(), "PLO1", target, 2, count, passed);
        Assert.Equal(expected, evidence.AssessmentStatus);
        Assert.Equal(count, evidence.PublishedGradeRecords);
        Assert.Equal(QualityRules.EvidenceKind, evidence.EvidenceKind);
    }

    [Fact]
    public void InvalidEvidenceCountsNeverYieldPercentages()
        => Assert.Throws<ApiFault>(() => QualityRules.Evidence(Guid.NewGuid(), "PLO1", 70, 1, 4, 5));

    [Theory]
    [InlineData("Open", "InProgress", "InProgress")]
    [InlineData("Open", "Completed", "Completed")]
    [InlineData("InProgress", "Open", "Open")]
    [InlineData("InProgress", "Completed", "Completed")]
    public void SupportedActionTransitions(string current, string target, string result)
        => Assert.Equal(result, QualityRules.NextActionStatus(current, target));

    [Theory]
    [InlineData("Open", "Open")]
    [InlineData("Completed", "Open")]
    [InlineData("Completed", "Completed")]
    public void InvalidOrRepeatedActionTransitionsAreRejected(string current, string target)
        => Assert.Throws<ApiFault>(() => QualityRules.NextActionStatus(current, target));
}
