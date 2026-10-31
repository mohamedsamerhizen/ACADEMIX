using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Academix.Api.Data;
using Academix.Api.Domain.Academic;
using Academix.Api.Domain.Courses;
using Academix.Api.Domain.Identity;
using Academix.Api.DTOs.Advising;
using Academix.Api.DTOs.Identity;
using Academix.Api.DTOs.Workspaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Academix.Tests.Integration;

// Requires a real Testcontainers PostgreSQL instance. Intentionally excluded from local ZIP installer.
public sealed class Phase13AdvisingTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private const string Password = "Advising!TestPassword2026";

    private async Task<(Guid Id, string Token)> User(Guid institutionId, string role)
    {
        var id = Guid.NewGuid();
        var email = $"adv-{id:N}@example.test";
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var u = new ApplicationUser { Id = id, InstitutionId = institutionId,
                UserName = email, Email = email };
            Assert.True((await users.CreateAsync(u, Password)).Succeeded);
            Assert.True((await users.AddToRoleAsync(u, role)).Succeeded);
        }
        using var login = await fixture.Client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, Password));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var token = (await login.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken;
        return (id, token);
    }

    private async Task<HttpResponseMessage> Call(HttpMethod method, string path, string token, object? data = null)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (data is not null) request.Content = JsonContent.Create(data);
        return await fixture.Client.SendAsync(request);
    }

    private async Task<(Guid Institution, Guid Section)> Section()
    {
        var institution = Guid.NewGuid();
        var term = Guid.NewGuid();
        var course = Guid.NewGuid();
        var section = Guid.NewGuid();
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AcademixDbContext>();
        db.Institutions.Add(new Institution { Id = institution,
            Code = "ADV" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(), Name = "Advising" });
        db.Courses.Add(Course.Create(course, institution, "ADV101", "Advising Study"));
        db.AcademicTerms.Add(new AcademicTerm { Id = term, InstitutionId = institution, Code = "AD1", Name = "Term",
            StartsOn = new DateOnly(2026,1,1), EndsOn = new DateOnly(2027,1,1), Status = "Active" });
        db.CourseSections.Add(new CourseSection { Id = section, InstitutionId = institution,
            CourseId = course, TermId = term, Code = "S1", Status = "Open", Capacity = 30 });
        await db.SaveChangesAsync();
        return (institution, section);
    }

    [Fact]
    public async Task Cases_ProtectInternalNotes_AndTrackStudentActions()
    {
        var (institution, section) = await Section();
        var (studentId, studentToken) = await User(institution, AcademixRoles.Student);
        var (_, adminToken) = await User(institution, AcademixRoles.InstitutionAdministrator);
        var (_, teacherToken) = await User(institution, AcademixRoles.Instructor);
        var (otherInstitution, _) = await Section();
        var (_, otherAdminToken) = await User(otherInstitution, AcademixRoles.InstitutionAdministrator);
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AcademixDbContext>();
            db.StudentEnrollments.Add(new StudentEnrollment { InstitutionId = institution,
                SectionId = section, StudentId = studentId });
            await db.SaveChangesAsync();
        }
        using (var denied = await Call(HttpMethod.Get, $"/api/advising/sections/{section}/risks", studentToken))
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        using (var denied = await Call(HttpMethod.Get, $"/api/advising/sections/{section}/risks", teacherToken))
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        using (var denied = await Call(HttpMethod.Get, $"/api/advising/sections/{section}/risks", otherAdminToken))
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        using (var risks = await Call(HttpMethod.Get, $"/api/advising/sections/{section}/risks", adminToken))
        {
            Assert.Equal(HttpStatusCode.OK, risks.StatusCode);
            var data = await risks.Content.ReadFromJsonAsync<PageResult<StudentRisk>>();
            Assert.Contains(data!.Items, x => x.StudentId == studentId && x.Indicator == "InsufficientData");
        }
        Guid caseId;
        var requestId = Guid.NewGuid();
        var openRequest = new CreateAdvisingCase(requestId, studentId, "Study support", "Staff-only progress notes");
        using (var opened = await Call(HttpMethod.Post, $"/api/advising/sections/{section}/cases", adminToken, openRequest))
        {
            Assert.Equal(HttpStatusCode.OK, opened.StatusCode);
            caseId = (await opened.Content.ReadFromJsonAsync<AdvisingCaseView>())!.Id;
        }
        using (var repeat = await Call(HttpMethod.Post, $"/api/advising/sections/{section}/cases", adminToken, openRequest))
        {
            Assert.Equal(HttpStatusCode.OK, repeat.StatusCode);
            Assert.Equal(caseId, (await repeat.Content.ReadFromJsonAsync<AdvisingCaseView>())!.Id);
        }
        using (var note = await Call(HttpMethod.Post, $"/api/advising/cases/{caseId}/notes", adminToken,
            new CreateAdvisingNote("Confidential advising conversation")))
            Assert.Equal(HttpStatusCode.OK, note.StatusCode);
        AdvisingTaskView task;
        using (var created = await Call(HttpMethod.Post, $"/api/advising/cases/{caseId}/tasks", adminToken,
            new CreateAdvisingTask("Attend the study session", null)))
        {
            Assert.Equal(HttpStatusCode.OK, created.StatusCode);
            task = (await created.Content.ReadFromJsonAsync<AdvisingTaskView>())!;
        }
        using (var blocked = await Call(HttpMethod.Put, $"/api/advising/cases/{caseId}/status", adminToken,
            new ChangeAdvisingStatus(1, "Resolved", "Incomplete task")))
            Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        using (var mine = await Call(HttpMethod.Get, "/api/advising/me/cases", studentToken))
        {
            Assert.Equal(HttpStatusCode.OK, mine.StatusCode);
            var body = await mine.Content.ReadAsStringAsync();
            Assert.DoesNotContain("Confidential advising conversation", body);
            Assert.DoesNotContain("Staff-only progress notes", body);
            var own = await mine.Content.ReadFromJsonAsync<PageResult<StudentCaseView>>();
            Assert.Contains(own!.Items, x => x.Id == caseId && x.Tasks.Any(t => t.Id == task.Id));
        }
        using (var denied = await Call(HttpMethod.Get, $"/api/advising/cases/{caseId}", studentToken))
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        using (var reported = await Call(HttpMethod.Post, $"/api/advising/tasks/{task.Id}/report-done", studentToken,
            new TaskTransition(task.Version)))
        {
            Assert.Equal(HttpStatusCode.OK, reported.StatusCode);
            task = (await reported.Content.ReadFromJsonAsync<AdvisingTaskView>())!;
            Assert.Equal("ReportedDone", task.Status);
        }
        using (var approved = await Call(HttpMethod.Post, $"/api/advising/tasks/{task.Id}/review", adminToken,
            new ReviewAdvisingTask(task.Version, true, "Confirmed participation")))
        {
            Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
            Assert.Equal("Completed", (await approved.Content.ReadFromJsonAsync<AdvisingTaskView>())!.Status);
        }
        using (var closed = await Call(HttpMethod.Put, $"/api/advising/cases/{caseId}/status", adminToken,
            new ChangeAdvisingStatus(1, "Resolved", "Follow-up finished")))
        {
            Assert.Equal(HttpStatusCode.OK, closed.StatusCode);
            Assert.Equal("Resolved", (await closed.Content.ReadFromJsonAsync<AdvisingCaseView>())!.Status);
        }
    }
}
