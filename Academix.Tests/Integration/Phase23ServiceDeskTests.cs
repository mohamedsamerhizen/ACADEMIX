using System.Security.Claims;
using System.Text;
using Academix.Api.Data;
using Academix.Api.Domain.Academic;
using Academix.Api.Domain.Identity;
using Academix.Api.DTOs.StudentServices;
using Academix.Api.Middleware;
using Academix.Api.Services.StudentServices;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Academix.Tests.Integration;

/// <summary>Actual PostgreSQL and ASP.NET Identity verification; deliberately not executed with Docker disabled.</summary>
public sealed class Phase23ServiceDeskTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private static ClaimsPrincipal Principal(Guid user, Guid institution)
        => new(new ClaimsIdentity([new(ClaimTypes.NameIdentifier, user.ToString()),
            new("institution_id", institution.ToString())], "test"));

    private async Task<(Guid Institution, ClaimsPrincipal Admin, ClaimsPrincipal Staff, ClaimsPrincipal Student)> Setup()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AcademixDbContext>();
        var inst = new Institution { Code = Guid.NewGuid().ToString("N")[..12], Name = "Service desk test" };
        db.Institutions.Add(inst); await db.SaveChangesAsync();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        async Task<ApplicationUser> Create(string role)
        {
            var mail = Guid.NewGuid().ToString("N") + "@example.test";
            var user = new ApplicationUser { InstitutionId = inst.Id, UserName = mail, Email = mail };
            Assert.True((await users.CreateAsync(user, "Safe!ServiceDesk2026")).Succeeded);
            Assert.True((await users.AddToRoleAsync(user, role)).Succeeded);
            return user;
        }
        var admin = await Create(AcademixRoles.InstitutionAdministrator);
        var instructor = await Create(AcademixRoles.Instructor);
        var student = await Create(AcademixRoles.Student);
        return (inst.Id, Principal(admin.Id, inst.Id), Principal(instructor.Id, inst.Id), Principal(student.Id, inst.Id));
    }

    [Fact]
    public async Task GrantsProtectAssignmentAndRevocationInvalidatesCurrentStaffAccess()
    {
        var (inst, admin, staff, student) = await Setup();
        await using var scope = fixture.Services.CreateAsyncScope();
        var svc = scope.ServiceProvider.GetRequiredService<StudentServiceCaseService>();
        var definition = await svc.SaveDefinition(admin, inst, null,
            new("ITSupport", "IT Support", "Helpdesk", 24, true), default);
        var studentCase = await svc.Submit(student, new(definition.Id, Guid.NewGuid(), "Account", "Access problem"), default);
        var staffId = Guid.Parse(staff.FindFirst(ClaimTypes.NameIdentifier)!.Value);
        await Assert.ThrowsAsync<ApiFault>(() => svc.StaffTimeline(staff, inst, studentCase.Id, default));
        var grant = await svc.SetStaffGrant(admin, inst, staffId, "ITSupport", new(true), default);
        var assigned = await svc.Assign(admin, inst, studentCase.Id,
            new(staffId, studentCase.Version, "Helpdesk owner"), default);
        var started = await svc.Act(staff, inst, studentCase.Id, new("Start", assigned.Version, "Under review"), false, default);
        Assert.Equal("InReview", started.Status);
        await svc.SetStaffGrant(admin, inst, staffId, "ITSupport", new(false, grant.Version), default);
        await Assert.ThrowsAsync<ApiFault>(() => svc.StaffTimeline(staff, inst, studentCase.Id, default));
        var after = (await svc.Queue(admin, inst, null, "ITSupport", 1, default)).Single(x => x.Id == studentCase.Id);
        Assert.Null(after.AssignedStaffId);
    }

    [Fact]
    public async Task ReferralAndSlaReportFollowCurrentQueueWithoutChangingOriginalServiceDefinition()
    {
        var (inst, admin, _, student) = await Setup();
        await using var scope = fixture.Services.CreateAsyncScope();
        var svc = scope.ServiceProvider.GetRequiredService<StudentServiceCaseService>();
        var def = await svc.SaveDefinition(admin, inst, null,
            new("StudentAffairs", "Student affairs", "General issue", 24, true), default);
        var item = await svc.Submit(student, new(def.Id, Guid.NewGuid(), "Issue", "Details"), default);
        var moved = await svc.Refer(admin, inst, item.Id,
            new("Library", item.Version, "Library needs to review"), default);
        Assert.Equal(def.Id, moved.DefinitionId);
        Assert.Equal("Library", moved.CurrentQueueCode);
        Assert.Single(await svc.Referrals(admin, inst, item.Id, default));
        var report = await svc.Report(admin, inst, default);
        Assert.Contains(report.Queues, x => x.Queue == "Library" && x.Open == 1);
        Assert.Empty(await svc.Queue(admin, inst, null, "StudentAffairs", 1, default));
    }

    [Fact]
    public async Task ForeignInstitutionCannotReadOrMutateAnotherServiceQueue()
    {
        var (inst, admin, _, student) = await Setup();
        var (otherInst, otherAdmin, _, _) = await Setup();
        await using var scope = fixture.Services.CreateAsyncScope();
        var svc = scope.ServiceProvider.GetRequiredService<StudentServiceCaseService>();
        var def = await svc.SaveDefinition(admin, inst, null,
            new("Library", "Library assistance", "Library", 24, true), default);
        var item = await svc.Submit(student, new(def.Id, Guid.NewGuid(), "Borrowing", "Account issue"), default);
        await Assert.ThrowsAsync<ApiFault>(() => svc.StaffTimeline(otherAdmin, inst, item.Id, default));
        await Assert.ThrowsAsync<ApiFault>(() => svc.Queue(admin, otherInst, null, null, 1, default));
    }

    [Fact]
    public async Task StaffOnlyAttachmentAndNotesCannotBeReadByStudent()
    {
        var (inst, admin, _, student) = await Setup();
        await using var scope = fixture.Services.CreateAsyncScope();
        var svc = scope.ServiceProvider.GetRequiredService<StudentServiceCaseService>();
        var def = await svc.SaveDefinition(admin, inst, null,
            new("GeneralSupport", "Help", "General", 72, true), default);
        var item = await svc.Submit(student, new(def.Id, Guid.NewGuid(), "Request", "Details"), default);
        _ = await svc.Act(admin, inst, item.Id,
            new("Start", item.Version, "We are reviewing", "Confidential processing note"), false, default);
        var bytes = Encoding.ASCII.GetBytes("%PDF-1.7\nEvidence\n");
        using var input = new MemoryStream(bytes);
        var form = new FormFile(input, 0, bytes.Length, "file", "evidence.pdf")
        { Headers = new HeaderDictionary(), ContentType = "application/pdf" };
        var meta = await svc.Upload(admin, inst, item.Id, form, "StaffOnly", false, default);
        Assert.Single(await svc.Attachments(admin, inst, item.Id, false, default));
        Assert.Empty(await svc.Attachments(student, inst, item.Id, true, default));
        await Assert.ThrowsAsync<ApiFault>(() => svc.Download(student, inst, item.Id, meta.Id, true, default));
        var timeline = await svc.StudentTimeline(student, item.Id, default);
        Assert.DoesNotContain(timeline, x => x.PublicMessage.Contains("Confidential", StringComparison.Ordinal));
        var staffNotes = await svc.StaffTimeline(admin, inst, item.Id, default);
        Assert.Contains(staffNotes, x => x.PrivateStaffNote == "Confidential processing note");
    }
}
