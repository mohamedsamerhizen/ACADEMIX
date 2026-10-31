using Academix.Api.Controllers;
using Academix.Api.DTOs.Curriculum;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Academix.Tests.Security;

public sealed class CurriculumContractsTests
{
    [Fact]
    public void ControllerRequiresAuthenticatedUser()
    {
        Assert.NotNull(Attribute.GetCustomAttribute(typeof(CurriculumController), typeof(AuthorizeAttribute)));
        Assert.NotNull(Attribute.GetCustomAttribute(typeof(CurriculumController), typeof(ApiControllerAttribute)));
    }

    [Fact]
    public void StudentGradeReportOnlyContainsDerivedGradesNotPrivateExamAnswers()
    {
        var exposed = typeof(StudentGpaReport).GetProperties().Select(x => x.Name).ToHashSet();
        Assert.DoesNotContain("CorrectAnswer", exposed);
        Assert.DoesNotContain("RubricAnswer", exposed);
        Assert.Contains("CumulativeGpa", exposed);
    }
}
