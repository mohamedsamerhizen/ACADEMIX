using Academix.Api.Controllers;
using Academix.Api.DTOs.Records;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Academix.Tests.Security;

public sealed class RecordsContractsTests
{
    [Fact]
    public void RecordsControllerRequiresAuthenticationAndUsesContracts()
    {
        Assert.NotNull(typeof(RecordsController).GetCustomAttributes(typeof(AuthorizeAttribute), true).SingleOrDefault());
        Assert.Equal("api/records", typeof(RecordsController)
            .GetCustomAttributes(typeof(RouteAttribute), true).Cast<RouteAttribute>().Single().Template);
        Assert.All(typeof(RecordsController).GetMethods().Where(x => x.DeclaringType == typeof(RecordsController)), method =>
            Assert.DoesNotContain(method.ReturnType.ToString(), "System.Object"));
        Assert.All(typeof(TranscriptLine).GetProperties(), x => Assert.NotEqual("AnswerKey", x.Name));
        Assert.All(typeof(EnrollmentEligibility).GetProperties(), x => Assert.NotEqual("Password", x.Name));
    }
}
