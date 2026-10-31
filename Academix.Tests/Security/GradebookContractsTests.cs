using Academix.Api.Controllers;
using Academix.Api.DTOs.Assessment;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Academix.Tests.Security;

public sealed class GradebookContractsTests
{
    [Fact]
    public void GradebookControllerRequiresAuthentication()
    {
        Assert.NotNull(Attribute.GetCustomAttribute(typeof(GradebookController), typeof(AuthorizeAttribute)));
        Assert.NotNull(Attribute.GetCustomAttribute(typeof(GradebookController), typeof(ApiControllerAttribute)));
    }

    [Fact]
    public void PublishedResultDtoDoesNotContainAuditOrPrivateGrades()
    {
        var fields = typeof(GradebookResultView).GetProperties().Select(x => x.Name).ToArray();
        Assert.DoesNotContain("ActorId", fields);
        Assert.DoesNotContain("Reason", fields);
        Assert.DoesNotContain("Answers", fields);
        Assert.DoesNotContain("AnswerKeyJson", fields);
    }
}
