using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Academix.Api.Data;
using Academix.Api.Domain.Academic;
using Academix.Api.Domain.Identity;
using Academix.Api.DTOs.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Academix.Tests.Integration;

public sealed class Phase1ConcurrencyTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private async Task<(Guid Id, string Token)> CreateUser(string role)
    {
        var email = $"{role}-{Guid.NewGuid():N}@example.test";
        var id = Guid.NewGuid();
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser { Id = id, UserName = email, Email = email, InstitutionId = AcademicIds.InitialInstitution };
            Assert.True((await users.CreateAsync(user, "Safe!TestPassword2026")).Succeeded);
            Assert.True((await users.AddToRoleAsync(user, role)).Succeeded);
        }
        var login = await fixture.Client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "Safe!TestPassword2026"));
        login.EnsureSuccessStatusCode();
        var result = await login.Content.ReadFromJsonAsync<AuthResponse>();
        return (id, result!.AccessToken);
    }

    private async Task<HttpResponseMessage> Send(HttpMethod method, string url, string token, object? body = null)
    {
        using var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null) request.Content = JsonContent.Create(body);
        return await fixture.Client.SendAsync(request);
    }

    private static async Task<Guid> CreatedId(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task LastAvailableSeat_IsNotOversubscribedUnderConcurrentRequests()
    {
        var (_, adminToken) = await CreateUser(AcademixRoles.InstitutionAdministrator);
        var (instructorId, _) = await CreateUser(AcademixRoles.Instructor);
        var (s1, _) = await CreateUser(AcademixRoles.Student);
        var (s2, _) = await CreateUser(AcademixRoles.Student);
        var inst = AcademicIds.InitialInstitution;
        var tag = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        var termId = await CreatedId(await Send(HttpMethod.Post, "/api/academic/terms", adminToken,
            new { institutionId = inst, code = $"T{tag}", name = "Term Test", startsOn = "2027-01-01", endsOn = "2027-06-30" }));
        using (var activate = await Send(HttpMethod.Put, $"/api/academic/terms/{termId}", adminToken,
            new { name = "Term Test", startsOn = "2027-01-01", endsOn = "2027-06-30", status = "Active", expectedVersion = 1 }))
            Assert.Equal(HttpStatusCode.OK, activate.StatusCode);
        var courseId = Academix.Api.Data.CourseConfiguration.Cs101Id;
        var sectionId = await CreatedId(await Send(HttpMethod.Post, "/api/academic/sections", adminToken,
            new { institutionId = inst, courseId, termId, code = $"S{tag}", capacity = 1 }));
        using (var assignment = await Send(HttpMethod.Post, $"/api/academic/sections/{sectionId}/staff", adminToken,
            new { userId = instructorId, kind = AcademixRoles.Instructor }))
            Assert.Equal(HttpStatusCode.Created, assignment.StatusCode);
        using (var open = await Send(HttpMethod.Put, $"/api/academic/sections/{sectionId}", adminToken,
            new { capacity = 1, status = "Open", expectedVersion = 1 }))
            Assert.Equal(HttpStatusCode.OK, open.StatusCode);

        var req1 = Send(HttpMethod.Post, $"/api/academic/sections/{sectionId}/enrollments", adminToken, new { studentId = s1 });
        var req2 = Send(HttpMethod.Post, $"/api/academic/sections/{sectionId}/enrollments", adminToken, new { studentId = s2 });
        var outcomes = await Task.WhenAll(req1, req2);
        Assert.Single(outcomes, x => x.StatusCode == HttpStatusCode.Created);
        Assert.Single(outcomes, x => x.StatusCode == HttpStatusCode.Conflict);
        using var roster = await Send(HttpMethod.Get, $"/api/academic/sections/{sectionId}/roster", adminToken);
        Assert.Equal(HttpStatusCode.OK, roster.StatusCode);
        using var json = JsonDocument.Parse(await roster.Content.ReadAsStringAsync());
        Assert.Equal(1, json.RootElement.GetArrayLength());
    }

    [Fact]
    public async Task CapacityReduction_RechecksActiveEnrollmentsAfterWaitingForSectionLock()
    {
        var (_, adminToken) = await CreateUser(AcademixRoles.InstitutionAdministrator);
        var (instructorId, _) = await CreateUser(AcademixRoles.Instructor);
        var (student1, _) = await CreateUser(AcademixRoles.Student);
        var (student2, _) = await CreateUser(AcademixRoles.Student);
        var institution = AcademicIds.InitialInstitution;
        var tag = Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        using var createTerm = await Send(HttpMethod.Post, "/api/academic/terms", adminToken,
            new { institutionId = institution, code = $"T{tag}", name = "Capacity Race Test", startsOn = "2027-07-01", endsOn = "2027-12-31" });
        var termId = await CreatedId(createTerm);
        using (var activate = await Send(HttpMethod.Put, $"/api/academic/terms/{termId}", adminToken,
            new { name = "Capacity Race Test", startsOn = "2027-07-01", endsOn = "2027-12-31", status = "Active", expectedVersion = 1 }))
            Assert.Equal(HttpStatusCode.OK, activate.StatusCode);
        using var createSection = await Send(HttpMethod.Post, "/api/academic/sections", adminToken,
            new { institutionId = institution, courseId = CourseConfiguration.Cs101Id, termId, code = $"S{tag}", capacity = 2 });
        var sectionId = await CreatedId(createSection);
        using (var assign = await Send(HttpMethod.Post, $"/api/academic/sections/{sectionId}/staff", adminToken,
            new { userId = instructorId, kind = AcademixRoles.Instructor }))
            Assert.Equal(HttpStatusCode.Created, assign.StatusCode);
        using (var open = await Send(HttpMethod.Put, $"/api/academic/sections/{sectionId}", adminToken,
            new { capacity = 2, status = "Open", expectedVersion = 1 }))
            Assert.Equal(HttpStatusCode.OK, open.StatusCode);
        using (var initialEnrollment = await Send(HttpMethod.Post, $"/api/academic/sections/{sectionId}/enrollments", adminToken,
            new { studentId = student1 }))
            Assert.Equal(HttpStatusCode.Created, initialEnrollment.StatusCode);

        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AcademixDbContext>();
        var connectionString = db.Database.GetConnectionString()!;
        await using var blocker = new NpgsqlConnection(connectionString);
        await blocker.OpenAsync();
        await using var tx = await blocker.BeginTransactionAsync();
        var committed = false;
        try
        {
            // Hold the same section row which Enroll and UpdateSection must lock.
            await using (var lockRow = new NpgsqlCommand("SELECT 1 FROM course_sections WHERE \"Id\" = @id FOR UPDATE", blocker, tx))
            {
                lockRow.Parameters.AddWithValue("id", sectionId);
                Assert.NotNull(await lockRow.ExecuteScalarAsync());
            }

            // Both the old buggy and new corrected implementation will wait:
            // before the UPDATE (old), or BEFORE counting enrollments (new).
            var shrinkTask = Send(HttpMethod.Put, $"/api/academic/sections/{sectionId}", adminToken,
                new { capacity = 1, status = "Open", expectedVersion = 2 });
            await WaitForSectionStatementToBlock(connectionString);

            // Model the enrollment that commits while the capacity change waits.
            // Writing inside the lock-owning transaction makes the ordering explicit.
            await using (var insert = new NpgsqlCommand("""
                INSERT INTO student_enrollments
                    ("Id", "InstitutionId", "SectionId", "StudentId", "Status", "EnrolledAtUtc")
                VALUES (@id, @institutionId, @sectionId, @studentId, 'Active', now())
                """, blocker, tx))
            {
                insert.Parameters.AddWithValue("id", Guid.NewGuid());
                insert.Parameters.AddWithValue("institutionId", institution);
                insert.Parameters.AddWithValue("sectionId", sectionId);
                insert.Parameters.AddWithValue("studentId", student2);
                Assert.Equal(1, await insert.ExecuteNonQueryAsync());
            }
            await tx.CommitAsync();
            committed = true;

            using var shrinkResponse = await shrinkTask.WaitAsync(TimeSpan.FromSeconds(15));
            Assert.Equal(HttpStatusCode.Conflict, shrinkResponse.StatusCode);

            await using var check = new NpgsqlCommand("""
                SELECT "Capacity" FROM course_sections WHERE "Id" = @sectionId
                """, blocker);
            check.Parameters.AddWithValue("sectionId", sectionId);
            Assert.Equal(2, Convert.ToInt32(await check.ExecuteScalarAsync()));
        }
        finally
        {
            if (!committed) await tx.RollbackAsync();
        }
    }

    private static async Task WaitForSectionStatementToBlock(string connectionString)
    {
        await using var poll = new NpgsqlConnection(connectionString);
        await poll.OpenAsync();
        for (var i = 0; i < 240; i++)
        {
            await using var command = new NpgsqlCommand("""
                SELECT EXISTS (
                    SELECT 1 FROM pg_stat_activity
                    WHERE datname = current_database()
                      AND pid <> pg_backend_pid()
                      AND wait_event_type = 'Lock'
                      AND query ILIKE '%course_sections%'
                )
                """, poll);
            if (await command.ExecuteScalarAsync() is true) return;
            await Task.Delay(50);
        }
        throw new TimeoutException("Capacity update never reached the section-row lock; test cannot prove the race ordering.");
    }

}
