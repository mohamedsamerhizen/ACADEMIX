using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Academix.Api.Data;
using Academix.Api.Domain.Academic;
using Academix.Api.Domain.Courses;
using Academix.Api.Domain.Identity;
using Academix.Api.DTOs.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Academix.Tests.Integration;

public sealed class Phase2LearningTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private async Task<HttpResponseMessage> Send(HttpMethod method, string url, string token, object? body = null)
    {
        using var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null) request.Content = JsonContent.Create(body);
        return await fixture.Client.SendAsync(request);
    }
    private static async Task<Guid> Id(HttpResponseMessage response, HttpStatusCode expected = HttpStatusCode.Created)
    {
        Assert.Equal(expected, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return json.RootElement.GetProperty("id").GetGuid();
    }
    private async Task<(Guid Id, string Token)> CreateUser(Guid institutionId, string role)
    {
        var id = Guid.NewGuid();
        var email = $"lms-{Guid.NewGuid():N}@example.test";
        const string password = "Secure!Phase2TestPass2026";
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser { Id = id, Email = email, UserName = email, InstitutionId = institutionId };
            Assert.True((await users.CreateAsync(user, password)).Succeeded);
            Assert.True((await users.AddToRoleAsync(user, role)).Succeeded);
        }
        using var login = await fixture.Client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        return (id, (await login.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken);
    }

    [Fact]
    public async Task AnonymousRequestsCannotReadLms()
    {
        var response = await fixture.Client.GetAsync($"/api/lms/sections/{Guid.NewGuid()}/content");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task StaffAndEnrolledStudentCompleteLearningWorkflow()
    {
        var institutionId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var termId = Guid.NewGuid();
        var sectionId = Guid.NewGuid();
        var tag = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AcademixDbContext>();
            db.Institutions.Add(new Institution { Id = institutionId, Code = $"U{tag}", Name = "LMS Test University" });
            db.Courses.Add(Course.Create(courseId, institutionId, "LMS101", "Learning systems"));
            db.AcademicTerms.Add(new AcademicTerm { Id = termId, InstitutionId = institutionId, Code = $"T{tag}", Name = "LMS Term", Status = "Active", StartsOn = new DateOnly(2026, 10, 1), EndsOn = new DateOnly(2027, 7, 1) });
            db.CourseSections.Add(new CourseSection { Id = sectionId, InstitutionId = institutionId, CourseId = courseId, TermId = termId, Code = $"S{tag}", Status = "Open", Capacity = 10 });
            await db.SaveChangesAsync();
        }
        var (staffId, staff) = await CreateUser(institutionId, AcademixRoles.Instructor);
        var (studentId, student) = await CreateUser(institutionId, AcademixRoles.Student);
        var (_, outsider) = await CreateUser(institutionId, AcademixRoles.Student);
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AcademixDbContext>();
            db.TeachingAssignments.Add(new TeachingAssignment { InstitutionId = institutionId, SectionId = sectionId, UserId = staffId, Kind = AcademixRoles.Instructor });
            db.StudentEnrollments.Add(new StudentEnrollment { InstitutionId = institutionId, SectionId = sectionId, StudentId = studentId });
            await db.SaveChangesAsync();
        }
        using (var denied = await Send(HttpMethod.Get, $"/api/lms/sections/{sectionId}/content", outsider))
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);

        var unitId = await Id(await Send(HttpMethod.Post, $"/api/lms/sections/{sectionId}/units", staff, new { title = "Algorithms", position = 1 }));
        var lessonId = await Id(await Send(HttpMethod.Post, $"/api/lms/sections/{sectionId}/lessons", staff,
            new { unitId, title = "Sorting", body = "Understand stable sorting.", position = 1 }));
        using (var hidden = await Send(HttpMethod.Get, $"/api/lms/sections/{sectionId}/content", student))
        {
            Assert.Equal(HttpStatusCode.OK, hidden.StatusCode);
            var body = await hidden.Content.ReadAsStringAsync();
            Assert.DoesNotContain("Sorting", body);
        }
        using (var p = await Send(HttpMethod.Put, $"/api/lms/units/{unitId}/publish", staff, new { isPublished = true, expectedVersion = 1 }))
            Assert.Equal(HttpStatusCode.OK, p.StatusCode);
        using (var p = await Send(HttpMethod.Put, $"/api/lms/lessons/{lessonId}/publish", staff, new { isPublished = true, expectedVersion = 1 }))
            Assert.Equal(HttpStatusCode.OK, p.StatusCode);
        using (var visible = await Send(HttpMethod.Get, $"/api/lms/sections/{sectionId}/content", student))
        {
            Assert.Equal(HttpStatusCode.OK, visible.StatusCode);
            Assert.Contains("Sorting", await visible.Content.ReadAsStringAsync());
        }
        using (var completed = await Send(HttpMethod.Put, $"/api/lms/lessons/{lessonId}/complete", student))
            Assert.Equal(HttpStatusCode.OK, completed.StatusCode);
        using (var progress = await Send(HttpMethod.Get, $"/api/lms/sections/{sectionId}/my-progress", student))
        {
            Assert.Equal(HttpStatusCode.OK, progress.StatusCode);
            Assert.Contains("100", await progress.Content.ReadAsStringAsync());
        }
        var assignmentId = await Id(await Send(HttpMethod.Post, $"/api/lms/sections/{sectionId}/assignments", staff,
            new { title = "Sorting Practice", instructions = "Submit pseudocode.", dueAtUtc = DateTime.UtcNow.AddDays(3), maxScore = 10m }));
        using (var p = await Send(HttpMethod.Put, $"/api/lms/assignments/{assignmentId}/publish", staff, new { isPublished = true, expectedVersion = 1 }))
            Assert.Equal(HttpStatusCode.OK, p.StatusCode);
        var submissionId = await Id(await Send(HttpMethod.Put, $"/api/lms/assignments/{assignmentId}/my-draft", student,
            new { content = "merge sort pseudocode" }), HttpStatusCode.OK);
        using (var submitted = await Send(HttpMethod.Post, $"/api/lms/assignments/{assignmentId}/submit", student))
            Assert.Equal(HttpStatusCode.OK, submitted.StatusCode);
        using (var retry = await Send(HttpMethod.Post, $"/api/lms/assignments/{assignmentId}/submit", student))
            Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        using (var rejected = await Send(HttpMethod.Put, $"/api/lms/assignments/{assignmentId}/my-draft", student, new { content = "changed" }))
            Assert.Equal(HttpStatusCode.Conflict, rejected.StatusCode);
        using (var grade = await Send(HttpMethod.Put, $"/api/lms/submissions/{submissionId}/grade", staff,
            new { score = 9.5m, feedback = "Good" }))
            Assert.Equal(HttpStatusCode.OK, grade.StatusCode);
        using (var result = await Send(HttpMethod.Get, $"/api/lms/assignments/{assignmentId}/my-submission", student))
        {
            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
            Assert.Contains("Graded", await result.Content.ReadAsStringAsync());
        }
    }
}
