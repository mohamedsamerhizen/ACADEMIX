using Academix.Api.DTOs.Examinations;
using Academix.Api.Middleware;
using Academix.Api.Services.Examinations;

namespace Academix.Tests.Unit;

public sealed class ExamValidationTests
{
    private static ExamDraftRequest Draft() => new("Midterm", "Exam instructions", 90, 1,
        new DateTime(2026, 12, 1, 9, 0, 0, DateTimeKind.Utc),
        new DateTime(2026, 12, 1, 12, 0, 0, DateTimeKind.Utc), false);

    [Fact]
    public void ValidDraftReturnsNormalizedTitle()
    {
        var (title, _) = ExamValidation.ValidateDraft(Draft() with { Title = "  Midterm  " });
        Assert.Equal("Midterm", title);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(481)]
    public void InvalidDurationRejected(int duration)
        => Assert.Throws<ApiFault>(() => ExamValidation.ValidateDraft(Draft() with { DurationMinutes = duration }));

    [Theory]
    [InlineData(0)]
    [InlineData(11)]
    public void InvalidAttemptLimitRejected(int attempts)
        => Assert.Throws<ApiFault>(() => ExamValidation.ValidateDraft(Draft() with { MaxAttempts = attempts }));

    [Fact]
    public void InvalidUtcWindowRejected()
        => Assert.Throws<ApiFault>(() => ExamValidation.ValidateDraft(Draft() with
        { OpensAtUtc = DateTime.SpecifyKind(Draft().OpensAtUtc, DateTimeKind.Local) }));

    [Fact]
    public void EmptyQuestionSelectionRejected()
        => Assert.Throws<ApiFault>(() => ExamValidation.ValidateQuestions(new(1, [])));

    [Fact]
    public void DuplicateQuestionRejected()
    {
        var id = Guid.NewGuid();
        Assert.Throws<ApiFault>(() => ExamValidation.ValidateQuestions(new(1,
            [new(id, 1, 2m), new(id, 2, 3m)])));
    }

    [Theory]
    [InlineData("0")]
    [InlineData("1000.001")]
    public void InvalidQuestionPointsRejected(string pointsText)
        => Assert.Throws<ApiFault>(() => ExamValidation.ValidateQuestions(new(1,
            [new(Guid.NewGuid(), 1, decimal.Parse(pointsText, System.Globalization.CultureInfo.InvariantCulture))])));

    [Fact]
    public void ValidQuestionSelectionMaintainsOrder()
    {
        var a = Guid.NewGuid();
        var b = Guid.NewGuid();
        var selected = ExamValidation.ValidateQuestions(new(1,
            [new(a, 2, 3.5m), new(b, 1, 4m)]));
        Assert.Equal([a, b], selected.Select(q => q.QuestionId).ToArray());
    }
}
