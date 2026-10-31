using Microsoft.EntityFrameworkCore;
using Academix.Api.Services.Examinations;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Academix.Api.Data;
using Academix.Api.Domain.Academic;
using Academix.Api.Domain.Courses;
using Academix.Api.Domain.Identity;
using Academix.Api.DTOs.Examinations;
using Academix.Api.DTOs.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Academix.Tests.Integration;

// Real PostgreSQL via Testcontainers. Not included in Docker-independent test filter.
public sealed class Phase4ExamExecutionTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private async Task<(Guid Id, string Token)> User(Guid institutionId, string role)
    {
        var email = $"attempt-{Guid.NewGuid():N}@example.test";
        const string password = "Secure!Execution2026";
        var id = Guid.NewGuid();
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

    private async Task<HttpResponseMessage> Send(HttpMethod method, string path, string token, object? body = null)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null) request.Content = JsonContent.Create(body);
        return await fixture.Client.SendAsync(request);
    }

    private async Task<(Guid ExamId, Guid SectionId, Guid StudentId, string Student, string Instructor)> Setup()
    {
        var institutionId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var sectionId = Guid.NewGuid();
        var termId = Guid.NewGuid();
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AcademixDbContext>();
            db.Institutions.Add(new Institution { Id = institutionId,
                Code = "X" + Guid.NewGuid().ToString("N")[..7], Name = "Execution Institution" });
            db.Courses.Add(Course.Create(courseId, institutionId, "EXE202", "Execution Course"));
            db.AcademicTerms.Add(new AcademicTerm { Id = termId, InstitutionId = institutionId,
                Code = "EXE", Name = "Term", Status = "Active",
                StartsOn = new DateOnly(2026, 10, 1), EndsOn = new DateOnly(2027, 7, 1) });
            db.CourseSections.Add(new CourseSection { Id = sectionId, InstitutionId = institutionId,
                CourseId = courseId, TermId = termId, Code = "A", Capacity = 30, Status = "Open" });
            await db.SaveChangesAsync();
        }
        var (staffId, staffJwt) = await User(institutionId, AcademixRoles.Instructor);
        var (studentId, studentJwt) = await User(institutionId, AcademixRoles.Student);
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AcademixDbContext>();
            db.TeachingAssignments.Add(new TeachingAssignment { InstitutionId = institutionId,
                SectionId = sectionId, UserId = staffId, Kind = AcademixRoles.Instructor });
            db.StudentEnrollments.Add(new StudentEnrollment { InstitutionId = institutionId,
                SectionId = sectionId, StudentId = studentId, Status = "Active" });
            await db.SaveChangesAsync();
        }
        var question = new QuestionDraftRequest("SingleChoice", "Choose the correct number", "Easy", 2m,
            [new("A", "One"), new("B", "Two")], ["A"], [], "SECRET SOLUTION", []);
        using var qResponse = await Send(HttpMethod.Post,
            $"/api/exams/courses/{courseId}/questions", staffJwt, question);
        Assert.Equal(HttpStatusCode.Created, qResponse.StatusCode);
        var q = (await qResponse.Content.ReadFromJsonAsync<BankQuestionDetail>())!;
        using var create = await Send(HttpMethod.Post,
            $"/api/exams/sections/{sectionId}/exams", staffJwt,
            new ExamDraftRequest("Execution test", "Instructions", 40, 1,
                DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddHours(2), true));
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var exam = (await create.Content.ReadFromJsonAsync<ExamDetail>())!;
        using var replace = await Send(HttpMethod.Put, $"/api/exams/{exam.Exam.Id}/questions", staffJwt,
            new ExamReplaceQuestionsRequest(exam.Exam.Version,
                [new ExamQuestionSelection(q.Question.Id, 1, 2m)]));
        Assert.Equal(HttpStatusCode.OK, replace.StatusCode);
        var populated = (await replace.Content.ReadFromJsonAsync<ExamDetail>())!;
        using var publish = await Send(HttpMethod.Post, $"/api/exams/{exam.Exam.Id}/publish", staffJwt,
            new ExamPublishRequest(populated.Exam.Version));
        Assert.Equal(HttpStatusCode.OK, publish.StatusCode);
        return (exam.Exam.Id, sectionId, studentId, studentJwt, staffJwt);
    }

    [Fact]
    public async Task StudentCanStartResumeAutosaveAndSubmitWithoutSeeingAnswerKey()
    {
        var setup = await Setup();
        var startUrl = $"/api/exam-taking/exams/{setup.ExamId}/attempts";
        using (var forbidden = await Send(HttpMethod.Post, startUrl, setup.Instructor))
            Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        using var start = await Send(HttpMethod.Post, startUrl, setup.Student);
        Assert.Equal(HttpStatusCode.OK, start.StatusCode);
        var detail = (await start.Content.ReadFromJsonAsync<ExamAttemptDetail>())!;
        var text = await start.Content.ReadAsStringAsync();
        Assert.DoesNotContain("SECRET SOLUTION", text);
        Assert.DoesNotContain("AnswerKeyJson", text);
        Assert.Equal("InProgress", detail.Attempt.Status);
        Assert.Single(detail.Questions);
        var item = detail.Questions[0];
        using var retryStart = await Send(HttpMethod.Post, startUrl, setup.Student);
        Assert.Equal(detail.Attempt.Id, (await retryStart.Content.ReadFromJsonAsync<ExamAttemptDetail>())!.Attempt.Id);
        var url = $"/api/exam-taking/attempts/{detail.Attempt.Id}/answers/{item.PublishedQuestionId}";
        using var save = await Send(HttpMethod.Put, url, setup.Student,
            new SaveExamAnswerRequest(1, JsonDocument.Parse("\"A\"").RootElement.Clone()));
        Assert.Equal(HttpStatusCode.OK, save.StatusCode);
        using var retrySave = await Send(HttpMethod.Put, url, setup.Student,
            new SaveExamAnswerRequest(1, JsonDocument.Parse("\"A\"").RootElement.Clone()));
        Assert.Equal(HttpStatusCode.OK, retrySave.StatusCode);
        using (var stale = await Send(HttpMethod.Put, url, setup.Student,
            new SaveExamAnswerRequest(1, JsonDocument.Parse("\"B\"").RootElement.Clone())))
            Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using var resume = await Send(HttpMethod.Get,
            $"/api/exam-taking/attempts/{detail.Attempt.Id}", setup.Student);
        var resumed = (await resume.Content.ReadFromJsonAsync<ExamAttemptDetail>())!;
        Assert.Single(resumed.SavedAnswers);
        using var submit = await Send(HttpMethod.Post,
            $"/api/exam-taking/attempts/{detail.Attempt.Id}/submit", setup.Student);
        Assert.Equal(HttpStatusCode.OK, submit.StatusCode);
        var first = (await submit.Content.ReadFromJsonAsync<ExamSubmissionReceipt>())!;
        Assert.Equal("Submitted", first.Status);
        Assert.Equal(1, first.AnsweredQuestionCount);
        using var duplicate = await Send(HttpMethod.Post,
            $"/api/exam-taking/attempts/{detail.Attempt.Id}/submit", setup.Student);
        Assert.Equal(first.SubmittedAtUtc, (await duplicate.Content.ReadFromJsonAsync<ExamSubmissionReceipt>())!.SubmittedAtUtc);
        using (var late = await Send(HttpMethod.Put, url, setup.Student,
            new SaveExamAnswerRequest(2, JsonDocument.Parse("\"B\"").RootElement.Clone())))
            Assert.Equal(HttpStatusCode.Conflict, late.StatusCode);
        using (var extra = await Send(HttpMethod.Post, startUrl, setup.Student))
            Assert.Equal(HttpStatusCode.Conflict, extra.StatusCode);
    }

    [Fact]
    public async Task ExpiredAttemptsRejectLateAnswersAndSubmission()
    {
        var setup = await Setup();
        using var start = await Send(HttpMethod.Post,
            $"/api/exam-taking/exams/{setup.ExamId}/attempts", setup.Student);
        var detail = (await start.Content.ReadFromJsonAsync<ExamAttemptDetail>())!;
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AcademixDbContext>();
            var attempt = await db.ExamAttempts.FindAsync(detail.Attempt.Id);
            Assert.NotNull(attempt);
            attempt!.StartedAtUtc = DateTime.UtcNow.AddHours(-2);
            attempt.DeadlineUtc = DateTime.UtcNow.AddSeconds(-5);
            await db.SaveChangesAsync();
        }
        var item = Assert.Single(detail.Questions);
        using var answer = await Send(HttpMethod.Put,
            $"/api/exam-taking/attempts/{detail.Attempt.Id}/answers/{item.PublishedQuestionId}",
            setup.Student, new SaveExamAnswerRequest(1, JsonDocument.Parse("\"A\"").RootElement.Clone()));
        Assert.Equal(HttpStatusCode.Conflict, answer.StatusCode);
        using var submit = await Send(HttpMethod.Post,
            $"/api/exam-taking/attempts/{detail.Attempt.Id}/submit", setup.Student);
        Assert.Equal(HttpStatusCode.Conflict, submit.StatusCode);
        using var resume = await Send(HttpMethod.Get,
            $"/api/exam-taking/attempts/{detail.Attempt.Id}", setup.Student);
        Assert.Equal("Expired", (await resume.Content.ReadFromJsonAsync<ExamAttemptDetail>())!.Attempt.Status);
    }
    [Fact]
    public async Task TimeoutFinalizationPreservesAnswersIsIdempotentAndEntersGrading()
    {
        var setup = await Setup();
        using var started = await Send(HttpMethod.Post, $"/api/exam-taking/exams/{setup.ExamId}/attempts", setup.Student);
        started.EnsureSuccessStatusCode();
        var detail = (await started.Content.ReadFromJsonAsync<ExamAttemptDetail>())!;
        var question = Assert.Single(detail.Questions);
        using var saved = await Send(HttpMethod.Put, $"/api/exam-taking/attempts/{detail.Attempt.Id}/answers/{question.PublishedQuestionId}",
            setup.Student, new SaveExamAnswerRequest(1, JsonDocument.Parse("\"A\"").RootElement.Clone()));
        saved.EnsureSuccessStatusCode();
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AcademixDbContext>();
            await db.ExamAttempts.Where(x => x.Id == detail.Attempt.Id).ExecuteUpdateAsync(s => s
                .SetProperty(x => x.StartedAtUtc, DateTime.UtcNow.AddHours(-1)).SetProperty(x => x.DeadlineUtc, DateTime.UtcNow.AddMinutes(-1)));
        }
        async Task FinalizeAttempt()
        {
            await using var scope = fixture.Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<ExamExpirationService>().FinalizeOne(detail.Attempt.Id, default);
        }
        await Task.WhenAll(FinalizeAttempt(), FinalizeAttempt());
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AcademixDbContext>();
            Assert.Equal("Expired", (await db.ExamAttempts.AsNoTracking().SingleAsync(x => x.Id == detail.Attempt.Id)).Status);
            Assert.Equal("\"A\"", (await db.ExamAttemptAnswers.AsNoTracking().SingleAsync(x => x.AttemptId == detail.Attempt.Id)).AnswerJson);
            Assert.Equal(1, await db.SecurityAuditEvents.CountAsync(x => x.Action == "exam.attempt.timeout" && x.Target == detail.Attempt.Id.ToString()));
        }
        using var grading = await Send(HttpMethod.Post, $"/api/exam-grading/attempts/{detail.Attempt.Id}/initialize", setup.Instructor);
        Assert.Equal(HttpStatusCode.OK, grading.StatusCode);
        using var late = await Send(HttpMethod.Put, $"/api/exam-taking/attempts/{detail.Attempt.Id}/answers/{question.PublishedQuestionId}",
            setup.Student, new SaveExamAnswerRequest(2, JsonDocument.Parse("\"B\"").RootElement.Clone()));
        Assert.Equal(HttpStatusCode.Conflict, late.StatusCode);
    }

}
