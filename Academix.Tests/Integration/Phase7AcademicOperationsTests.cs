using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Academix.Api.Data;
using Academix.Api.Domain.Academic;
using Academix.Api.Domain.Courses;
using Academix.Api.Domain.Identity;
using Academix.Api.DTOs.AcademicOperations;
using Academix.Api.DTOs.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Academix.Tests.Integration;

// Requires PostgreSQL/Testcontainers. Not included in Docker-independent verification.
public sealed class Phase7AcademicOperationsTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private const string Password = "Excellent!AcademicOperations2026";
    private async Task<(Guid Id, string Token)> User(Guid inst, string role)
    {
        var id = Guid.NewGuid();
        var email = $"op-{Guid.NewGuid():N}@example.test";
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var mgr = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var account = new ApplicationUser { Id = id, Email = email, UserName = email, InstitutionId = inst };
            Assert.True((await mgr.CreateAsync(account, Password)).Succeeded);
            Assert.True((await mgr.AddToRoleAsync(account, role)).Succeeded);
        }
        using var response = await fixture.Client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, Password));
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (id, (await response.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken);
    }

    private async Task<HttpResponseMessage> Send(HttpMethod method, string url, string token, object? data = null)
    {
        using var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (data is not null) request.Content = JsonContent.Create(data);
        return await fixture.Client.SendAsync(request);
    }

    private async Task<(Guid Institution, Guid Section, Guid ForeignInstitution, Guid ForeignSection)> Seed()
    {
        var a = Guid.NewGuid(); var b = Guid.NewGuid();
        var aSection = Guid.NewGuid(); var bSection = Guid.NewGuid();
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AcademixDbContext>();
        db.Institutions.AddRange(new Institution { Id = a, Code = "O" + Guid.NewGuid().ToString("N")[..10], Name = "Operations A" },
            new Institution { Id = b, Code = "O" + Guid.NewGuid().ToString("N")[..10], Name = "Operations B" });
        var ca = Guid.NewGuid(); var cb = Guid.NewGuid();
        var ta = Guid.NewGuid(); var tb = Guid.NewGuid();
        db.Courses.AddRange(Course.Create(ca, a, "OPS101", "Operations"), Course.Create(cb, b, "OPS101", "Other"));
        db.AcademicTerms.AddRange(
            new AcademicTerm { Id = ta, InstitutionId = a, Code = "T1", Name = "Term 1", StartsOn = new DateOnly(2026, 1, 1), EndsOn = new DateOnly(2027, 1, 1) },
            new AcademicTerm { Id = tb, InstitutionId = b, Code = "T1", Name = "Term 1", StartsOn = new DateOnly(2026, 1, 1), EndsOn = new DateOnly(2027, 1, 1) });
        db.CourseSections.AddRange(
            new CourseSection { Id = aSection, InstitutionId = a, CourseId = ca, TermId = ta, Code = "A", Capacity = 30, Status = "Open" },
            new CourseSection { Id = bSection, InstitutionId = b, CourseId = cb, TermId = tb, Code = "A", Capacity = 30, Status = "Open" });
        await db.SaveChangesAsync();
        return (a, aSection, b, bSection);
    }

    [Fact]
    public async Task StudentCannotReadForeignAttendanceAndAdminCannotAccessOtherTenant()
    {
        var x = await Seed();
        var student = await User(x.Institution, AcademixRoles.Student);
        var admin = await User(x.Institution, AcademixRoles.InstitutionAdministrator);
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AcademixDbContext>();
            db.StudentEnrollments.Add(new StudentEnrollment { InstitutionId = x.Institution,
                SectionId = x.Section, StudentId = student.Id, Status = "Active" });
            await db.SaveChangesAsync();
        }
        using (var own = await Send(HttpMethod.Get,
            $"/api/academic-operations/student/me/sections/{x.Section}/attendance", student.Token))
            Assert.Equal(HttpStatusCode.OK, own.StatusCode);
        using (var foreign = await Send(HttpMethod.Get,
            $"/api/academic-operations/student/me/sections/{x.ForeignSection}/attendance", student.Token))
            Assert.Equal(HttpStatusCode.NotFound, foreign.StatusCode);
        using (var denied = await Send(HttpMethod.Get,
            $"/api/academic-operations/institutions/{x.ForeignInstitution}/requests", admin.Token))
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
    }

    [Fact]
    public async Task StudentRequestAdministrativeDecisionAndHistory()
    {
        var x = await Seed();
        var student = await User(x.Institution, AcademixRoles.Student);
        var admin = await User(x.Institution, AcademixRoles.InstitutionAdministrator);
        Guid requestId;
        using (var created = await Send(HttpMethod.Post, "/api/academic-operations/student/me/requests",
            student.Token, new AcademicRequestCreate(null, "General", "Academic certificate", "I request official confirmation of my enrollment status.")))
        {
            Assert.Equal(HttpStatusCode.OK, created.StatusCode);
            requestId = (await created.Content.ReadFromJsonAsync<AcademicRequestView>())!.Id;
        }
        using (var unauthorized = await Send(HttpMethod.Post,
            $"/api/academic-operations/institutions/{x.Institution}/requests/{requestId}/decision",
            student.Token, new AcademicRequestDecision("Approved", "Verified identity", 1)))
            Assert.Equal(HttpStatusCode.Forbidden, unauthorized.StatusCode);
        using (var approved = await Send(HttpMethod.Post,
            $"/api/academic-operations/institutions/{x.Institution}/requests/{requestId}/decision",
            admin.Token, new AcademicRequestDecision("Approved", "All supporting records verified", 1)))
        {
            Assert.Equal(HttpStatusCode.OK, approved.StatusCode);
            Assert.Equal("Approved", (await approved.Content.ReadFromJsonAsync<AcademicRequestView>())!.Status);
        }
        using (var repeated = await Send(HttpMethod.Post,
            $"/api/academic-operations/institutions/{x.Institution}/requests/{requestId}/decision",
            admin.Token, new AcademicRequestDecision("Rejected", "Already approved", 1)))
            Assert.Equal(HttpStatusCode.Conflict, repeated.StatusCode);
        using (var history = await Send(HttpMethod.Get,
            $"/api/academic-operations/institutions/{x.Institution}/requests/{requestId}/history", admin.Token))
        {
            Assert.Equal(HttpStatusCode.OK, history.StatusCode);
            Assert.Single((await history.Content.ReadFromJsonAsync<List<AcademicRequestAudit>>())!);
        }
    }


    [Fact]
    public async Task StaffAttendanceIsScopedToActiveSectionEnrollment()
    {
        var x = await Seed();
        var teacher = await User(x.Institution, AcademixRoles.Instructor);
        var student = await User(x.Institution, AcademixRoles.Student);
        var other = await User(x.ForeignInstitution, AcademixRoles.Student);
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AcademixDbContext>();
            db.TeachingAssignments.Add(new TeachingAssignment { InstitutionId = x.Institution,
                SectionId = x.Section, UserId = teacher.Id, Kind = "Instructor" });
            db.StudentEnrollments.Add(new StudentEnrollment { InstitutionId = x.Institution,
                SectionId = x.Section, StudentId = student.Id, Status = "Active" });
            await db.SaveChangesAsync();
        }
        var begins = DateTime.UtcNow.AddHours(-2);
        Guid sessionId;
        using (var created = await Send(HttpMethod.Post,
            $"/api/academic-operations/sections/{x.Section}/sessions", teacher.Token,
            new SessionCreate(begins, begins.AddHours(1), "Introduction to Academic Operations")))
        {
            Assert.Equal(HttpStatusCode.OK, created.StatusCode);
            sessionId = (await created.Content.ReadFromJsonAsync<SessionView>())!.Id;
        }
        using (var foreignStudent = await Send(HttpMethod.Put,
            $"/api/academic-operations/sessions/{sessionId}/attendance", teacher.Token,
            new AttendanceBatch([new AttendanceUpsert(other.Id, "Present", "")])) )
            Assert.Equal(HttpStatusCode.BadRequest, foreignStudent.StatusCode);
        using (var accepted = await Send(HttpMethod.Put,
            $"/api/academic-operations/sessions/{sessionId}/attendance", teacher.Token,
            new AttendanceBatch([new AttendanceUpsert(student.Id, "Present", "On time")])) )
            Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        using (var summary = await Send(HttpMethod.Get,
            $"/api/academic-operations/student/me/sections/{x.Section}/attendance", student.Token))
        {
            Assert.Equal(HttpStatusCode.OK, summary.StatusCode);
            Assert.Equal(1, (await summary.Content.ReadFromJsonAsync<AttendanceSummary>())!.Present);
        }
    }

    [Fact]
    public async Task StaffTimetableRejectsOverlappingSlots()
    {
        var x = await Seed();
        var teacher = await User(x.Institution, AcademixRoles.Instructor);
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AcademixDbContext>();
            db.TeachingAssignments.Add(new TeachingAssignment { InstitutionId = x.Institution,
                SectionId = x.Section, UserId = teacher.Id, Kind = "Instructor" });
            await db.SaveChangesAsync();
        }
        using (var first = await Send(HttpMethod.Post,
            $"/api/academic-operations/sections/{x.Section}/timetable", teacher.Token,
            new SlotUpsert(1, 540, 120, "Room 101")))
            Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        using (var overlap = await Send(HttpMethod.Post,
            $"/api/academic-operations/sections/{x.Section}/timetable", teacher.Token,
            new SlotUpsert(1, 600, 90, "Room 101")))
            Assert.Equal(HttpStatusCode.Conflict, overlap.StatusCode);
    }
}
