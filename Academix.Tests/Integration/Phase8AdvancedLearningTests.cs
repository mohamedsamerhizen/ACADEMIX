using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Academix.Api.Data;
using Academix.Api.Domain.Academic;
using Academix.Api.Domain.Courses;
using Academix.Api.Domain.Identity;
using Academix.Api.DTOs.AdvancedLearning;
using Academix.Api.DTOs.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Academix.Tests.Integration;

// PostgreSQL/Testcontainers required. This test class is excluded from local Docker-independent runs.
public sealed class Phase8AdvancedLearningTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private const string Password = "Valid!TestPassword2026";

    private async Task<(Guid Id, string Token)> User(Guid institution, string role)
    {
        var id = Guid.NewGuid();
        var email = $"advanced-{Guid.NewGuid():N}@example.test";
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser { Id = id, Email = email, UserName = email, InstitutionId = institution };
            Assert.True((await manager.CreateAsync(user, Password)).Succeeded);
            Assert.True((await manager.AddToRoleAsync(user, role)).Succeeded);
        }
        using var login = await fixture.Client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, Password));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        return (id, (await login.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken);
    }

    private async Task<(Guid Institution, Guid Section)> Seed()
    {
        var inst = Guid.NewGuid(); var section = Guid.NewGuid(); var course = Guid.NewGuid(); var term = Guid.NewGuid();
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AcademixDbContext>();
        db.Institutions.Add(new Institution { Id = inst, Code = "P8" + Guid.NewGuid().ToString("N")[..8], Name = "LMS Advanced" });
        db.Courses.Add(Course.Create(course, inst, "ADV101", "Advanced course"));
        db.AcademicTerms.Add(new AcademicTerm { Id = term, InstitutionId = inst, Code = "TERM", Name = "Term", StartsOn = new DateOnly(2026,1,1), EndsOn = new DateOnly(2027,1,1) });
        db.CourseSections.Add(new CourseSection { Id = section, InstitutionId = inst, CourseId = course, TermId = term,
            Code = "S1", Status = "Open", Capacity = 50 });
        await db.SaveChangesAsync();
        return (inst, section);
    }

    private async Task<HttpResponseMessage> Send(HttpMethod method, string path, string token, object? body = null)
    {
        using var req = new HttpRequestMessage(method, path);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null) req.Content = JsonContent.Create(body);
        return await fixture.Client.SendAsync(req);
    }

    [Fact]
    public async Task DiscussionPermissionsLockingAndHiddenReplies()
    {
        var (inst, section) = await Seed();
        var admin = await User(inst, AcademixRoles.InstitutionAdministrator);
        var student = await User(inst, AcademixRoles.Student);
        var outsider = await User(inst, AcademixRoles.Student);
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AcademixDbContext>();
            db.StudentEnrollments.Add(new StudentEnrollment { InstitutionId = inst, SectionId = section,
                StudentId = student.Id, Status = "Active" });
            await db.SaveChangesAsync();
        }
        var baseUrl = $"/api/advanced-lms/sections/{section}/discussions";
        using var denied = await Send(HttpMethod.Get, baseUrl, outsider.Token);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        using var topicResponse = await Send(HttpMethod.Post, baseUrl, student.Token,
            new DiscussionTopicCreate("A question", "How does it work?"));
        Assert.Equal(HttpStatusCode.OK, topicResponse.StatusCode);
        var topic = (await topicResponse.Content.ReadFromJsonAsync<DiscussionTopicView>())!;
        using var replyResponse = await Send(HttpMethod.Post, $"/api/advanced-lms/discussions/{topic.Id}/replies",
            student.Token, new DiscussionReplyCreate("My follow-up", null));
        Assert.Equal(HttpStatusCode.OK, replyResponse.StatusCode);
        var reply = (await replyResponse.Content.ReadFromJsonAsync<DiscussionPostView>())!;
        using var hide = await Send(HttpMethod.Put, $"/api/advanced-lms/discussion-posts/{reply.Id}/visibility", admin.Token, true);
        Assert.Equal(HttpStatusCode.OK, hide.StatusCode);
        using var read = await Send(HttpMethod.Get, $"/api/advanced-lms/discussions/{topic.Id}", student.Token);
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        Assert.Empty((await read.Content.ReadFromJsonAsync<DiscussionPage>())!.Posts);
        using var locked = await Send(HttpMethod.Put, $"/api/advanced-lms/discussions/{topic.Id}/state",
            admin.Token, new DiscussionStateUpdate(true, false, 1));
        Assert.Equal(HttpStatusCode.OK, locked.StatusCode);
        using var blocked = await Send(HttpMethod.Post, $"/api/advanced-lms/discussions/{topic.Id}/replies",
            student.Token, new DiscussionReplyCreate("Too late", null));
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
    }

    [Fact]
    public async Task TeamProjectAndRubricGradingLifecycle()
    {
        var (inst, section) = await Seed();
        var admin = await User(inst, AcademixRoles.InstitutionAdministrator);
        var student = await User(inst, AcademixRoles.Student);
        var outsider = await User(inst, AcademixRoles.Student);
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AcademixDbContext>();
            db.StudentEnrollments.AddRange(
                new StudentEnrollment { InstitutionId = inst, SectionId = section, StudentId = student.Id, Status = "Active" },
                new StudentEnrollment { InstitutionId = inst, SectionId = section, StudentId = outsider.Id, Status = "Active" });
            await db.SaveChangesAsync();
        }
        var prefix = "/api/advanced-lms";
        using var teamResponse = await Send(HttpMethod.Post, $"{prefix}/sections/{section}/teams", admin.Token,
            new CreateTeam("Group A"));
        Assert.Equal(HttpStatusCode.OK, teamResponse.StatusCode);
        var team = (await teamResponse.Content.ReadFromJsonAsync<TeamView>())!;
        using var added = await Send(HttpMethod.Post, $"{prefix}/teams/{team.Id}/members/{student.Id}", admin.Token);
        Assert.Equal(HttpStatusCode.OK, added.StatusCode);

        using var rubricResponse = await Send(HttpMethod.Post, $"{prefix}/sections/{section}/rubrics", admin.Token,
            new CreateRubric("Research rubric", [
                new RubricCriterionInput(1, "Evidence", "Sources", 60),
                new RubricCriterionInput(2, "Clarity", "Writing", 40)
            ]));
        Assert.Equal(HttpStatusCode.OK, rubricResponse.StatusCode);
        var rubric = (await rubricResponse.Content.ReadFromJsonAsync<RubricView>())!;

        using var projectResponse = await Send(HttpMethod.Post, $"{prefix}/sections/{section}/group-projects", admin.Token,
            new CreateGroupProject("Final project", "Write a report", DateTime.UtcNow.AddDays(7), 100, rubric.Id));
        Assert.Equal(HttpStatusCode.OK, projectResponse.StatusCode);
        var project = (await projectResponse.Content.ReadFromJsonAsync<GroupProjectView>())!;
        using var published = await Send(HttpMethod.Put, $"{prefix}/group-projects/{project.Id}/publish", admin.Token,
            new PublishGroupProject(true, 1));
        Assert.Equal(HttpStatusCode.OK, published.StatusCode);
        using var denied = await Send(HttpMethod.Put, $"{prefix}/group-projects/{project.Id}/teams/{team.Id}/draft",
            outsider.Token, new UpdateGroupDraft("Not in team", null));
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        using var draft = await Send(HttpMethod.Put, $"{prefix}/group-projects/{project.Id}/teams/{team.Id}/draft",
            student.Token, new UpdateGroupDraft("Research report", null));
        Assert.Equal(HttpStatusCode.OK, draft.StatusCode);
        var submission = (await draft.Content.ReadFromJsonAsync<GroupSubmissionView>())!;
        using var submitted = await Send(HttpMethod.Post, $"{prefix}/group-projects/{project.Id}/teams/{team.Id}/submit", student.Token);
        Assert.Equal(HttpStatusCode.OK, submitted.StatusCode);
        using var retry = await Send(HttpMethod.Post, $"{prefix}/group-projects/{project.Id}/teams/{team.Id}/submit", student.Token);
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        using var membershipFrozen = await Send(HttpMethod.Delete, $"{prefix}/teams/{team.Id}/members/{student.Id}", admin.Token);
        Assert.Equal(HttpStatusCode.Conflict, membershipFrozen.StatusCode);
        using var grade = await Send(HttpMethod.Put, $"{prefix}/group-submissions/{submission.Id}/grade", admin.Token,
            new GradeGroupSubmission(null, "Good job", "First assessment", 2, [
                new CriterionScoreInput(rubric.Criteria[0].Id, 55, "Good sources"),
                new CriterionScoreInput(rubric.Criteria[1].Id, 35, "Well written")
            ]));
        Assert.Equal(HttpStatusCode.OK, grade.StatusCode);
        Assert.Equal(90, (await grade.Content.ReadFromJsonAsync<GroupSubmissionView>())!.Score);
        using var audit = await Send(HttpMethod.Get, $"{prefix}/group-submissions/{submission.Id}/grade-history", admin.Token);
        Assert.Equal(HttpStatusCode.OK, audit.StatusCode);
        Assert.Single((await audit.Content.ReadFromJsonAsync<GradeAuditView[]>())!);
    }
}
