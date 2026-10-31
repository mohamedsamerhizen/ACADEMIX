using Academix.Api.Controllers;
using Academix.Api.DTOs.Registrar;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.RateLimiting;
namespace Academix.Tests.Security;
public sealed class Phase19CompletionSecurityTests
{
    [Fact]
    public void ApplicantCapabilitiesRequireOpaqueTokensAndAreRateLimited()
    {
        Assert.True(typeof(ApplicantOnboardingController).IsDefined(typeof(AuthorizeAttribute),true));
        var anonymous=typeof(ApplicantOnboardingController).GetMethods().Where(x=>x.DeclaringType==typeof(ApplicantOnboardingController)&&x.IsDefined(typeof(AllowAnonymousAttribute),true)).ToArray();
        Assert.Equal(new[]{"Access","Activate","Profile"},anonymous.Select(x=>x.Name).Order());
        Assert.All(anonymous,x=>Assert.True(x.IsDefined(typeof(EnableRateLimitingAttribute),true)));
    }
    [Fact]
    public void NewRegistrarActionsHaveNoAnonymousBypass()
    {
        Assert.True(typeof(RegistrarCompletionController).IsDefined(typeof(AuthorizeAttribute),true));
        Assert.DoesNotContain(typeof(RegistrarCompletionController).GetMethods(),x=>x.IsDefined(typeof(AllowAnonymousAttribute),true));
    }
    [Fact]
    public void KeyAndApplicantViewsNeverExposeProtectedSecrets()
    {
        foreach(var type in new[]{typeof(VerificationKeyView),typeof(ApplicantView),typeof(ApplicantStatusView)})
        {
            var properties=type.GetProperties().Select(x=>x.Name).ToArray();
            Assert.DoesNotContain("ProtectedSecret",properties);Assert.DoesNotContain("InvitationHash",properties);Assert.DoesNotContain("ProtectedInvitation",properties);
        }
    }
}
