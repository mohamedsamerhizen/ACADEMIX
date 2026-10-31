using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Academix.Api.Data;
using Academix.Api.Domain.Academic;
using Academix.Api.Domain.Courses;
using Academix.Api.Domain.Identity;
using Academix.Api.DTOs.Assessment;
using Academix.Api.DTOs.Academic;
using Academix.Api.DTOs.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Academix.Tests.Integration;

// PostgreSQL/Testcontainers: excluded by the normal Docker-independent test filter.
public sealed class Phase9GradebookTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private const string Password = "Valid!TestPassword2026";

    private async Task<(Guid Id, string Token)> User(Guid institution, string role)
    {
        var id = Guid.NewGuid();
        var email = $"gradebook-{Guid.NewGuid():N}@example.test";
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

    private async Task<(Guid Institution, Guid Section, Guid Term)> Seed()
    {
        var inst = Guid.NewGuid(); var section = Guid.NewGuid(); var course = Guid.NewGuid(); var term = Guid.NewGuid();
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AcademixDbContext>();
        db.Institutions.Add(new Institution { Id = inst, Code = "GB" + Guid.NewGuid().ToString("N")[..8], Name = "Assessment Institution" });
        db.Courses.Add(Course.Create(course, inst, "SCI101", "Assessment Course"));
        db.AcademicTerms.Add(new AcademicTerm { Id = term, InstitutionId = inst, Code = "FALL", Name = "Fall", StartsOn = new DateOnly(2026, 1, 1), EndsOn = new DateOnly(2027, 1, 1) });
        db.CourseSections.Add(new CourseSection { Id = section, InstitutionId = inst, CourseId = course, TermId = term,
            Code = "S1", Status = "Open", Capacity = 50 });
        await db.SaveChangesAsync();
        return (inst, section, term);
    }

    private async Task<HttpResponseMessage> Send(HttpMethod method, string path, string token, object? body = null)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null) request.Content = JsonContent.Create(body);
        return await fixture.Client.SendAsync(request);
    }

    [Fact]
    public async Task DraftPreviewPublicationStudentVisibilityCorrectionAndAudit()
    {
        var (inst, section, term) = await Seed();
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
        var prefix = $"/api/gradebook/sections/{section}";
        using var configure = await Send(HttpMethod.Put, $"{prefix}/policy", admin.Token, new ConfigureGradePolicy(60, 0));
        Assert.Equal(HttpStatusCode.OK, configure.StatusCode);
        var policy = (await configure.Content.ReadFromJsonAsync<GradePolicyView>())!;
        using var categoryCreate = await Send(HttpMethod.Post, $"{prefix}/categories", admin.Token,
            new GradeCategoryInput("Coursework", 100));
        Assert.Equal(HttpStatusCode.OK, categoryCreate.StatusCode);
        var category = (await categoryCreate.Content.ReadFromJsonAsync<GradePolicyView>())!.Categories.Single();
        using var itemCreate = await Send(HttpMethod.Post, $"{prefix}/components", admin.Token,
            new GradeComponentInput(category.Id, "Attendance", "Manual", null, 100, 20));
        Assert.Equal(HttpStatusCode.OK, itemCreate.StatusCode);
        var component = (await itemCreate.Content.ReadFromJsonAsync<GradePolicyView>())!.Categories.Single().Components.Single();
        using var notYet = await Send(HttpMethod.Get, $"{prefix}/my-result", student.Token);
        Assert.Equal(HttpStatusCode.NotFound, notYet.StatusCode);
        using var outsiderDenied = await Send(HttpMethod.Get, $"{prefix}/my-result", outsider.Token);
        Assert.Equal(HttpStatusCode.NotFound, outsiderDenied.StatusCode);
        using var draft = await Send(HttpMethod.Get, $"{prefix}/preview", admin.Token);
        Assert.Equal(HttpStatusCode.OK, draft.StatusCode);
        Assert.Equal(1, (await draft.Content.ReadFromJsonAsync<GradebookPreview>())!.IncompleteStudents);
        using var early = await Send(HttpMethod.Post, $"{prefix}/publish", admin.Token,
            new PublishGradebookRequest(3));
        Assert.Equal(HttpStatusCode.Conflict, early.StatusCode);
        using var graded = await Send(HttpMethod.Put,
            $"/api/gradebook/components/{component.Id}/students/{student.Id}/grade", admin.Token,
            new ManualGradeRequest(16, 0));
        Assert.Equal(HttpStatusCode.OK, graded.StatusCode);
        var gradeView = (await graded.Content.ReadFromJsonAsync<ManualGradeView>())!;
        Assert.Equal(16m, gradeView.Points);
        using var freshPolicy = await Send(HttpMethod.Get, $"{prefix}/policy", admin.Token);
        var current = (await freshPolicy.Content.ReadFromJsonAsync<GradePolicyView>())!;
        using var publish = await Send(HttpMethod.Post, $"{prefix}/publish", admin.Token,
            new PublishGradebookRequest(current.Version));
        Assert.Equal(HttpStatusCode.OK, publish.StatusCode);
        using var lateEnroll = await Send(HttpMethod.Post, $"/api/academic/sections/{section}/enrollments", admin.Token, new EnrollmentCreate(outsider.Id));
        Assert.Equal(HttpStatusCode.Conflict, lateEnroll.StatusCode);
        using var readStudent = await Send(HttpMethod.Get, $"{prefix}/my-result", student.Token);
        Assert.Equal(HttpStatusCode.OK, readStudent.StatusCode);
        var grade = (await readStudent.Content.ReadFromJsonAsync<GradebookResultView>())!;
        Assert.Equal(80m, grade.Percentage);
        Assert.True(grade.Passed);
        using var blocked = await Send(HttpMethod.Post, $"{prefix}/categories", admin.Token,
            new GradeCategoryInput("Too late", 1));
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        using var correction = await Send(HttpMethod.Put,
            $"{prefix}/students/{student.Id}/correction", admin.Token,
            new CorrectGradebookResult(55, "Rechecked physical attendance", grade.Version));
        Assert.Equal(HttpStatusCode.OK, correction.StatusCode);
        var updated = (await correction.Content.ReadFromJsonAsync<GradebookResultView>())!;
        Assert.False(updated.Passed);
        using var stale = await Send(HttpMethod.Put,
            $"{prefix}/students/{student.Id}/correction", admin.Token,
            new CorrectGradebookResult(80, "Outdated update", grade.Version));
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using var audit = await Send(HttpMethod.Get, $"{prefix}/audit", admin.Token);
        Assert.Equal(HttpStatusCode.OK, audit.StatusCode);
        Assert.Equal(2, (await audit.Content.ReadFromJsonAsync<List<GradebookAuditView>>())!.Count);
        using var termSummary = await Send(HttpMethod.Get, $"/api/gradebook/terms/{term}/my-summary", student.Token);
        Assert.Equal(HttpStatusCode.OK, termSummary.StatusCode);
        Assert.Equal(55m, (await termSummary.Content.ReadFromJsonAsync<TermGradeSummary>())!.UnweightedAveragePercentage);
    }
}
