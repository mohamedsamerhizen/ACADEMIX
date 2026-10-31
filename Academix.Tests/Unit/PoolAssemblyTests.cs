using Academix.Api.DTOs.Examinations;
using Academix.Api.Middleware;
using Academix.Api.Services.Examinations;

namespace Academix.Tests.Unit;

public sealed class PoolAssemblyTests
{
    private static PoolAssembly.Candidate C(string kind, string difficulty)
        => new(Guid.NewGuid(), 1, kind, difficulty);

    [Fact]
    public void SpecificDifficultyIsAllocatedBeforeWildcardToAvoidUnnecessaryShortages()
    {
        var easy = C("SingleChoice", "Easy");
        var hard = C("SingleChoice", "Hard");
        var rules = new List<ExamPoolView>
        {
            new(1, "SingleChoice", null, 1, 2m),
            new(2, "SingleChoice", "Hard", 1, 4m)
        };
        var allocations = PoolAssembly.Allocate(rules, [easy, hard], [], _ => 0);
        Assert.Equal(hard.QuestionId, Assert.Single(allocations, a => a.PoolPosition == 2).QuestionId);
        Assert.Equal(easy.QuestionId, Assert.Single(allocations, a => a.PoolPosition == 1).QuestionId);
    }

    [Fact]
    public void AllocatorNeverReusesManualOrPooledQuestions()
    {
        var first = C("Essay", "Easy");
        var second = C("Essay", "Easy");
        var third = C("Essay", "Easy");
        var pools = new List<ExamPoolView>
        {
            new(1, "Essay", "Easy", 1, 2m),
            new(2, "Essay", null, 1, 3m)
        };
        var allocated = PoolAssembly.Allocate(pools, [first, second, third], [first.QuestionId], _ => 0);
        Assert.Equal(2, allocated.Count);
        Assert.Equal(2, allocated.Select(a => a.QuestionId).Distinct().Count());
        Assert.DoesNotContain(allocated, a => a.QuestionId == first.QuestionId);
    }

    [Fact]
    public void ShortageFailsRatherThanSilentlyPublishingFewerQuestions()
    {
        var candidate = C("TrueFalse", "Medium");
        var pools = new List<ExamPoolView> { new(1, "TrueFalse", "Medium", 2, 1m) };
        var error = Assert.Throws<ApiFault>(() => PoolAssembly.Allocate(pools, [candidate], [], _ => 0));
        Assert.Equal(409, error.StatusCode);
    }

    [Fact]
    public void InvalidPoolKindRejected()
        => Assert.Throws<ApiFault>(() => PoolAssembly.Validate(new(1, [new("Unsafe", "Easy", 1, 1m)])));

    [Fact]
    public void NegativeAndZeroPoolQuantityRejected()
        => Assert.Throws<ApiFault>(() => PoolAssembly.Validate(new(1, [new("Essay", null, 0, 2m)])));

    [Fact]
    public void PoolPointPrecisionIsBounded()
        => Assert.Throws<ApiFault>(() => PoolAssembly.Validate(new(1, [new("Essay", null, 1, 1.123m)])));

    [Fact]
    public void EmptyArrayExplicitlyClearsPools()
        => Assert.Empty(PoolAssembly.Validate(new(1, [])));

    [Fact]
    public void NullPoolsArrayRejected()
        => Assert.Throws<ApiFault>(() => PoolAssembly.Validate(new(1, null)));

    [Fact]
    public void CombinedQuestionCountAndScoreLimitsAreEnforced()
    {
        var pools = new List<ExamPoolView> { new(1, "Essay", "Hard", 3, 5m) };
        Assert.Throws<ApiFault>(() => PoolAssembly.ValidateCombined(198, 50m, pools));
        Assert.Throws<ApiFault>(() => PoolAssembly.ValidateCombined(1, 9990m, pools));
        PoolAssembly.ValidateCombined(1, 8m, pools);
    }

    [Fact]
    public void PoolsOverLimitAreRejected()
    {
        var tooMany = Enumerable.Range(0, 21).Select(_ => new ExamPoolRequest("Essay", null, 1, 1m)).ToList();
        Assert.Throws<ApiFault>(() => PoolAssembly.Validate(new(1, tooMany)));
    }

    [Fact]
    public void DraftManualQuestionValidatorRetainsExistingDefaultBehavior()
    {
        Assert.Throws<ApiFault>(() => ExamValidation.ValidateQuestions(new(1, [])));
        Assert.Empty(ExamValidation.ValidateQuestions(new(1, []), allowEmpty: true));
    }
}
