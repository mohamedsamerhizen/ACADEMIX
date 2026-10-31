using System.Security.Claims;
using Academix.Api.Data;
using Academix.Api.Domain.Academic;
using Academix.Api.Domain.Identity;
using Academix.Api.Domain.Courses;
using Academix.Api.DTOs.Registrar;
using Academix.Api.DTOs.Academic;
using Academix.Api.Middleware;
using Academix.Api.Services.Academic;
using Academix.Api.Services.Registrar;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Academix.Tests.Integration;

// Each test uses its own institution, accounts, term and section. Real PostgreSQL only.
public sealed class Phase19RegistrarTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private async Task<(Guid Inst, ClaimsPrincipal Creator, ClaimsPrincipal Reviewer, ClaimsPrincipal Student, Guid StudentId, Guid Section, Guid Term)> Setup()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AcademixDbContext>();
        var inst = new Institution { Code = Guid.NewGuid().ToString("N")[..12], Name = "Phase19 Tests" };
        db.Institutions.Add(inst); await db.SaveChangesAsync();
        var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        async Task<(ClaimsPrincipal Principal, Guid Id)> Account(string role)
        {
            var email = Guid.NewGuid().ToString("N") + "@example.test";
            var account = new ApplicationUser { Id = Guid.NewGuid(), InstitutionId = inst.Id, UserName = email, Email = email };
            Assert.True((await manager.CreateAsync(account, "Safe!Phase19Test2026")).Succeeded);
            Assert.True((await manager.AddToRoleAsync(account, role)).Succeeded);
            return (new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, account.Id.ToString()), new Claim("institution_id", inst.Id.ToString()) }, "test")), account.Id);
        }
        var creator = await Account(AcademixRoles.InstitutionAdministrator);
        var reviewer = await Account(AcademixRoles.InstitutionAdministrator);
        var student = await Account(AcademixRoles.Student);
        var term = new AcademicTerm { InstitutionId = inst.Id, Code = "P19", Name = "Term", StartsOn = new(2026, 1, 1), EndsOn = new(2027, 1, 1), Status = "Active" };
        var course = Course.Create(Guid.NewGuid(), inst.Id, "PR19", "Course");
        db.AcademicTerms.Add(term); db.Courses.Add(course);
        var section = new CourseSection { InstitutionId = inst.Id, TermId = term.Id, CourseId = course.Id, Code = "P19", Status = "Open", Capacity = 10000 };
        db.CourseSections.Add(section); await db.SaveChangesAsync();
        return (inst.Id, creator.Principal, reviewer.Principal, student.Principal, student.Id, section.Id, term.Id);
    }
    private static PolicyDraft Draft() => new(null, DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddYears(1), new(true, true, null, true));

    [Fact]
    public async Task RegistrationHistoryDoesNotExposeLegacyCrossInstitutionStudentLinks()
    {
        var own = await Setup(); var foreign = await Setup();
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AcademixDbContext>();
        // Historical enrollment constraints link StudentId alone. Reproduce a bad
        // legacy row without weakening those constraints or deleting its data.
        db.StudentEnrollments.Add(new StudentEnrollment { InstitutionId = foreign.Inst, SectionId = foreign.Section, StudentId = own.StudentId });
        await db.SaveChangesAsync();
        var history = await scope.ServiceProvider.GetRequiredService<AcademicOperations>().StudentHistory(own.Student, own.StudentId, default);
        Assert.Empty(System.Text.Json.JsonSerializer.SerializeToElement(history).EnumerateArray());
        Assert.Equal(1, await db.StudentEnrollments.CountAsync(x => x.StudentId == own.StudentId));
    }

    [Fact]
    public async Task PolicyPublicationRequiresIndependentReviewerAndPreventsOverlap()
    {
        var setup = await Setup();
        await using var scope = fixture.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<AcademicPolicyService>();
        var first = await service.Draft(setup.Creator, setup.Inst, Draft(), default);
        Assert.Equal(403, (await Assert.ThrowsAsync<ApiFault>(() => service.Publish(setup.Creator, setup.Inst, first.Id, default))).StatusCode);
        Assert.Equal("Published", (await service.Publish(setup.Reviewer, setup.Inst, first.Id, default)).Status);
        var next = await service.Draft(setup.Creator, setup.Inst, Draft(), default);
        Assert.Equal(409, (await Assert.ThrowsAsync<ApiFault>(() => service.Publish(setup.Reviewer, setup.Inst, next.Id, default))).StatusCode);
    }

    [Fact]
    public async Task StudentAndOtherInstitutionCannotChangePolicy()
    {
        var one = await Setup(); var two = await Setup();
        await using var scope = fixture.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<AcademicPolicyService>();
        Assert.Equal(403, (await Assert.ThrowsAsync<ApiFault>(() => service.Draft(one.Student, one.Inst, Draft(), default))).StatusCode);
        Assert.Equal(403, (await Assert.ThrowsAsync<ApiFault>(() => service.Draft(two.Creator, one.Inst, Draft(), default))).StatusCode);
    }

    [Fact]
    public async Task EnrollmentAndBulkEligibilityRespectClosedTermAndAcademicHolds()
    {
        var setup = await Setup();
        await using var scope = fixture.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<AcademicPolicyService>();
        var hold = await service.AddHold(setup.Creator, setup.Inst, setup.StudentId, new("Registrar review required"), default);
        var academic = scope.ServiceProvider.GetRequiredService<AcademicOperations>();
        Assert.Equal(409, (await Assert.ThrowsAsync<ApiFault>(() => academic.Enroll(setup.Creator, setup.Section, new EnrollmentCreate(setup.StudentId), default))).StatusCode);
        await service.ReleaseHold(setup.Reviewer, setup.Inst, hold.Id, default);
        var db = scope.ServiceProvider.GetRequiredService<AcademixDbContext>();
        await db.AcademicTerms.Where(x => x.Id == setup.Term).ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, "Closed"));
        Assert.Equal(409, (await Assert.ThrowsAsync<ApiFault>(() => academic.Enroll(setup.Creator, setup.Section, new EnrollmentCreate(setup.StudentId), default))).StatusCode);
        Assert.Empty(await db.StudentEnrollments.Where(x => x.SectionId == setup.Section).ToListAsync());
    }

    [Fact]
    public async Task DecisionSnapshotsRemainHistoricalWhenHoldIsReleased()
    {
        var setup = await Setup();
        await using var scope = fixture.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<AcademicPolicyService>();
        var hold = await service.AddHold(setup.Creator, setup.Inst, setup.StudentId, new("Review registration hold"), default);
        var result = await service.Evaluate(setup.Student, setup.Inst, new(setup.StudentId, setup.Section), default);
        Assert.False(result.Eligible); Assert.Contains("ActiveAcademicHold", result.Reasons);
        await service.ReleaseHold(setup.Creator, setup.Inst, hold.Id, default);
        Assert.False(Assert.Single(await service.Decisions(setup.Student, setup.Inst, setup.StudentId, 1, default)).Eligible);
        Assert.True((await service.Evaluate(setup.Student, setup.Inst, new(setup.StudentId, setup.Section), default)).Eligible);
    }

    [Fact]
    public async Task ImportInvalidStudentDryRunCannotBeApprovedOrApplied()
    {
        var setup = await Setup();
        await using var scope = fixture.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<InstitutionalImportService>();
        var input = new ImportUpload(Guid.NewGuid(), "TEST", new("id", "email", "program", "term", "plan"), new[] {
            new Dictionary<string, string?> { ["id"] = "EX1", ["email"] = "missing@example.test", ["program"] = Guid.NewGuid().ToString(), ["term"] = setup.Term.ToString(), ["plan"] = Guid.NewGuid().ToString() } });
        var job = await service.Upload(setup.Creator, setup.Inst, input, default);
        Assert.Equal(job.Job.Id, (await service.Upload(setup.Creator, setup.Inst, input, default)).Job.Id);
        Assert.Equal("Invalid", Assert.Single((await service.DryRun(setup.Creator, setup.Inst, job.Job.Id, default)).Rows).Status);
        Assert.Equal(409, (await Assert.ThrowsAsync<ApiFault>(() => service.Approve(setup.Reviewer, setup.Inst, job.Job.Id, default))).StatusCode);
        Assert.Equal(409, (await Assert.ThrowsAsync<ApiFault>(() => service.Apply(setup.Creator, setup.Inst, job.Job.Id, default))).StatusCode);
    }
    [Fact]
    public async Task ImportAppliesThroughAcademicServicesAndConcurrentResumeDoesNotDuplicate()
    {
        var setup = await Setup();
        Guid programId; Guid planId; string email;
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AcademixDbContext>();
            var department = new Department { InstitutionId = setup.Inst, Code = "D", Name = "Department" };
            var program = new AcademicProgram { InstitutionId = setup.Inst, DepartmentId = department.Id, Code = "P", Name = "Program" };
            var plan = new Academix.Api.Domain.Curriculum.CurriculumPlan { InstitutionId = setup.Inst, ProgramId = program.Id, CatalogYear = 2026,
                Revision = 1, Name = "Published test plan", Status = "Published", PublishedAtUtc = DateTime.UtcNow };
            db.Departments.Add(department); db.AcademicPrograms.Add(program); db.CurriculumPlans.Add(plan);
            await db.SaveChangesAsync();
            programId = program.Id; planId = plan.Id;
            email = (await db.Users.SingleAsync(x => x.Id == setup.StudentId)).Email!;
        }
        var input = new ImportUpload(Guid.NewGuid(), "LEGACY", new("id", "email", "program", "term", "plan"), new[] {
            new Dictionary<string, string?> { ["id"] = "STU-1", ["email"] = email, ["program"] = programId.ToString(), ["term"] = setup.Term.ToString(), ["plan"] = planId.ToString() } });
        Guid jobId;
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<InstitutionalImportService>();
            jobId = (await service.Upload(setup.Creator, setup.Inst, input, default)).Job.Id;
            Assert.Equal("Valid", Assert.Single((await service.DryRun(setup.Creator, setup.Inst, jobId, default)).Rows).Status);
            Assert.Equal(403, (await Assert.ThrowsAsync<ApiFault>(() => service.Approve(setup.Creator, setup.Inst, jobId, default))).StatusCode);
            await service.Approve(setup.Reviewer, setup.Inst, jobId, default);
        }
        async Task Apply()
        {
            await using var scope = fixture.Services.CreateAsyncScope();
            var result = await scope.ServiceProvider.GetRequiredService<InstitutionalImportService>().Apply(setup.Creator, setup.Inst, jobId, default);
            Assert.Equal(1, result.Job.AppliedRows); Assert.Equal(0, result.Job.FailedRows);
        }
        await Task.WhenAll(Apply(), Apply());
        await Apply();
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AcademixDbContext>();
            Assert.Equal(1, await db.ExternalIdentityMaps.CountAsync(x => x.InstitutionId == setup.Inst));
            Assert.Equal(1, await db.InstitutionalOutboxEvents.CountAsync(x => x.InstitutionId == setup.Inst));
            Assert.Equal(1, await db.StudentProgramMemberships.CountAsync(x => x.StudentId == setup.StudentId));
            Assert.Equal(1, await db.StudentCurriculumAssignments.CountAsync(x => x.StudentId == setup.StudentId));
        }
    }

    [Fact]
    public async Task PostgreSqlRejectsPublishedPolicyMutationAndCrossInstitutionHold()
    {
        var setup = await Setup(); var foreign = await Setup();
        await using var scope = fixture.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<AcademicPolicyService>();
        var policy = await service.Draft(setup.Creator, setup.Inst, Draft(), default);
        await service.Publish(setup.Reviewer, setup.Inst, policy.Id, default);
        var db = scope.ServiceProvider.GetRequiredService<AcademixDbContext>();
        var immutable = await Assert.ThrowsAsync<Npgsql.PostgresException>(() => db.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE registrar_policy_revisions SET \"RulesJson\" = {"{}"}::jsonb WHERE \"Id\" = {policy.Id}"));
        Assert.Equal("23514", immutable.SqlState);
        db.StudentAcademicHolds.Add(new Academix.Api.Domain.Registrar.StudentAcademicHold { InstitutionId = setup.Inst, StudentId = foreign.StudentId,
            CreatedBy = Guid.Parse(setup.Creator.FindFirstValue(ClaimTypes.NameIdentifier)!), Reason = "Invalid tenant fixture" });
        var crossTenant = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.Equal("23514", Assert.IsType<Npgsql.PostgresException>(crossTenant.InnerException).SqlState);
    }

}
