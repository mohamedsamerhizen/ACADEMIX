using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Academix.Api.Data;
using Academix.Api.Domain.Academic;
using Academix.Api.Domain.Identity;
using Academix.Api.DTOs.Identity;
using Academix.Api.DTOs.Lifecycle;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Academix.Tests.Integration;

// Requires a real PostgreSQL Testcontainer and all accumulated migrations.
// The ZIP install intentionally does not run this test class.
public sealed class Phase16LifecycleTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private const string Password = "Academic!Lifecycle2026Secure";

    private async Task<(Guid Id, string Token)> Account(Guid institution, string role)
    {
        var id = Guid.NewGuid();
        var email = $"lifecycle-{id:N}@example.test";
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var account = new ApplicationUser { Id = id, InstitutionId = institution, UserName = email, Email = email };
            Assert.True((await manager.CreateAsync(account, Password)).Succeeded);
            Assert.True((await manager.AddToRoleAsync(account, role)).Succeeded);
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
    public async Task AdmissionApprovalIsSingleUse_TransferIsIsolated_GraduationNeedsAudit()
    {
        var inst = Guid.NewGuid(); var dept = Guid.NewGuid(); var program = Guid.NewGuid();
        var term = Guid.NewGuid(); var course = Guid.NewGuid();
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AcademixDbContext>();
            db.Institutions.Add(new Institution { Id = inst,
                Code = "LC" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(), Name = "Lifecycle test institute" });
            db.Departments.Add(new Department { Id = dept, InstitutionId = inst, Code = "ENG", Name = "Engineering" });
            db.AcademicPrograms.Add(new AcademicProgram { Id = program, InstitutionId = inst,
                DepartmentId = dept, Code = "ENG", Name = "Engineering" });
            db.AcademicTerms.Add(new AcademicTerm { Id = term, InstitutionId = inst,
                Code = "T1", Name = "First term", StartsOn = new DateOnly(2026, 1, 1),
                EndsOn = new DateOnly(2027, 1, 1), Status = "Active" });
            db.Courses.Add(Academix.Api.Domain.Courses.Course.Create(course, inst, "ENG101", "Foundations"));
            db.ProgramCourses.Add(new ProgramCourse { InstitutionId = inst, ProgramId = program, CourseId = course });
            await db.SaveChangesAsync();
        }
        var (studentId, student) = await Account(inst, AcademixRoles.Student);
        var (_, admin) = await Account(inst, AcademixRoles.InstitutionAdministrator);
        var (_, anotherStudent) = await Account(inst, AcademixRoles.Student);

        using var applied = await Call(HttpMethod.Post, $"/api/lifecycle/programs/{program}/admissions", student,
            new SubmitAdmission(term, "I request admission to the program."));
        Assert.Equal(HttpStatusCode.OK, applied.StatusCode);
        var application = (await applied.Content.ReadFromJsonAsync<AdmissionView>())!;
        using var cannotApproveSelf = await Call(HttpMethod.Post,
            $"/api/lifecycle/admissions/{application.Id}/decision", student,
            new LifecycleDecision(1, "Approve", "Eligible applicant"));
        Assert.Equal(HttpStatusCode.Forbidden, cannotApproveSelf.StatusCode);

        using var approved = await Call(HttpMethod.Post, $"/api/lifecycle/admissions/{application.Id}/decision", admin,
            new LifecycleDecision(1, "Approve", "Verified requirements"));
        Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
        using var duplicate = await Call(HttpMethod.Post, $"/api/lifecycle/admissions/{application.Id}/decision", admin,
            new LifecycleDecision(1, "Approve", "Verified requirements"));
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AcademixDbContext>();
            Assert.Equal(1, db.StudentProgramMemberships.Count(x => x.StudentId == studentId));
        }
        using var transfer = await Call(HttpMethod.Post, "/api/lifecycle/transfers", student,
            new SubmitTransferCredit("External College", [
                new TransferCourseRequest(course, "EXT101", "External Foundations", 3m)
            ]));
        Assert.Equal(HttpStatusCode.OK, transfer.StatusCode);
        var transferRow = (await transfer.Content.ReadFromJsonAsync<TransferView>())!;
        using var forbiddenTransfer = await Call(HttpMethod.Post,
            $"/api/lifecycle/transfers/{transferRow.Id}/decision", anotherStudent,
            new LifecycleDecision(1, "Approve", "Validated course"));
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenTransfer.StatusCode);
        using var approvedTransfer = await Call(HttpMethod.Post,
            $"/api/lifecycle/transfers/{transferRow.Id}/decision", admin,
            new LifecycleDecision(1, "Approve", "Transcript verified"));
        // Equivalency approvals require an assigned published plan and matching credit hours.
        Assert.Equal(HttpStatusCode.Conflict, approvedTransfer.StatusCode);
        using var graduation = await Call(HttpMethod.Post, "/api/lifecycle/graduations", student);
        Assert.Equal(HttpStatusCode.Conflict, graduation.StatusCode);
    }
}
