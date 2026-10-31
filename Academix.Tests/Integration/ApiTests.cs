using System.Net;
using System.Net.Http.Json;
using Academix.Api.DTOs.Courses;

namespace Academix.Tests.Integration;

public sealed class ApiTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    [Fact]
    public async Task Catalog_ReturnsSeededActiveCourses()
    {
        var response = await fixture.Client.GetAsync("/api/courses");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var courses = await response.Content.ReadFromJsonAsync<CourseDto[]>();
        Assert.NotNull(courses);
        Assert.Collection(courses,
            c => Assert.Equal("CS101", c.Code),
            c => Assert.Equal("MATH101", c.Code));
    }

    [Fact]
    public async Task Readiness_UsesRealPostgreSql()
    {
        var response = await fixture.Client.GetAsync("/health/ready");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Liveness_DoesNotDependOnDatabaseProbes()
    {
        var response = await fixture.Client.GetAsync("/health/live");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }
}
