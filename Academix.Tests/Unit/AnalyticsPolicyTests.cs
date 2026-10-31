using Academix.Api.Controllers;
using Academix.Api.Services.Analytics;
using Microsoft.AspNetCore.Authorization;
using Xunit;

namespace Academix.Tests.Unit;

public sealed class AnalyticsPolicyTests
{
    [Theory]
    [InlineData(1, 0)]
    [InlineData(2, 20)]
    [InlineData(10000, 199980)]
    public void Offset_UsesBoundedPaging(int page, int expected)
        => Assert.Equal(expected, AnalyticsPolicy.Offset(page));

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(10001)]
    public void Offset_RejectsInvalidPages(int page)
        => Assert.ThrowsAny<Exception>(() => AnalyticsPolicy.Offset(page));

    [Fact]
    public void Rate_DoesNotInventDenominatorForEmptyMetrics()
    {
        Assert.Null(AnalyticsPolicy.Rate(0, 0));
        Assert.Equal(75m, AnalyticsPolicy.Rate(3, 4));
        Assert.Equal(33.33m, AnalyticsPolicy.Rate(1, 3));
    }

    [Theory]
    [InlineData("=SUM(A1:A3)")]
    [InlineData("+cmd")]
    [InlineData("-999")]
    [InlineData("@formula")]
    [InlineData("\ttab")]
    [InlineData("  =SUM(A1)")]
    public void CsvCell_DisarmsSpreadsheetFormulas(string malicious)
        => Assert.StartsWith("\"'", AnalyticsPolicy.CsvCell(malicious));

    [Fact]
    public void CsvCell_EscapesEmbeddedQuotesAndNewlines()
        => Assert.Equal("\"Course \"\"A\"\"\nTitle\"", AnalyticsPolicy.CsvCell("Course \"A\"\nTitle"));

    [Fact]
    public void AnalyticsController_RequiresAuthentication()
        => Assert.NotNull(Attribute.GetCustomAttribute(typeof(AnalyticsController), typeof(AuthorizeAttribute)));
}
