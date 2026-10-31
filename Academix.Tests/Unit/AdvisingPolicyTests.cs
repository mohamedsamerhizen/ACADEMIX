using Academix.Api.Controllers;
using Academix.Api.Services.Advising;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace Academix.Tests.Unit;

public sealed class AdvisingPolicyTests
{
    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, 25)]
    [InlineData(10000, 249975)]
    public void Pagination_IsBounded(int page, int skip)
        => Assert.Equal(skip, AdvisingPolicy.Offset(page));

    [Theory]
    [InlineData(0)]
    [InlineData(10001)]
    public void Pagination_RejectsOutOfRange(int page)
        => Assert.ThrowsAny<Exception>(() => AdvisingPolicy.Offset(page));

    [Fact]
    public void Risk_RequiresRealEvidence_NotEmptyDenominators()
    {
        Assert.Equal("InsufficientData", AdvisingPolicy.RiskIndicator(0, 0, null, 50m));
        Assert.Null(AdvisingPolicy.AttendanceRate(0, 0));
        Assert.Equal("Attention", AdvisingPolicy.RiskIndicator(4, 2, null, 50m));
        Assert.Equal("Attention", AdvisingPolicy.RiskIndicator(0, 0, 40m, 50m));
        Assert.Equal("OnTrack", AdvisingPolicy.RiskIndicator(4, 4, 65m, 50m));
        Assert.Equal(66.67m, AdvisingPolicy.AttendanceRate(3, 2));
    }

    [Fact]
    public void ResolvedCase_RequiresCompletedTasks()
    {
        Assert.ThrowsAny<Exception>(() => AdvisingPolicy.CanChangeStatus("InProgress", "Resolved", 1));
        AdvisingPolicy.CanChangeStatus("InProgress", "Resolved", 0);
        AdvisingPolicy.CanChangeStatus("Resolved", "InProgress", 0);
        Assert.ThrowsAny<Exception>(() => AdvisingPolicy.CanChangeStatus("Resolved", "Open", 0));
    }

    [Fact]
    public void TextValidation_RejectsEmptyOrOversizedStrings()
    {
        Assert.Equal("Hello", AdvisingPolicy.Required(" Hello ", 12, "title"));
        Assert.ThrowsAny<Exception>(() => AdvisingPolicy.Required("  ", 10, "title"));
        Assert.ThrowsAny<Exception>(() => AdvisingPolicy.Required("elevenchars", 5, "title"));
    }

    [Fact]
    public void AdvisingController_IsAuthenticated()
        => Assert.NotNull(Attribute.GetCustomAttribute(typeof(AdvisingController), typeof(AuthorizeAttribute)));
}
