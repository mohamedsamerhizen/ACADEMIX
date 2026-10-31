using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using Academix.Api.Data;
using Academix.Api.Domain.Academic;
using Academix.Api.Domain.Courses;
using Academix.Api.Domain.Identity;
using Academix.Api.DTOs.Identity;
using Academix.Api.Middleware;
using Academix.Api.Services.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Academix.Tests.Integration;

public sealed class TeachingRoleRevocationTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private async Task<(Guid InstitutionId, Guid SectionId)> CreateSectionAsync()
    {
        var institutionId = Guid.NewGuid();
        var sectionId = Guid.NewGuid();
        var termId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var tag = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();

        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AcademixDbContext>();
        db.Institutions.Add(new Institution { Id = institutionId, Code = $"U{tag}", Name = "Role Revocation Test University" });
        db.Courses.Add(Course.Create(courseId, institutionId, "RL99", "Role revocation test"));
        db.AcademicTerms.Add(new AcademicTerm
        {
            Id = termId, InstitutionId = institutionId, Code = $"T{tag}", Name = "Role Test Term",
            Status = "Active", StartsOn = new DateOnly(2026, 10, 1), EndsOn = new DateOnly(2027, 7, 1)
        });
        db.CourseSections.Add(new CourseSection
        {
            Id = sectionId, InstitutionId = institutionId, CourseId = courseId,
            TermId = termId, Code = $"S{tag}", Status = "Open", Capacity = 10
        });
        await db.SaveChangesAsync();
        return (institutionId, sectionId);
    }

    private async Task<(Guid Id, string Token)> CreateTeacherAsync(Guid institutionId, string role)
    {
        var userId = Guid.NewGuid();
        var email = $"staff-{Guid.NewGuid():N}@example.test";
        const string password = "Strong!TeacherPassword2026";
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser { Id = userId, Email = email, UserName = email, InstitutionId = institutionId };
            Assert.True((await users.CreateAsync(user, password)).Succeeded);
            Assert.True((await users.AddToRoleAsync(user, role)).Succeeded);
        }
        using var login = await fixture.Client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var tokens = await login.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(tokens);
        return (userId, tokens.AccessToken);
    }

    private async Task<HttpResponseMessage> GetContentAsync(Guid sectionId, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"/api/lms/sections/{sectionId}/content");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await fixture.Client.SendAsync(request);
    }

    [Fact]
    public async Task RevokedInstructorRoleBlocksTeachingAccessDespiteStaleAssignment()
    {
        var (institutionId, sectionId) = await CreateSectionAsync();
        var (userId, token) = await CreateTeacherAsync(institutionId, AcademixRoles.Instructor);
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AcademixDbContext>();
            db.TeachingAssignments.Add(new TeachingAssignment
            {
                InstitutionId = institutionId, SectionId = sectionId, UserId = userId, Kind = AcademixRoles.Instructor
            });
            await db.SaveChangesAsync();
        }

        using (var permitted = await GetContentAsync(sectionId, token))
            Assert.Equal(HttpStatusCode.OK, permitted.StatusCode);

        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var teacher = await users.FindByIdAsync(userId.ToString());
            Assert.NotNull(teacher);
            Assert.True((await users.RemoveFromRoleAsync(teacher, AcademixRoles.Instructor)).Succeeded);
        }

        // Verify the underlying service directly, even when a role-change security stamp
        // makes an old JWT invalid at middleware level. This catches the stale-assignment bug.
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var security = scope.ServiceProvider.GetRequiredService<IdentitySecurity>();
            var actor = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim("institution_id", institutionId.ToString())
            ], "RoleRevocationRegression"));
            var failure = await Assert.ThrowsAsync<ApiFault>(() =>
                security.RequireSectionStaffAsync(actor, institutionId, sectionId, CancellationToken.None));
            Assert.Equal(403, failure.StatusCode);
        }

        // A stale access token may still have old role claims. It must not restore staff access.
        using var denied = await GetContentAsync(sectionId, token);
        Assert.True(denied.StatusCode is HttpStatusCode.Forbidden or HttpStatusCode.Unauthorized,
            $"Expected role revocation to deny the request, received {(int)denied.StatusCode}.");
    }

    [Fact]
    public async Task AssignmentOfDifferentTeachingKindDoesNotConferStaffPrivileges()
    {
        var (institutionId, sectionId) = await CreateSectionAsync();
        var (userId, token) = await CreateTeacherAsync(institutionId, AcademixRoles.TeachingAssistant);
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AcademixDbContext>();
            // Deliberately inconsistent legacy assignment: TA role but Instructor assignment.
            db.TeachingAssignments.Add(new TeachingAssignment
            {
                InstitutionId = institutionId, SectionId = sectionId, UserId = userId, Kind = AcademixRoles.Instructor
            });
            await db.SaveChangesAsync();
        }
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var security = scope.ServiceProvider.GetRequiredService<IdentitySecurity>();
            var actor = new ClaimsPrincipal(new ClaimsIdentity(
            [
                new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
                new Claim("institution_id", institutionId.ToString())
            ], "TeachingKindRegression"));
            var failure = await Assert.ThrowsAsync<ApiFault>(() =>
                security.RequireSectionStaffAsync(actor, institutionId, sectionId, CancellationToken.None));
            Assert.Equal(403, failure.StatusCode);
        }
        using var denied = await GetContentAsync(sectionId, token);
        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }
}
