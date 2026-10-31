using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Academix.Api.Data;
using Academix.Api.Domain.Academic;
using Academix.Api.Domain.Assessment;
using Academix.Api.Domain.Courses;
using Academix.Api.Domain.Identity;
using Academix.Api.DTOs.Academic;
using Academix.Api.DTOs.Identity;
using Academix.Api.DTOs.Records;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Academix.Tests.Integration;

// Requires real PostgreSQL/Testcontainers. Not executed by the ZIP installer.
public sealed class Phase14AcademicRecordsTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private const string Password = "Records!TestPassword2026";

    private async Task<(Guid Id, string Token)> User(Guid institution, string role)
    {
        var id = Guid.NewGuid(); var email = $"rec-{id:N}@example.test";
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser { Id = id, InstitutionId = institution, UserName = email, Email = email };
            Assert.True((await manager.CreateAsync(user, Password)).Succeeded);
            Assert.True((await manager.AddToRoleAsync(user, role)).Succeeded);
        }
        using var login = await fixture.Client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, Password));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var token = (await login.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken;
        return (id, token);
    }

    private async Task<HttpResponseMessage> Call(HttpMethod method, string path, string token, object? data = null)
    {
        using var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (data is not null) request.Content = JsonContent.Create(data);
        return await fixture.Client.SendAsync(request);
    }

    [Fact]
    public async Task Prerequisites_Enrollment_Transcript_Standing_RespectTenantBoundaries()
    {
        var institution = Guid.NewGuid(); var term = Guid.NewGuid();
        var requiredCourse = Guid.NewGuid(); var targetCourse = Guid.NewGuid();
        var completedSection = Guid.NewGuid(); var targetSection = Guid.NewGuid();
        var department = Guid.NewGuid(); var program = Guid.NewGuid();
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AcademixDbContext>();
            db.Institutions.Add(new Institution { Id = institution, Code = "RC" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(), Name = "Records school" });
            db.Departments.Add(new Department { Id = department, InstitutionId = institution, Code = "DEPT", Name = "Department" });
            db.AcademicPrograms.Add(new AcademicProgram { Id = program, InstitutionId = institution, DepartmentId = department, Code = "PLAN", Name = "Academic program" });
            db.Courses.Add(Course.Create(requiredCourse, institution, "RC101", "Foundations"));
            db.Courses.Add(Course.Create(targetCourse, institution, "RC201", "Advanced topics"));
            db.AcademicTerms.Add(new AcademicTerm { Id = term, InstitutionId = institution, Code = "RC1", Name = "Academic term",
                StartsOn = new DateOnly(2026, 1, 1), EndsOn = new DateOnly(2027, 5, 1), Status = "Active" });
            db.CourseSections.Add(new CourseSection { Id = completedSection, InstitutionId = institution,
                CourseId = requiredCourse, TermId = term, Code = "S01", Capacity = 30, Status = "Closed" });
            db.CourseSections.Add(new CourseSection { Id = targetSection, InstitutionId = institution,
                CourseId = targetCourse, TermId = term, Code = "S02", Capacity = 30, Status = "Draft" });
            await db.SaveChangesAsync();
        }
        var (student, studentToken) = await User(institution, AcademixRoles.Student);
        var (admin, adminToken) = await User(institution, AcademixRoles.InstitutionAdministrator);
        var otherInst = Guid.NewGuid();
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AcademixDbContext>();
            db.Institutions.Add(new Institution { Id = otherInst,
                Code = "OT" + Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(), Name = "Other school" });
            await db.SaveChangesAsync();
        }
        var (_, foreignAdmin) = await User(otherInst, AcademixRoles.InstitutionAdministrator);
        using (var denied = await Call(HttpMethod.Post, $"/api/records/courses/{targetCourse}/prerequisites", studentToken,
            new AddPrerequisiteRequest(requiredCourse, 65m)))
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        using (var denied = await Call(HttpMethod.Post, $"/api/records/courses/{targetCourse}/prerequisites", foreignAdmin,
            new AddPrerequisiteRequest(requiredCourse, 65m)))
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        using (var result = await Call(HttpMethod.Post, $"/api/records/courses/{targetCourse}/prerequisites", adminToken,
            new AddPrerequisiteRequest(requiredCourse, 65m)))
            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        // Set up the prerequisite BEFORE opening the target section.
        // Once open, RecordsService correctly forbids changing prerequisite policy.
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AcademixDbContext>();
            var target = await db.CourseSections.SingleAsync(x => x.Id == targetSection);
            target.Status = "Open";
            target.Version++;
            await db.SaveChangesAsync();
        }
        using (var result = await Call(HttpMethod.Post, $"/api/records/programs/{program}/students/admit", adminToken,
            new AdmitStudent(student, term)))
            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        using (var eligibility = await Call(HttpMethod.Get,
            $"/api/records/sections/{targetSection}/students/{student}/eligibility", studentToken))
        {
            Assert.Equal(HttpStatusCode.OK, eligibility.StatusCode);
            Assert.False((await eligibility.Content.ReadFromJsonAsync<EnrollmentEligibility>())!.Eligible);
        }
        using (var rejected = await Call(HttpMethod.Post, $"/api/academic/sections/{targetSection}/enrollments", adminToken,
            new EnrollmentCreate(student)))
            Assert.Equal(HttpStatusCode.Conflict, rejected.StatusCode);
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AcademixDbContext>();
            db.SectionGradePolicies.Add(new SectionGradePolicy { SectionId = completedSection,
                InstitutionId = institution, PassingPercentage = 50m, PublishedAtUtc = DateTime.UtcNow, UpdatedBy = admin });
            db.GradebookResults.Add(new GradebookResult { InstitutionId = institution,
                SectionId = completedSection, StudentId = student, Percentage = 82m,
                Passed = true, Letter = "B" });
            await db.SaveChangesAsync();
        }
        using (var accepted = await Call(HttpMethod.Post, $"/api/academic/sections/{targetSection}/enrollments", adminToken,
            new EnrollmentCreate(student)))
            Assert.Equal(HttpStatusCode.Created, accepted.StatusCode);
        using (var sync = await Call(HttpMethod.Post, $"/api/records/sections/{completedSection}/transcript/sync", adminToken))
        {
            Assert.Equal(HttpStatusCode.OK, sync.StatusCode);
            Assert.Equal(1, (await sync.Content.ReadFromJsonAsync<TranscriptSyncResult>())!.Inserted);
        }
        using (var mine = await Call(HttpMethod.Get, $"/api/records/students/{student}/transcript", studentToken))
        {
            Assert.Equal(HttpStatusCode.OK, mine.StatusCode);
            Assert.Single((await mine.Content.ReadFromJsonAsync<TranscriptView>())!.Entries);
        }
        using (var standing = await Call(HttpMethod.Post,
            $"/api/records/terms/{term}/students/{student}/standing/recalculate", adminToken))
        {
            Assert.Equal(HttpStatusCode.OK, standing.StatusCode);
            Assert.Equal("GoodStanding", (await standing.Content.ReadFromJsonAsync<StandingView>())!.Status);
        }
        using (var denied = await Call(HttpMethod.Get, $"/api/records/students/{student}/transcript", foreignAdmin))
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }
}
