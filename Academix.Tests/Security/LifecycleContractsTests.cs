using Academix.Api.Controllers;
using Academix.Api.DTOs.Lifecycle;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Academix.Tests.Security;

public sealed class LifecycleContractsTests
{
    [Fact]
    public void LifecycleRoutesRequireAuthentication()
    {
        Assert.NotNull(Attribute.GetCustomAttribute(typeof(StudentLifecycleController), typeof(AuthorizeAttribute)));
        Assert.NotNull(Attribute.GetCustomAttribute(typeof(StudentLifecycleController), typeof(ApiControllerAttribute)));
    }

    [Fact]
    public void StudentCredentialHasNoExamAnswerKeysOrPrivateGradeDetails()
    {
        var properties = typeof(GraduationView).GetProperties().Select(x => x.Name).ToArray();
        Assert.DoesNotContain("CorrectAnswer", properties);
        Assert.DoesNotContain("ExamQuestion", properties);
        Assert.Contains("CredentialNumber", properties);
    }
}
