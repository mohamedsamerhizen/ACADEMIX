using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Academix.Api.Data;
using Academix.Api.Domain.Academic;
using Academix.Api.Domain.Courses;
using Academix.Api.Domain.Identity;
using Academix.Api.Domain.Records;
using Academix.Api.DTOs.Curriculum;
using Academix.Api.DTOs.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Academix.Tests.Integration;

// Requires PostgreSQL/Testcontainers and all previous EF migrations.
// Deliberately not executed by the local ZIP installer.
public sealed class Phase15CurriculumTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private const string Password = "Curriculum!SecurePassword2026";

    private async Task<(Guid Id, string Token)> Account(Guid institution, string role)
    {
        var id = Guid.NewGuid(); var email = $"curr-{id:N}@example.test";
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser { Id = id, InstitutionId = institution, UserName = email, Email = email };
            Assert.True((await manager.CreateAsync(user, Password)).Succeeded);
            Assert.True((await manager.AddToRoleAsync(user, role)).Succeeded);
        }
        using var login = await fixture.Client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, Password));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        return (id, (await login.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken);
    }

    private async Task<HttpResponseMessage> Call(HttpMethod method, string path, string token, object? body = null)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null) request.Content = JsonContent.Create(body);
        return await fixture.Client.SendAsync(request);
    }

    [Fact]
    public async Task PlanPublishAssignmentGpaAndTenantIsolation()
    {
        var inst = Guid.NewGuid(); var dept = Guid.NewGuid(); var program = Guid.NewGuid();
        var term = Guid.NewGuid(); var course1 = Guid.NewGuid(); var course2 = Guid.NewGuid();
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AcademixDbContext>();
            db.Institutions.Add(new Institution { Id = inst, Code = "P15" + Guid.NewGuid().ToString("N")[..7].ToUpperInvariant(), Name = "Curriculum test institute" });
            db.Departments.Add(new Department { Id = dept, InstitutionId = inst, Code = "COM", Name = "Computing" });
            db.AcademicPrograms.Add(new AcademicProgram { Id = program, InstitutionId = inst, DepartmentId = dept, Code = "CS", Name = "Computing" });
            db.Courses.Add(Course.Create(course1, inst, "CS101", "Intro"));
            db.Courses.Add(Course.Create(course2, inst, "CS102", "Methods"));
            db.ProgramCourses.Add(new ProgramCourse { InstitutionId = inst, ProgramId = program, CourseId = course1 });
            db.ProgramCourses.Add(new ProgramCourse { InstitutionId = inst, ProgramId = program, CourseId = course2 });
            db.AcademicTerms.Add(new AcademicTerm { Id = term, InstitutionId = inst, Code = "T1", Name = "First term",
                StartsOn = new DateOnly(2026, 1, 1), EndsOn = new DateOnly(2027, 1, 1), Status = "Active" });
            await db.SaveChangesAsync();
        }
        var (_, admin) = await Account(inst, AcademixRoles.InstitutionAdministrator);
        var (studentId, student) = await Account(inst, AcademixRoles.Student);
        var otherInst = Guid.NewGuid();
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AcademixDbContext>();
            db.Institutions.Add(new Institution { Id = otherInst, Code = "OT" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(), Name = "Foreign" });
            db.StudentProgramMemberships.Add(new StudentProgramMembership { InstitutionId = inst,
                StudentId = studentId, ProgramId = program, AdmissionTermId = term, CurrentTermId = term });
            await db.SaveChangesAsync();
        }
        var (_, foreignAdmin) = await Account(otherInst, AcademixRoles.InstitutionAdministrator);
        using var denied = await Call(HttpMethod.Post, $"/api/curriculum/programs/{program}/plans", student,
            new CreateCurriculumPlan(2026, 1, "Bachelor of CS", 5m, 2m, 2m));
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        using var created = await Call(HttpMethod.Post, $"/api/curriculum/programs/{program}/plans", admin,
            new CreateCurriculumPlan(2026, 1, "Bachelor of CS", 5m, 2m, 2m));
        Assert.Equal(HttpStatusCode.OK, created.StatusCode);
        var p = (await created.Content.ReadFromJsonAsync<CurriculumPlanView>())!;
        using var courses = await Call(HttpMethod.Put, $"/api/curriculum/plans/{p.Id}/courses", admin,
            new ReplaceCurriculumCourses(p.Version,
                [new(course1, "Required", 3m, 1), new(course2, "Elective", 2m, 2)]));
        Assert.Equal(HttpStatusCode.OK, courses.StatusCode);
        p = (await courses.Content.ReadFromJsonAsync<CurriculumPlanView>())!;
        using var scale = await Call(HttpMethod.Put, $"/api/curriculum/plans/{p.Id}/grade-scale", admin,
            new ReplaceCurriculumScale(p.Version, [new(0m, 0m, "F"), new(60m, 2m, "C"), new(80m, 4m, "A")]));
        Assert.Equal(HttpStatusCode.OK, scale.StatusCode);
        p = (await scale.Content.ReadFromJsonAsync<CurriculumPlanView>())!;
        using var publish = await Call(HttpMethod.Post, $"/api/curriculum/plans/{p.Id}/publish", admin,
            new PublishCurriculumPlan(p.Version));
        Assert.Equal(HttpStatusCode.OK, publish.StatusCode);
        using var stale = await Call(HttpMethod.Put, $"/api/curriculum/plans/{p.Id}/grade-scale", admin,
            new ReplaceCurriculumScale(p.Version, [new(0m, 0m, "F"), new(60m, 2m, "C")]));
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using var assigned = await Call(HttpMethod.Post, $"/api/curriculum/students/{studentId}/assign", admin,
            new AssignStudentCurriculum(p.Id));
        Assert.Equal(HttpStatusCode.OK, assigned.StatusCode);
        using var audit = await Call(HttpMethod.Get, $"/api/curriculum/students/{studentId}/graduation-audit", student);
        Assert.Equal(HttpStatusCode.OK, audit.StatusCode);
        Assert.False((await audit.Content.ReadFromJsonAsync<StudentCurriculumAudit>())!.GraduationReady);
        using var gpa = await Call(HttpMethod.Get, $"/api/curriculum/students/{studentId}/gpa", student);
        Assert.Equal(HttpStatusCode.OK, gpa.StatusCode);
        Assert.Null((await gpa.Content.ReadFromJsonAsync<StudentGpaReport>())!.CumulativeGpa);
        using var tenant = await Call(HttpMethod.Get, $"/api/curriculum/students/{studentId}/gpa", foreignAdmin);
        Assert.Equal(HttpStatusCode.Forbidden, tenant.StatusCode);
    }
}
