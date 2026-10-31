using Academix.Api.Controllers;
using Academix.Api.DTOs.Institutional;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Academix.Tests.Security;

public sealed class InstitutionalDocumentContractTests
{
    [Fact]
    public void DocumentControllerRequiresAuthorizationExceptPublicVerification()
    {
        var controller = typeof(InstitutionalDocumentsController);
        Assert.NotNull(Attribute.GetCustomAttribute(controller, typeof(AuthorizeAttribute)));
        Assert.NotNull(Attribute.GetCustomAttribute(controller, typeof(ApiControllerAttribute)));
        var method = controller.GetMethod(nameof(InstitutionalDocumentsController.Verify))!;
        Assert.NotNull(Attribute.GetCustomAttribute(method, typeof(AllowAnonymousAttribute)));
        Assert.Single(controller.GetMethods(), x => x.DeclaringType == controller &&
            Attribute.GetCustomAttribute(x, typeof(AllowAnonymousAttribute)) is not null);
    }

    [Fact]
    public void PublicVerificationDoesNotLeakStudentIdentityOrTranscript()
    {
        var props = typeof(AcademicDocumentVerification).GetProperties().Select(x => x.Name).ToArray();
        Assert.DoesNotContain("StudentId", props);
        Assert.DoesNotContain("SnapshotJson", props);
        Assert.DoesNotContain("Proof", props);
        Assert.DoesNotContain("StudentEmail", props);
        Assert.Contains("Status", props);
    }
}
