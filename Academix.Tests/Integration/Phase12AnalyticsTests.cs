using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Academix.Api.Data;
using Academix.Api.Domain.Academic;
using Academix.Api.Domain.Courses;
using Academix.Api.Domain.Identity;
using Academix.Api.DTOs.Identity;
using Academix.Api.DTOs.Analytics;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Academix.Tests.Integration;

// Docker/PostgreSQL integration suite: not run by the local ZIP installer.
public sealed class Phase12AnalyticsTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private const string Password = "Analytics!TestPassword2026";

    private async Task<string> Token(Guid? institutionId, string role)
    {
        var email = $"analytic-{Guid.NewGuid():N}@example.test";
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser { Id = Guid.NewGuid(), InstitutionId = institutionId,
                UserName = email, Email = email };
            Assert.True((await users.CreateAsync(user, Password)).Succeeded);
            Assert.True((await users.AddToRoleAsync(user, role)).Succeeded);
        }
        using var login = await fixture.Client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, Password));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        return (await login.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken;
    }

    private async Task<HttpResponseMessage> Get(string url, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await fixture.Client.SendAsync(request);
    }

    [Fact]
    public async Task InstitutionAndStudentAnalytics_RespectCurrentRolesAndTenantBoundaries()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AcademixDbContext>();
            db.Institutions.AddRange(
                new Institution { Id = first, Code = "ANA" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(), Name = "First" },
                new Institution { Id = second, Code = "ANB" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(), Name = "Second" });
            await db.SaveChangesAsync();
        }
        var admin = await Token(first, AcademixRoles.InstitutionAdministrator);
        var student = await Token(first, AcademixRoles.Student);
        var global = await Token(null, AcademixRoles.SuperAdministrator);
        using (var own = await Get($"/api/analytics/institutions/{first}/overview", admin))
        {
            Assert.Equal(HttpStatusCode.OK, own.StatusCode);
            var overview = await own.Content.ReadFromJsonAsync<InstitutionAnalytics>();
            Assert.Equal(first, overview!.InstitutionId);
        }
        using (var other = await Get($"/api/analytics/institutions/{second}/overview", admin))
            Assert.Equal(HttpStatusCode.Forbidden, other.StatusCode);
        using (var denied = await Get($"/api/analytics/institutions/{first}/sections.csv", student))
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        using (var mine = await Get("/api/analytics/me/summary", student))
            Assert.Equal(HttpStatusCode.OK, mine.StatusCode);
        using (var globalView = await Get("/api/analytics/platform/overview", global))
            Assert.Equal(HttpStatusCode.OK, globalView.StatusCode);
        using (var globalDenied = await Get("/api/analytics/platform/overview", admin))
            Assert.Equal(HttpStatusCode.Forbidden, globalDenied.StatusCode);
    }

    [Fact]
    public async Task SectionMetrics_DenyUnassignedTeachersAndOtherStudents()
    {
        var id = Guid.NewGuid();
        var termId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var sectionId = Guid.NewGuid();
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AcademixDbContext>();
            db.Institutions.Add(new Institution { Id = id,
                Code = "ANC" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(), Name = "Third" });
            db.Courses.Add(Course.Create(courseId, id, "ANA101", "Analysis"));
            db.AcademicTerms.Add(new AcademicTerm { Id = termId, InstitutionId = id,
                Code = "T1", Name = "Term", Status = "Active", StartsOn = new DateOnly(2026,1,1),
                EndsOn = new DateOnly(2027,1,1) });
            db.CourseSections.Add(new CourseSection { Id = sectionId, InstitutionId = id,
                CourseId = courseId, TermId = termId, Code = "S1", Status = "Open", Capacity = 20 });
            await db.SaveChangesAsync();
        }
        var student = await Token(id, AcademixRoles.Student);
        var teacher = await Token(id, AcademixRoles.Instructor);
        var admin = await Token(id, AcademixRoles.InstitutionAdministrator);
        using (var denied = await Get($"/api/analytics/sections/{sectionId}/students", student))
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        using (var denied = await Get($"/api/analytics/sections/{sectionId}/students", teacher))
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        using (var allowed = await Get($"/api/analytics/sections/{sectionId}/students", admin))
            Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
    }
}
