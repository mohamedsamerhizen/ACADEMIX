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

// PostgreSQL/Testcontainers workflow test. Explicitly excluded from Docker-independent install.
public sealed class Phase5GradingResultsTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private async Task<(Guid Id, string Token)> User(Guid institutionId, string role)
    {
        var email = $"grading-{Guid.NewGuid():N}@example.test";
        const string password = "Secure!Grading2026";
        var id = Guid.NewGuid();
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser { Id = id, InstitutionId = institutionId, Email = email, UserName = email };
            Assert.True((await users.CreateAsync(user, password)).Succeeded);
            Assert.True((await users.AddToRoleAsync(user, role)).Succeeded);
        }
        using var login = await fixture.Client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        return (id, (await login.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken);
    }

    private async Task<HttpResponseMessage> Send(HttpMethod method, string route, string token,
        object? content = null)
    {
        using var req = new HttpRequestMessage(method, route);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (content is not null) req.Content = JsonContent.Create(content);
        return await fixture.Client.SendAsync(req);
    }

    private async Task<(Guid AttemptId, Guid ExamId, Guid SingleId, Guid EssayId, string Student, string Teacher)> Prepare()
    {
        var instId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var sectionId = Guid.NewGuid();
        var termId = Guid.NewGuid();
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AcademixDbContext>();
            db.Institutions.Add(new Institution { Id = instId,
                Code = "G" + Guid.NewGuid().ToString("N")[..7], Name = "Grading Institute" });
            db.Courses.Add(Course.Create(courseId, instId, "GRD210", "Grading Course"));
            db.AcademicTerms.Add(new AcademicTerm { Id = termId, InstitutionId = instId,
                Code = "GRD", Name = "Grading Term", Status = "Active",
                StartsOn = new DateOnly(2026, 10, 1), EndsOn = new DateOnly(2027, 7, 1) });
            db.CourseSections.Add(new CourseSection { Id = sectionId, InstitutionId = instId,
                CourseId = courseId, TermId = termId, Code = "A", Capacity = 40, Status = "Open" });
            await db.SaveChangesAsync();
        }
        var (teacherId, teacher) = await User(instId, AcademixRoles.Instructor);
        var (studentId, student) = await User(instId, AcademixRoles.Student);
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AcademixDbContext>();
            db.TeachingAssignments.Add(new TeachingAssignment { InstitutionId = instId,
                SectionId = sectionId, UserId = teacherId, Kind = AcademixRoles.Instructor });
            db.StudentEnrollments.Add(new StudentEnrollment { InstitutionId = instId,
                SectionId = sectionId, StudentId = studentId, Status = "Active" });
            await db.SaveChangesAsync();
        }
        var single = new QuestionDraftRequest("SingleChoice", "Pick the right letter", "Easy", 4m,
            [new("A", "Correct"), new("B", "Wrong")], ["A"], [], "Secret key text", []);
        var essay = new QuestionDraftRequest("Essay", "Explain the grading rubric", "Medium", 6m,
            [], [], [], null, []);
        using var q1Response = await Send(HttpMethod.Post,
            $"/api/exams/courses/{courseId}/questions", teacher, single);
        using var q2Response = await Send(HttpMethod.Post,
            $"/api/exams/courses/{courseId}/questions", teacher, essay);
        Assert.Equal(HttpStatusCode.Created, q1Response.StatusCode);
        Assert.Equal(HttpStatusCode.Created, q2Response.StatusCode);
        var q1 = (await q1Response.Content.ReadFromJsonAsync<BankQuestionDetail>())!;
        var q2 = (await q2Response.Content.ReadFromJsonAsync<BankQuestionDetail>())!;
        using var create = await Send(HttpMethod.Post,
            $"/api/exams/sections/{sectionId}/exams", teacher,
            new ExamDraftRequest("Grading exam", "Instructions", 45, 1,
                DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddHours(2), false));
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var exam = (await create.Content.ReadFromJsonAsync<ExamDetail>())!;
        using var selection = await Send(HttpMethod.Put, $"/api/exams/{exam.Exam.Id}/questions", teacher,
            new ExamReplaceQuestionsRequest(exam.Exam.Version,
                [new ExamQuestionSelection(q1.Question.Id, 1, 4m),
                 new ExamQuestionSelection(q2.Question.Id, 1, 6m)]));
        Assert.Equal(HttpStatusCode.OK, selection.StatusCode);
        var selected = (await selection.Content.ReadFromJsonAsync<ExamDetail>())!;
        using var publish = await Send(HttpMethod.Post,
            $"/api/exams/{exam.Exam.Id}/publish", teacher,
            new ExamPublishRequest(selected.Exam.Version));
        Assert.Equal(HttpStatusCode.OK, publish.StatusCode);
        using var start = await Send(HttpMethod.Post,
            $"/api/exam-taking/exams/{exam.Exam.Id}/attempts", student);
        Assert.Equal(HttpStatusCode.OK, start.StatusCode);
        var detail = (await start.Content.ReadFromJsonAsync<ExamAttemptDetail>())!;
        Assert.Equal(2, detail.Questions.Count);
        var ids = detail.Questions.ToDictionary(x => x.Kind, x => x.PublishedQuestionId);
        using var saveOne = await Send(HttpMethod.Put,
            $"/api/exam-taking/attempts/{detail.Attempt.Id}/answers/{ids["SingleChoice"]}", student,
            new SaveExamAnswerRequest(1, JsonDocument.Parse("\"A\"").RootElement.Clone()));
        using var saveEssay = await Send(HttpMethod.Put,
            $"/api/exam-taking/attempts/{detail.Attempt.Id}/answers/{ids["Essay"]}", student,
            new SaveExamAnswerRequest(1, JsonDocument.Parse("\"My long essay text\"").RootElement.Clone()));
        Assert.Equal(HttpStatusCode.OK, saveOne.StatusCode);
        Assert.Equal(HttpStatusCode.OK, saveEssay.StatusCode);
        using var submit = await Send(HttpMethod.Post,
            $"/api/exam-taking/attempts/{detail.Attempt.Id}/submit", student);
        Assert.Equal(HttpStatusCode.OK, submit.StatusCode);
        return (detail.Attempt.Id, exam.Exam.Id, ids["SingleChoice"], ids["Essay"], student, teacher);
    }

    [Fact]
    public async Task GradePublishResultAppealAndCorrectionPreservePermissionsAndAudit()
    {
        var context = await Prepare();
        var route = $"/api/exam-grading/attempts/{context.AttemptId}";
        using (var hidden = await Send(HttpMethod.Get,
            $"/api/exam-results/attempts/{context.AttemptId}", context.Student))
            Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        using (var forbidden = await Send(HttpMethod.Post, route + "/initialize", context.Student))
            Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        using var init = await Send(HttpMethod.Post, route + "/initialize", context.Teacher);
        Assert.Equal(HttpStatusCode.OK, init.StatusCode);
        var report = (await init.Content.ReadFromJsonAsync<ExamStaffGradeView>())!;
        Assert.Equal(4m, report.Report.EarnedPoints);
        Assert.Equal(10m, report.Report.MaximumPoints);
        Assert.Equal(1, report.Report.PendingManual);
        using var again = await Send(HttpMethod.Post, route + "/initialize", context.Teacher);
        Assert.Equal(report.Report.ReportId,
            (await again.Content.ReadFromJsonAsync<ExamStaffGradeView>())!.Report.ReportId);
        using (var blocked = await Send(HttpMethod.Post, route + "/publish", context.Teacher,
            new ExamPublishGradesRequest(report.Report.Version)))
            Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        using var grade = await Send(HttpMethod.Put,
            route + $"/questions/{context.EssayId}/grade", context.Teacher,
            new ExamManualGradeRequest(report.Report.Version, 5m, "Good", "Essay rubric checked"));
        Assert.Equal(HttpStatusCode.OK, grade.StatusCode);
        var graded = (await grade.Content.ReadFromJsonAsync<ExamStaffGradeView>())!;
        Assert.Equal(9m, graded.Report.EarnedPoints);
        Assert.Equal(0, graded.Report.PendingManual);
        using var publish = await Send(HttpMethod.Post, route + "/publish", context.Teacher,
            new ExamPublishGradesRequest(graded.Report.Version));
        Assert.Equal(HttpStatusCode.OK, publish.StatusCode);
        using var published = await Send(HttpMethod.Get,
            $"/api/exam-results/attempts/{context.AttemptId}", context.Student);
        Assert.Equal(HttpStatusCode.OK, published.StatusCode);
        var studentText = await published.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Secret key text", studentText);
        Assert.DoesNotContain("answerKey", studentText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("studentAnswerJson", studentText, StringComparison.OrdinalIgnoreCase);
        var studentResult = (await published.Content.ReadFromJsonAsync<ExamStudentResult>())!;
        Assert.Equal(9m, studentResult.Report.EarnedPoints);
        using var appeal = await Send(HttpMethod.Post,
            $"/api/exam-results/attempts/{context.AttemptId}/appeal", context.Student,
            new ExamAppealRequest("Please review the essay criterion."));
        Assert.Equal(HttpStatusCode.OK, appeal.StatusCode);
        var appealView = (await appeal.Content.ReadFromJsonAsync<ExamAppealView>())!;
        using (var duplicate = await Send(HttpMethod.Post,
            $"/api/exam-results/attempts/{context.AttemptId}/appeal", context.Student,
            new ExamAppealRequest("Second attempt to appeal.")))
            Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        using var resolve = await Send(HttpMethod.Post,
            $"/api/exam-grading/appeals/{appealView.Id}/resolve", context.Teacher,
            new ExamResolveAppealRequest(studentResult.Report.Version, true,
                "Essay rechecked and full credit approved.", context.EssayId, 6m));
        Assert.Equal(HttpStatusCode.OK, resolve.StatusCode);
        using var revised = await Send(HttpMethod.Get,
            $"/api/exam-results/attempts/{context.AttemptId}", context.Student);
        Assert.Equal(10m, (await revised.Content.ReadFromJsonAsync<ExamStudentResult>())!.Report.EarnedPoints);
        using var history = await Send(HttpMethod.Get, route + "/history", context.Teacher);
        Assert.Equal(HttpStatusCode.OK, history.StatusCode);
        var log = (await history.Content.ReadFromJsonAsync<List<ExamGradeAuditView>>())!;
        Assert.Contains(log, x => x.Action == "AppealCorrection" && x.PreviousPoints == 5m && x.NewPoints == 6m);
        using (var denied = await Send(HttpMethod.Get, route + "/history", context.Student))
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }
}
