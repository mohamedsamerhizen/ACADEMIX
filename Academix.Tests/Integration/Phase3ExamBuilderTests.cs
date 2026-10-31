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

// PostgreSQL/Testcontainers tests: not part of Docker-independent validation.
public sealed class Phase3ExamBuilderTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private async Task<(Guid Id, string Token)> User(Guid institutionId, string role)
    {
        var id = Guid.NewGuid();
        var email = $"exambuilder-{Guid.NewGuid():N}@example.test";
        const string password = "Secure!ExamBuilder2026";
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser { Id = id, Email = email, UserName = email, InstitutionId = institutionId };
            Assert.True((await users.CreateAsync(user, password)).Succeeded);
            Assert.True((await users.AddToRoleAsync(user, role)).Succeeded);
        }
        using var response = await fixture.Client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (id, (await response.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken);
    }

    private Task<HttpResponseMessage> Send(HttpMethod method, string route, string jwt, object? payload = null)
    {
        var req = new HttpRequestMessage(method, route);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", jwt);
        if (payload is not null) req.Content = JsonContent.Create(payload);
        return SendOwned(req);
    }

    private async Task<HttpResponseMessage> SendOwned(HttpRequestMessage req)
    {
        using (req) return await fixture.Client.SendAsync(req);
    }

    [Fact]
    public async Task PublishedSnapshotIsImmutableAndDoesNotLeakAnswerKeys()
    {
        var institutionId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var sectionId = Guid.NewGuid();
        var termId = Guid.NewGuid();
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AcademixDbContext>();
            db.Institutions.Add(new Institution { Id = institutionId,
                Code = "E" + Guid.NewGuid().ToString("N")[..7], Name = "Exam Institution" });
            db.Courses.Add(Course.Create(courseId, institutionId, "EXAM202", "Exam design"));
            db.AcademicTerms.Add(new AcademicTerm { Id = termId, InstitutionId = institutionId,
                Code = "T2026", Name = "Term", Status = "Active",
                StartsOn = new DateOnly(2026, 10, 1), EndsOn = new DateOnly(2027, 7, 1) });
            db.CourseSections.Add(new CourseSection { Id = sectionId, InstitutionId = institutionId,
                CourseId = courseId, TermId = termId, Code = "A", Capacity = 20, Status = "Open" });
            await db.SaveChangesAsync();
        }
        var (instructorId, instructor) = await User(institutionId, AcademixRoles.Instructor);
        var (assistantId, assistant) = await User(institutionId, AcademixRoles.TeachingAssistant);
        var (_, student) = await User(institutionId, AcademixRoles.Student);
        var outsiderId = Guid.NewGuid();
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AcademixDbContext>();
            db.Institutions.Add(new Institution { Id = outsiderId,
                Code = "E" + Guid.NewGuid().ToString("N")[..7], Name = "Other Institution" });
            db.TeachingAssignments.Add(new TeachingAssignment { SectionId = sectionId,
                InstitutionId = institutionId, UserId = instructorId, Kind = AcademixRoles.Instructor });
            db.TeachingAssignments.Add(new TeachingAssignment { SectionId = sectionId,
                InstitutionId = institutionId, UserId = assistantId, Kind = AcademixRoles.TeachingAssistant });
            await db.SaveChangesAsync();
        }
        var (_, outsider) = await User(outsiderId, AcademixRoles.Instructor);
        var questionDraft = new QuestionDraftRequest("SingleChoice", "Initial wording for snapshot", "Easy", 3m,
            [new("A", "Correct"), new("B", "Incorrect")], ["A"], [], "Do not show this solution", []);
        using var createQuestion = await Send(HttpMethod.Post,
            $"/api/exams/courses/{courseId}/questions", instructor, questionDraft);
        Assert.Equal(HttpStatusCode.Created, createQuestion.StatusCode);
        var question = (await createQuestion.Content.ReadFromJsonAsync<BankQuestionDetail>())!;
        var examRequest = new ExamDraftRequest("Midterm", "Instructions", 45, 2,
            DateTime.UtcNow.AddMinutes(10), DateTime.UtcNow.AddDays(2), true);
        using var createExam = await Send(HttpMethod.Post,
            $"/api/exams/sections/{sectionId}/exams", instructor, examRequest);
        Assert.Equal(HttpStatusCode.Created, createExam.StatusCode);
        var exam = (await createExam.Content.ReadFromJsonAsync<ExamDetail>())!;
        var examId = exam.Exam.Id;
        using (var denied = await Send(HttpMethod.Get, $"/api/exams/{examId}", student))
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        using (var hidden = await Send(HttpMethod.Get, $"/api/exams/{examId}", outsider))
            Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        using var replace = await Send(HttpMethod.Put, $"/api/exams/{examId}/questions", instructor,
            new ExamReplaceQuestionsRequest(exam.Exam.Version,
                [new ExamQuestionSelection(question.Question.Id, 1, 5m)]));
        Assert.Equal(HttpStatusCode.OK, replace.StatusCode);
        var populated = (await replace.Content.ReadFromJsonAsync<ExamDetail>())!;
        using (var denied = await Send(HttpMethod.Post, $"/api/exams/{examId}/publish", assistant,
            new ExamPublishRequest(populated.Exam.Version)))
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        using var publish = await Send(HttpMethod.Post, $"/api/exams/{examId}/publish", instructor,
            new ExamPublishRequest(populated.Exam.Version));
        Assert.Equal(HttpStatusCode.OK, publish.StatusCode);
        var preview = (await publish.Content.ReadFromJsonAsync<ExamPublishedPreview>())!;
        Assert.Equal("Published", preview.Exam.Status);
        Assert.Equal("Initial wording for snapshot", Assert.Single(preview.Questions).Stem);
        Assert.Equal(5m, preview.TotalPoints);
        var publishedJson = await publish.Content.ReadAsStringAsync();
        Assert.False(publishedJson.Contains("AnswerKey", StringComparison.OrdinalIgnoreCase));
        Assert.False(publishedJson.Contains("solution", StringComparison.OrdinalIgnoreCase));
        using (var retry = await Send(HttpMethod.Post, $"/api/exams/{examId}/publish", instructor,
            new ExamPublishRequest(populated.Exam.Version)))
            Assert.Equal(HttpStatusCode.Conflict, retry.StatusCode);
        using (var edit = await Send(HttpMethod.Put, $"/api/exams/{examId}", instructor,
            new ExamEditRequest(preview.Exam.Version, examRequest with { Title = "Modified" })))
            Assert.Equal(HttpStatusCode.Conflict, edit.StatusCode);
        using var updateQuestion = await Send(HttpMethod.Put,
            $"/api/exams/questions/{question.Question.Id}", instructor,
            new QuestionEditRequest(question.Question.Version, questionDraft with { Stem = "Changed after publication" }));
        Assert.Equal(HttpStatusCode.OK, updateQuestion.StatusCode);
        using var frozen = await Send(HttpMethod.Get, $"/api/exams/{examId}/published-preview", instructor);
        Assert.Equal(HttpStatusCode.OK, frozen.StatusCode);
        var frozenPreview = (await frozen.Content.ReadFromJsonAsync<ExamPublishedPreview>())!;
        Assert.Equal("Initial wording for snapshot", Assert.Single(frozenPreview.Questions).Stem);
    }
}
