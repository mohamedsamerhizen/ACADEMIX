using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Academix.Api.Data;
using Academix.Api.Domain.Academic;
using Academix.Api.Domain.Courses;
using Academix.Api.Domain.Identity;
using Academix.Api.DTOs.Identity;
using Academix.Api.DTOs.Workspaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Academix.Tests.Integration;

// Requires Testcontainers/PostgreSQL; deliberately excluded from Docker-independent test runs.
public sealed class Phase6WorkspacesTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private const string Password = "Excellent!RoleWorkspaces2026";

    private async Task<(Guid UserId, string AccessToken)> CreateUser(Guid? institutionId, string role)
    {
        var id = Guid.NewGuid();
        var email = $"ws-{Guid.NewGuid():N}@example.test";
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var account = new ApplicationUser { Id = id, Email = email, UserName = email, InstitutionId = institutionId };
            Assert.True((await manager.CreateAsync(account, Password)).Succeeded);
            Assert.True((await manager.AddToRoleAsync(account, role)).Succeeded);
        }
        using var login = await fixture.Client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, Password));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        return (id, (await login.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken);
    }

    private async Task<HttpResponseMessage> Call(HttpMethod method, string url, string token, object? body = null)
    {
        using var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null) request.Content = JsonContent.Create(body);
        return await fixture.Client.SendAsync(request);
    }

    [Fact]
    public async Task StudentIsRestrictedToOwnEnrolledSectionAndCannotReadAdministrativeData()
    {
        var instA = Guid.NewGuid();
        var instB = Guid.NewGuid();
        var courseA = Guid.NewGuid();
        var courseB = Guid.NewGuid();
        var termA = Guid.NewGuid();
        var termB = Guid.NewGuid();
        var sectionA = Guid.NewGuid();
        var sectionB = Guid.NewGuid();
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AcademixDbContext>();
            db.Institutions.AddRange(
                new Institution { Id = instA, Code = "W" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(), Name = "A" },
                new Institution { Id = instB, Code = "W" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(), Name = "B" });
            db.Courses.AddRange(Course.Create(courseA, instA, "WSA101", "Student Workspace"),
                Course.Create(courseB, instB, "WSB101", "External Course"));
            db.AcademicTerms.AddRange(
                new AcademicTerm { Id = termA, InstitutionId = instA, Code = "T1", Name = "First", Status = "Active", StartsOn = new DateOnly(2026, 1, 1), EndsOn = new DateOnly(2027, 1, 1) },
                new AcademicTerm { Id = termB, InstitutionId = instB, Code = "T1", Name = "First", Status = "Active", StartsOn = new DateOnly(2026, 1, 1), EndsOn = new DateOnly(2027, 1, 1) });
            db.CourseSections.AddRange(
                new CourseSection { Id = sectionA, InstitutionId = instA, CourseId = courseA, TermId = termA, Code = "A", Status = "Open", Capacity = 10 },
                new CourseSection { Id = sectionB, InstitutionId = instB, CourseId = courseB, TermId = termB, Code = "A", Status = "Open", Capacity = 10 });
            await db.SaveChangesAsync();
        }
        var student = await CreateUser(instA, AcademixRoles.Student);
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AcademixDbContext>();
            db.StudentEnrollments.Add(new StudentEnrollment { SectionId = sectionA, StudentId = student.UserId,
                InstitutionId = instA, Status = "Active" });
            await db.SaveChangesAsync();
        }
        using (var courses = await Call(HttpMethod.Get, "/api/workspace/student/courses", student.AccessToken))
        {
            Assert.Equal(HttpStatusCode.OK, courses.StatusCode);
            var content = (await courses.Content.ReadFromJsonAsync<PageResult<StudentCourse>>())!;
            Assert.Single(content.Items);
            Assert.Equal(sectionA, content.Items[0].SectionId);
        }
        using (var myProgress = await Call(HttpMethod.Get, $"/api/workspace/student/courses/{sectionA}/progress", student.AccessToken))
            Assert.Equal(HttpStatusCode.OK, myProgress.StatusCode);
        using (var otherProgress = await Call(HttpMethod.Get, $"/api/workspace/student/courses/{sectionB}/progress", student.AccessToken))
            Assert.Equal(HttpStatusCode.NotFound, otherProgress.StatusCode);
        using (var denied = await Call(HttpMethod.Get, $"/api/workspace/institutions/{instA}/users", student.AccessToken))
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        using (var globalDenied = await Call(HttpMethod.Get, "/api/platform/overview", student.AccessToken))
            Assert.Equal(HttpStatusCode.Forbidden, globalDenied.StatusCode);
    }

    [Fact]
    public async Task GlobalCanProvisionInstitutionAndAdminOnlySeesOwnInstitution()
    {
        var super = await CreateUser(null, AcademixRoles.SuperAdministrator);
        var code = "W" + Guid.NewGuid().ToString("N")[..10].ToUpperInvariant();
        Guid newInstitutionId;
        using (var response = await Call(HttpMethod.Post, "/api/platform/institutions", super.AccessToken,
            new InstitutionCreateRequest(code, "Satellite Campus")))
        {
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var institution = (await response.Content.ReadFromJsonAsync<InstitutionDirectoryItem>())!;
            newInstitutionId = institution.Id;
            Assert.Equal(code, institution.Code);
        }
        var outsider = await CreateUser(AcademicIds.InitialInstitution, AcademixRoles.InstitutionAdministrator);
        using (var denied = await Call(HttpMethod.Get, $"/api/workspace/institutions/{newInstitutionId}/overview", outsider.AccessToken))
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        using (var own = await Call(HttpMethod.Get, $"/api/workspace/institutions/{AcademicIds.InitialInstitution}/overview", outsider.AccessToken))
            Assert.Equal(HttpStatusCode.OK, own.StatusCode);
        using (var duplicate = await Call(HttpMethod.Post, "/api/platform/institutions", super.AccessToken,
            new InstitutionCreateRequest(code, "Duplicate")))
            Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        using (var deniedProvisioning = await Call(HttpMethod.Post, "/api/platform/institutions", outsider.AccessToken,
            new InstitutionCreateRequest("OTHER-TEST", "Should not work")))
            Assert.Equal(HttpStatusCode.Forbidden, deniedProvisioning.StatusCode);
    }

    [Fact]
    public async Task RevokingInstructorRoleRemovesWorkspaceAccessEvenWithOldJwt()
    {
        var teacher = await CreateUser(AcademicIds.InitialInstitution, AcademixRoles.Instructor);
        using (var empty = await Call(HttpMethod.Get, "/api/workspace/instructor/sections", teacher.AccessToken))
            Assert.Equal(HttpStatusCode.OK, empty.StatusCode);
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var account = (await manager.FindByIdAsync(teacher.UserId.ToString()))!;
            Assert.True((await manager.RemoveFromRoleAsync(account, AcademixRoles.Instructor)).Succeeded);
        }
        using var denied = await Call(HttpMethod.Get, "/api/workspace/instructor/sections", teacher.AccessToken);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }
}
