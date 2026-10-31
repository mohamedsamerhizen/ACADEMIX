using System.Reflection;
using Academix.Api.DTOs.Examinations;
using Academix.Api.Controllers;
using Microsoft.AspNetCore.Authorization;

namespace Academix.Tests.Security;

public sealed class GradeContractSecurityTests
{
    [Fact]
    public void StudentResultHasNoAnswerKeyOrSubmittedAnswer()
    {
        foreach (var type in new[] { typeof(ExamStudentResult), typeof(ExamGradeSummary), typeof(ExamStudentGradeItemView) })
        {
            Assert.DoesNotContain(type.GetProperties(), p => p.Name.Contains("AnswerKey", StringComparison.OrdinalIgnoreCase));
        }
        Assert.Null(typeof(ExamStudentGradeItemView).GetProperty("StudentAnswerJson"));
    }

    [Fact]
    public void AllNewEndpointsRequireAuthorization()
    {
        Assert.NotEmpty(typeof(ExamGradingController).GetCustomAttributes<AuthorizeAttribute>());
        Assert.NotEmpty(typeof(ExamResultsController).GetCustomAttributes<AuthorizeAttribute>());
    }
}
