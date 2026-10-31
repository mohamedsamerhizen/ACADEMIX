using Academix.Api.Controllers;
using Academix.Api.DTOs.Communications;
using Microsoft.AspNetCore.Authorization;

namespace Academix.Tests.Security;

public sealed class CommunicationContractSecurityTests
{
    [Fact]
    public void EveryCommunicationRouteRequiresAuthentication()
    {
        Assert.NotEmpty(typeof(CommunicationsController)
            .GetCustomAttributes(typeof(AuthorizeAttribute), true));
        Assert.DoesNotContain(typeof(CommunicationsController).GetMethods()
            .Where(x => x.DeclaringType == typeof(CommunicationsController)),
            x => x.IsDefined(typeof(AllowAnonymousAttribute), true));
    }

    [Fact]
    public void StudentNotificationPayloadDoesNotContainAuthOrGradeData()
    {
        var visible = typeof(InboxNotificationView).GetProperties().Select(x => x.Name).ToArray();
        Assert.DoesNotContain("AccessToken", visible);
        Assert.DoesNotContain("CorrectAnswer", visible);
        Assert.DoesNotContain("StudentEmail", visible);
    }

    [Fact]
    public void MessagesUseClientGeneratedIdempotencyKey()
    {
        Assert.NotNull(typeof(SendSectionMessage).GetProperty("ClientMessageId"));
        Assert.NotNull(typeof(SectionMessageView).GetProperty("SenderId"));
    }
}
