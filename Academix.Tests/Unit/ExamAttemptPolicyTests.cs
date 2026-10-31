using System.Text.Json;
using Academix.Api.DTOs.Examinations;
using Academix.Api.Middleware;
using Academix.Api.Services.Examinations;

namespace Academix.Tests.Unit;

public sealed class ExamAttemptPolicyTests
{
    private static readonly QuestionOptionDto[] Options = [new("A", "One"), new("B", "Two"), new("C", "Three")];
    private static SaveExamAnswerRequest Request(long sequence, string value)
        => new(sequence, JsonDocument.Parse(value).RootElement.Clone());

    [Fact]
    public void DeadlineIsBoundedByExamClosure()
    {
        var start = new DateTime(2026, 10, 26, 12, 0, 0, DateTimeKind.Utc);
        Assert.Equal(start.AddMinutes(30), ExamAttemptPolicy.Deadline(start, 30, start.AddHours(2)));
        Assert.Equal(start.AddMinutes(10), ExamAttemptPolicy.Deadline(start, 90, start.AddMinutes(10)));
    }

    [Fact]
    public void OpeningIsInclusiveAndClosureIsExclusive()
    {
        var open = new DateTime(2026, 10, 26, 12, 0, 0, DateTimeKind.Utc);
        Assert.False(ExamAttemptPolicy.CanStart(open.AddTicks(-1), open, open.AddHours(1)));
        Assert.True(ExamAttemptPolicy.CanStart(open, open, open.AddHours(1)));
        Assert.False(ExamAttemptPolicy.CanStart(open.AddHours(1), open, open.AddHours(1)));
        Assert.True(ExamAttemptPolicy.IsExpired("InProgress", open.AddHours(1), open.AddHours(1)));
        Assert.False(ExamAttemptPolicy.IsExpired("Submitted", open, open.AddHours(2)));
    }

    [Fact]
    public void RepeatedSequenceAndValueAreIdempotentButStaleIsNot()
    {
        Assert.True(ExamAttemptPolicy.SameAnswer(8, "\"A\"", 8, "\"A\""));
        Assert.False(ExamAttemptPolicy.SameAnswer(8, "\"A\"", 8, "\"B\""));
        Assert.False(ExamAttemptPolicy.SameAnswer(8, "\"A\"", 7, "\"A\""));
    }

    [Fact]
    public void SingleChoiceMustMatchPublishedOption()
    {
        Assert.Equal("\"A\"", ExamAttemptPolicy.NormalizeAnswer("SingleChoice", Options, Request(1, "\"a\"")));
        Assert.Throws<ApiFault>(() => ExamAttemptPolicy.NormalizeAnswer("SingleChoice", Options, Request(1, "\"wrong\"")));
        Assert.Throws<ApiFault>(() => ExamAttemptPolicy.NormalizeAnswer("SingleChoice", Options, Request(1, "[\"A\"]")));
    }

    [Fact]
    public void MultipleChoiceNormalizesOrderAndRejectsDuplicates()
    {
        Assert.Equal("[\"A\",\"C\"]", ExamAttemptPolicy.NormalizeAnswer("MultipleChoice", Options, Request(1, "[\"C\",\"a\"]")));
        Assert.Throws<ApiFault>(() => ExamAttemptPolicy.NormalizeAnswer("MultipleChoice", Options, Request(1, "[\"A\",\"a\"]")));
        Assert.Throws<ApiFault>(() => ExamAttemptPolicy.NormalizeAnswer("MultipleChoice", Options, Request(1, "[\"A\",\"X\"]")));
    }

    [Fact]
    public void TrueFalseRejectsStringValues()
    {
        Assert.Equal("true", ExamAttemptPolicy.NormalizeAnswer("TrueFalse", [], Request(1, "true")));
        Assert.Throws<ApiFault>(() => ExamAttemptPolicy.NormalizeAnswer("TrueFalse", [], Request(1, "\"true\"")));
    }

    [Fact]
    public void TextResponsesRespectBounds()
    {
        Assert.Equal("\"answer\"", ExamAttemptPolicy.NormalizeAnswer("ShortText", [], Request(1, "\" answer \"")));
        Assert.Throws<ApiFault>(() => ExamAttemptPolicy.NormalizeAnswer("ShortText", [], Request(1, JsonSerializer.Serialize(new string('x', 501)))));
        Assert.Throws<ApiFault>(() => ExamAttemptPolicy.NormalizeAnswer("Essay", [], Request(1, JsonSerializer.Serialize(new string('x', 12001)))));
    }

    [Fact]
    public void NullClearsAnswerButKeepsSequence()
    {
        Assert.Equal("null", ExamAttemptPolicy.NormalizeAnswer("SingleChoice", Options, Request(5, "null")));
        Assert.Throws<ApiFault>(() => ExamAttemptPolicy.NormalizeAnswer("Essay", [], Request(0, "null")));
    }

    [Fact]
    public void PersistedQuestionOrderingDoesNotMutateSource()
    {
        int[] source = [1, 2, 3, 4, 5];
        Assert.Equal(source, ExamAttemptPolicy.OrderQuestions(source, false));
        var shuffled = ExamAttemptPolicy.OrderQuestions(source, true);
        Assert.Equal(source.Order(), shuffled.Order());
        Assert.Equal([1, 2, 3, 4, 5], source);
    }
}
