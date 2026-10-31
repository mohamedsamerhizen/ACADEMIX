using Academix.Api.Controllers;
using Academix.Api.DTOs.Examinations;
using Microsoft.AspNetCore.Authorization;

namespace Academix.Tests.Security;

public sealed class ExamAttemptContractTests
{
    [Fact]
    public void StudentDtosHaveNoAnswerKeysExplanationsOrGrades()
    {
        Type[] returned = [typeof(StudentExamQuestion), typeof(ExamAttemptDetail),
            typeof(SavedExamAnswer), typeof(ExamSubmissionReceipt), typeof(AvailableExamView)];
        var banned = new[] { "AnswerKey", "Correct", "Explanation", "AcceptedAnswers", "Grade" };
        Assert.All(returned, t => Assert.DoesNotContain(t.GetProperties(), p =>
            banned.Any(b => p.Name.Contains(b, StringComparison.OrdinalIgnoreCase))));
    }

    [Fact]
    public void ExamTakingRequiresAuthentication()
        => Assert.NotEmpty(typeof(ExamTakingController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), true));
}
