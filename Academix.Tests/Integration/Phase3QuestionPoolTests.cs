using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Academix.Api.Data;
using Academix.Api.Domain.Academic;
using Academix.Api.Domain.Courses;
using Academix.Api.Domain.Identity;
using Academix.Api.DTOs.Examinations;
using Academix.Api.DTOs.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Academix.Tests.Integration;

// Requires PostgreSQL/Testcontainers; not included in local unit/security filter.
public sealed class Phase3QuestionPoolTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private async Task<HttpResponseMessage> Send(HttpMethod method, string path, string token, object? body = null)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null) request.Content = JsonContent.Create(body);
        return await fixture.Client.SendAsync(request);
    }

    private async Task<(Guid Id, string Token)> CreateUser(Guid institutionId, string role)
    {
        var id = Guid.NewGuid();
        var email = $"pool-{Guid.NewGuid():N}@example.test";
        const string password = "Secure!QuestionPools2026";
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser { Id = id, InstitutionId = institutionId, Email = email, UserName = email };
            Assert.True((await manager.CreateAsync(user, password)).Succeeded);
            Assert.True((await manager.AddToRoleAsync(user, role)).Succeeded);
        }
        using var login = await fixture.Client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        return (id, (await login.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken);
    }

    [Fact]
    public async Task PoolOnlyExamPublishesDistinctFrozenQuestionsAndCannotBeEdited()
    {
        var institutionId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var sectionId = Guid.NewGuid();
        var termId = Guid.NewGuid();
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AcademixDbContext>();
            db.Institutions.Add(new Institution { Id = institutionId,
                Code = "P" + Guid.NewGuid().ToString("N")[..7], Name = "Pool Institution" });
            db.Courses.Add(Course.Create(courseId, institutionId, "POOL101", "Pool course"));
            db.AcademicTerms.Add(new AcademicTerm { Id = termId, InstitutionId = institutionId,
                Code = "POOL-T", Name = "Term", Status = "Active",
                StartsOn = new DateOnly(2026, 10, 1), EndsOn = new DateOnly(2027, 7, 1) });
            db.CourseSections.Add(new CourseSection { Id = sectionId, InstitutionId = institutionId,
                CourseId = courseId, TermId = termId, Code = "A", Capacity = 20, Status = "Open" });
            await db.SaveChangesAsync();
        }
        var (instructorId, instructor) = await CreateUser(institutionId, AcademixRoles.Instructor);
        var (_, student) = await CreateUser(institutionId, AcademixRoles.Student);
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AcademixDbContext>();
            db.TeachingAssignments.Add(new TeachingAssignment { InstitutionId = institutionId,
                SectionId = sectionId, UserId = instructorId, Kind = AcademixRoles.Instructor });
            await db.SaveChangesAsync();
        }
        var createdQuestions = new List<BankQuestionDetail>();
        foreach (var difficulty in new[] { "Easy", "Medium", "Hard" })
        {
            var question = new QuestionDraftRequest("SingleChoice", $"Pool question {difficulty}", difficulty,
                2m, [new("A", "Right"), new("B", "Wrong")], ["A"], [], "SECRET SOLUTION", []);
            using var response = await Send(HttpMethod.Post,
                $"/api/exams/courses/{courseId}/questions", instructor, question);
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            createdQuestions.Add((await response.Content.ReadFromJsonAsync<BankQuestionDetail>())!);
        }
        var now = DateTime.UtcNow;
        using var createExam = await Send(HttpMethod.Post, $"/api/exams/sections/{sectionId}/exams", instructor,
            new ExamDraftRequest("Pool exam", "Pooled questions", 30, 1,
                now.AddMinutes(5), now.AddDays(2), false));
        Assert.Equal(HttpStatusCode.Created, createExam.StatusCode);
        var detail = (await createExam.Content.ReadFromJsonAsync<ExamDetail>())!;
        var id = detail.Exam.Id;
        var pools = new ExamReplacePoolsRequest(detail.Exam.Version,
            [new("SingleChoice", null, 1, 2m), new("SingleChoice", "Hard", 1, 3m)]);
        using (var denied = await Send(HttpMethod.Put, $"/api/exams/{id}/pools", student, pools))
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        using var replace = await Send(HttpMethod.Put, $"/api/exams/{id}/pools", instructor, pools);
        Assert.Equal(HttpStatusCode.OK, replace.StatusCode);
        detail = (await replace.Content.ReadFromJsonAsync<ExamDetail>())!;
        Assert.Equal(2, detail.PlannedQuestionCount);
        Assert.Equal(5m, detail.TotalPoints);
        using (var stale = await Send(HttpMethod.Put, $"/api/exams/{id}/pools", instructor, pools))
            Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using var check = await Send(HttpMethod.Get, $"/api/exams/{id}/assembly-check", instructor);
        Assert.Equal(HttpStatusCode.OK, check.StatusCode);
        Assert.True((await check.Content.ReadFromJsonAsync<ExamAssemblyCheck>())!.CanPublish);
        using var published = await Send(HttpMethod.Post, $"/api/exams/{id}/publish", instructor,
            new ExamPublishRequest(detail.Exam.Version));
        Assert.Equal(HttpStatusCode.OK, published.StatusCode);
        var preview = (await published.Content.ReadFromJsonAsync<ExamPublishedPreview>())!;
        Assert.Equal(2, preview.Questions.Count);
        Assert.Equal(2, preview.Questions.Select(q => q.QuestionId).Distinct().Count());
        Assert.Equal(5m, preview.TotalPoints);
        Assert.Contains(preview.Questions, q => q.Stem == "Pool question Hard");
        Assert.DoesNotContain("SECRET SOLUTION", await published.Content.ReadAsStringAsync());
        using (var after = await Send(HttpMethod.Put, $"/api/exams/{id}/pools", instructor,
            new ExamReplacePoolsRequest(preview.Exam.Version, [])))
            Assert.Equal(HttpStatusCode.Conflict, after.StatusCode);
        var selected = preview.Questions[0];
        var old = createdQuestions.Single(x => x.Question.Id == selected.QuestionId);
        var altered = new QuestionDraftRequest("SingleChoice", "Edited pool question AFTER publish", "Hard",
            2m, [new("A", "Right"), new("B", "Wrong")], ["A"], [], "SECRET SOLUTION", []);
        using (var edited = await Send(HttpMethod.Put, $"/api/exams/questions/{old.Question.Id}", instructor,
            new QuestionEditRequest(old.Question.Version, altered)))
            Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        using var frozen = await Send(HttpMethod.Get, $"/api/exams/{id}/published-preview", instructor);
        Assert.Equal(HttpStatusCode.OK, frozen.StatusCode);
        var again = (await frozen.Content.ReadFromJsonAsync<ExamPublishedPreview>())!;
        Assert.Equal(selected.Stem, again.Questions[0].Stem);
    }
}
