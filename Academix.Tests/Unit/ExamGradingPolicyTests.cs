using Academix.Api.DTOs.Examinations;
using Academix.Api.Services.Examinations;
using Academix.Api.Middleware;

namespace Academix.Tests.Unit;

public sealed class ExamGradingPolicyTests
{
    private static QuestionBody Body(string kind) => new(kind, "A question?", "Easy", 10,
        [new("A", "One"), new("B", "Two"), new("C", "Three")], null, []);
    private static QuestionAnswerKey Key(string[]? ids = null, string[]? texts = null)
        => new(ids ?? [], texts ?? []);

    [Fact]
    public void ChoiceAndBooleanScoringIsAllOrNothing()
    {
        Assert.Equal(10m, ExamGradingPolicy.AutoScore(Body("SingleChoice"), Key(["A"]), "\"a\"", 10));
        Assert.Equal(0m, ExamGradingPolicy.AutoScore(Body("SingleChoice"), Key(["A"]), "\"B\"", 10));
        Assert.Equal(10m, ExamGradingPolicy.AutoScore(Body("MultipleChoice"), Key(["A", "C"]), "[\"C\",\"A\"]", 10));
        Assert.Equal(0m, ExamGradingPolicy.AutoScore(Body("MultipleChoice"), Key(["A", "C"]), "[\"A\"]", 10));
        Assert.Equal(10m, ExamGradingPolicy.AutoScore(Body("TrueFalse"), Key(["true"]), "true", 10));
        Assert.Equal(0m, ExamGradingPolicy.AutoScore(Body("TrueFalse"), Key(["true"]), "false", 10));
    }

    [Fact]
    public void ShortTextAcceptsCaseWhitespaceAndUnicodeNormalization()
    {
        Assert.Equal(10m, ExamGradingPolicy.AutoScore(Body("ShortText"), Key(texts: ["  Answer "]), "\"answer\"", 10));
        Assert.Equal(0m, ExamGradingPolicy.AutoScore(Body("ShortText"), Key(texts: ["Answer"]), "\"wrong\"", 10));
    }

    [Fact]
    public void EssayRequiresManualReviewUnlessBlank()
    {
        Assert.Null(ExamGradingPolicy.AutoScore(Body("Essay"), Key(), "\"My essay\"", 10));
        Assert.Equal(0m, ExamGradingPolicy.AutoScore(Body("Essay"), Key(), "null", 10));
        Assert.Equal(0m, ExamGradingPolicy.AutoScore(Body("Essay"), Key(), "\"  \"", 10));
    }

    [Fact]
    public void InvalidManualGradesAreRejected()
    {
        Assert.Equal(4.5m, ExamGradingPolicy.ValidateScore(4.5m, 10m));
        Assert.Throws<ApiFault>(() => ExamGradingPolicy.ValidateScore(-1, 10));
        Assert.Throws<ApiFault>(() => ExamGradingPolicy.ValidateScore(11, 10));
        Assert.Throws<ApiFault>(() => ExamGradingPolicy.ValidateScore(1.234m, 10));
    }
}
