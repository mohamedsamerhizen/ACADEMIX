using Academix.Api.DTOs.Examinations;
using Academix.Api.Middleware;
using Academix.Api.Services.Examinations;

namespace Academix.Tests.Unit;

public sealed class QuestionValidationTests
{
    private static QuestionDraftRequest Draft(string kind = "SingleChoice") => new(
        kind, "What is the correct answer?", "Medium", 5m,
        kind is "SingleChoice" or "MultipleChoice"
            ? [new("a", "First"), new("b", "Second"), new("c", "Third")]
            : [],
        kind switch { "SingleChoice" => ["a"], "MultipleChoice" => ["a", "c"], "TrueFalse" => ["true"], _ => [] },
        kind == "ShortText" ? ["alpha", "beta"] : [],
        "Feedback for instructors", ["basics"]);

    [Theory]
    [InlineData("SingleChoice")]
    [InlineData("MultipleChoice")]
    [InlineData("TrueFalse")]
    [InlineData("ShortText")]
    [InlineData("Essay")]
    public void AcceptsSupportedTypes(string kind)
    {
        var (body, answers) = QuestionValidation.Validate(Draft(kind));
        Assert.Equal(kind, body.Kind);
        Assert.Equal(5m, body.Points);
        Assert.NotNull(answers);
    }

    [Fact]
    public void RejectsCorrectOptionOutsideDefinedChoices()
    {
        var draft = Draft() with { CorrectOptionIds = ["missing"] };
        Assert.Throws<ApiFault>(() => QuestionValidation.Validate(draft));
    }

    [Fact]
    public void RejectsDuplicateOptionIdsEvenDifferentCase()
    {
        var draft = Draft() with { Options = [new("a", "First"), new("A", "Second")] };
        Assert.Throws<ApiFault>(() => QuestionValidation.Validate(draft));
    }

    [Fact]
    public void RejectsEssayWithAnswerKey()
    {
        var draft = Draft("Essay") with { CorrectOptionIds = ["a"] };
        Assert.Throws<ApiFault>(() => QuestionValidation.Validate(draft));
    }

    [Fact]
    public void RejectsTrueFalseIncorrectLiteral()
    {
        var draft = Draft("TrueFalse") with { CorrectOptionIds = ["yes"] };
        Assert.Throws<ApiFault>(() => QuestionValidation.Validate(draft));
    }

    [Fact]
    public void RejectsInvalidPointsPrecision()
    {
        var draft = Draft() with { Points = 0.001m };
        Assert.Throws<ApiFault>(() => QuestionValidation.Validate(draft));
    }

    [Fact]
    public void RejectsDuplicateTagsIgnoringCase()
    {
        var draft = Draft() with { Tags = ["math", "MATH"] };
        Assert.Throws<ApiFault>(() => QuestionValidation.Validate(draft));
    }

    [Fact]
    public void RejectsShortTextWithoutAcceptedAnswers()
    {
        var draft = Draft("ShortText") with { AcceptedAnswers = [] };
        Assert.Throws<ApiFault>(() => QuestionValidation.Validate(draft));
    }
}
