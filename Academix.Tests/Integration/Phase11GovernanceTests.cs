using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Academix.Api.Data;
using Academix.Api.Domain.Academic;
using Academix.Api.Domain.Courses;
using Academix.Api.Domain.Identity;
using Academix.Api.DTOs.Governance;
using Academix.Api.DTOs.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Academix.Tests.Integration;

// Requires PostgreSQL/Testcontainers and migrations Phase 1–11 applied.
public sealed class Phase11GovernanceTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private const string Password = "Valid!TestPassword2026";

    private async Task<(Guid Id, string Token)> User(Guid institutionId, string role)
    {
        var id = Guid.NewGuid(); var email = $"gov-{Guid.NewGuid():N}@example.test";
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser { Id = id, Email = email, UserName = email, InstitutionId = institutionId };
            Assert.True((await manager.CreateAsync(user, Password)).Succeeded);
            Assert.True((await manager.AddToRoleAsync(user, role)).Succeeded);
        }
        using var login = await fixture.Client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, Password));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        return (id, (await login.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken);
    }

    private async Task<(Guid Institution, Guid Section)> Seed(int capacity = 5)
    {
        var inst = Guid.NewGuid(); var section = Guid.NewGuid(); var course = Guid.NewGuid(); var term = Guid.NewGuid();
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AcademixDbContext>();
        db.Institutions.Add(new Institution { Id = inst, Code = "GV" + Guid.NewGuid().ToString("N")[..8], Name = "Governance Institute" });
        db.Courses.Add(Course.Create(course, inst, "GV101", "Governance"));
        db.AcademicTerms.Add(new AcademicTerm { Id = term, InstitutionId = inst, Code = "FALL", Name = "Fall",
            StartsOn = new DateOnly(2026, 1, 1), EndsOn = new DateOnly(2027, 1, 1), Status = "Active" });
        db.CourseSections.Add(new CourseSection { Id = section, InstitutionId = inst, CourseId = course,
            TermId = term, Code = "S1", Status = "Open", Capacity = capacity });
        await db.SaveChangesAsync();
        return (inst, section);
    }

    private async Task<HttpResponseMessage> Send(HttpMethod method, string url, string token, object? body = null)
    {
        using var message = new HttpRequestMessage(method, url);
        message.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null) message.Content = JsonContent.Create(body);
        return await fixture.Client.SendAsync(message);
    }

    [Fact]
    public async Task StaffApprovalRequiresIndependentAdministratorAndRejectsRoleEscalation()
    {
        var (institution, _) = await Seed();
        var requester = await User(institution, AcademixRoles.InstitutionAdministrator);
        var reviewer = await User(institution, AcademixRoles.InstitutionAdministrator);
        var target = await User(institution, AcademixRoles.Student);
        var outsiderInstitution = (await Seed()).Institution;
        var outsider = await User(outsiderInstitution, AcademixRoles.InstitutionAdministrator);
        var url = $"/api/governance/institutions/{institution}/staff-requests";
        var command = new CreateStaffChange(Guid.NewGuid(), target.Id, "AddRole", AcademixRoles.Instructor, "Hiring verification completed");
        using var create = await Send(HttpMethod.Post, url, requester.Token, command);
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var request = (await create.Content.ReadFromJsonAsync<StaffChangeView>())!;
        using var duplicate = await Send(HttpMethod.Post, url, requester.Token, command);
        Assert.Equal(request.Id, (await duplicate.Content.ReadFromJsonAsync<StaffChangeView>())!.Id);
        using var escalation = await Send(HttpMethod.Post, url, requester.Token,
            new CreateStaffChange(Guid.NewGuid(), target.Id, "AddRole", AcademixRoles.InstitutionAdministrator, "escalation"));
        Assert.Equal(HttpStatusCode.BadRequest, escalation.StatusCode);
        using var stranger = await Send(HttpMethod.Get, $"{url}/{request.Id}", outsider.Token);
        Assert.NotEqual(HttpStatusCode.OK, stranger.StatusCode);
        using var selfReview = await Send(HttpMethod.Post, $"{url}/{request.Id}/approve", requester.Token,
            new DecideStaffChange(request.Version, "I want to approve"));
        Assert.Equal(HttpStatusCode.Forbidden, selfReview.StatusCode);
        using var approve = await Send(HttpMethod.Post, $"{url}/{request.Id}/approve", reviewer.Token,
            new DecideStaffChange(request.Version, "Verified by second administrator"));
        Assert.Equal(HttpStatusCode.OK, approve.StatusCode);
        Assert.Equal("Approved", (await approve.Content.ReadFromJsonAsync<StaffChangeView>())!.Status);
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = (await manager.FindByIdAsync(target.Id.ToString()))!;
            Assert.True(await manager.IsInRoleAsync(user, AcademixRoles.Instructor));
        }
        using var replay = await Send(HttpMethod.Post, $"{url}/{request.Id}/approve", reviewer.Token,
            new DecideStaffChange(request.Version, "Repeated approval"));
        Assert.Equal(HttpStatusCode.Conflict, replay.StatusCode);
    }

    [Fact]
    public async Task EnrollmentBatchesPreviewCommitIdempotentlyAndRespectCapacity()
    {
        var (institution, section) = await Seed(capacity: 2);
        var admin = await User(institution, AcademixRoles.InstitutionAdministrator);
        var student1 = await User(institution, AcademixRoles.Student);
        var student2 = await User(institution, AcademixRoles.Student);
        var prefix = $"/api/governance/institutions/{institution}/enrollment-batches";
        var request = new CreateEnrollmentBatch(Guid.NewGuid(), section, [student1.Id, student2.Id]);
        using var create = await Send(HttpMethod.Post, prefix, admin.Token, request);
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var batch = (await create.Content.ReadFromJsonAsync<EnrollmentBatchView>())!;
        using var preview = await Send(HttpMethod.Get, $"{prefix}/{batch.Id}/preview", admin.Token);
        Assert.True((await preview.Content.ReadFromJsonAsync<BatchPreview>())!.Ready);
        using var commit = await Send(HttpMethod.Post, $"{prefix}/{batch.Id}/commit", admin.Token);
        Assert.Equal(HttpStatusCode.OK, commit.StatusCode);
        var committed = (await commit.Content.ReadFromJsonAsync<EnrollmentBatchView>())!;
        Assert.Equal("Committed", committed.Status);
        Assert.All(committed.Students, item => Assert.Equal("Enrolled", item.Result));
        using var retry = await Send(HttpMethod.Post, $"{prefix}/{batch.Id}/commit", admin.Token);
        Assert.Equal(HttpStatusCode.OK, retry.StatusCode);
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AcademixDbContext>();
            Assert.Equal(2, await db.StudentEnrollments.CountAsync(x => x.SectionId == section && x.Status == "Active"));
        }
        var extra = await User(institution, AcademixRoles.Student);
        using var createFull = await Send(HttpMethod.Post, prefix, admin.Token,
            new CreateEnrollmentBatch(Guid.NewGuid(), section, [extra.Id]));
        var full = (await createFull.Content.ReadFromJsonAsync<EnrollmentBatchView>())!;
        using var fullPreview = await Send(HttpMethod.Get, $"{prefix}/{full.Id}/preview", admin.Token);
        Assert.False((await fullPreview.Content.ReadFromJsonAsync<BatchPreview>())!.Ready);
        using var rejectedCommit = await Send(HttpMethod.Post, $"{prefix}/{full.Id}/commit", admin.Token);
        Assert.Equal(HttpStatusCode.Conflict, rejectedCommit.StatusCode);
    }
}
