using Academix.Api.Controllers;
using Academix.Api.DTOs.AdvancedLearning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Academix.Tests.Security;

public sealed class AdvancedLearningContractsTests
{
    [Theory]
    [InlineData(typeof(AdvancedDiscussionsController))]
    [InlineData(typeof(AdvancedGroupLearningController))]
    public void AdvancedLearningEndpointsRequireAuthentication(Type controller)
    {
        Assert.NotNull(Attribute.GetCustomAttribute(controller, typeof(AuthorizeAttribute)));
        Assert.NotNull(Attribute.GetCustomAttribute(controller, typeof(ApiControllerAttribute)));
    }

    [Fact]
    public void StudentFacingDtosDoNotContainOtherStudentsGradesOrUnreleasedAnswerKeys()
    {
        var properties = typeof(GroupSubmissionView).GetProperties().Select(x => x.Name).ToArray();
        Assert.DoesNotContain("CorrectAnswer", properties);
        Assert.DoesNotContain("InstitutionId", properties);
        Assert.DoesNotContain("GradedBy", properties);
    }
}
