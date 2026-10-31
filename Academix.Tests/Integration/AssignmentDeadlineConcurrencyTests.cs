using System.Data.Common;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Academix.Api.Data;
using Academix.Api.Domain.Academic;
using Academix.Api.Domain.Courses;
using Academix.Api.Domain.Identity;
using Academix.Api.Domain.Learning;
using Academix.Api.DTOs.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace Academix.Tests.Integration;

// Requires Testcontainers/PostgreSQL. The normal patch installer deliberately excludes it.
public sealed class AssignmentDeadlineConcurrencyTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private async Task<(Guid AssignmentId, Guid StudentId, string Token)> SeedAsync(bool existingDraft)
    {
        var institutionId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var termId = Guid.NewGuid();
        var sectionId = Guid.NewGuid();
        var assignmentId = Guid.NewGuid();
        var studentId = Guid.NewGuid();
        var tag = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AcademixDbContext>();
            db.Institutions.Add(new Institution { Id = institutionId, Code = $"I{tag}", Name = "Deadline Regression University" });
            db.Courses.Add(Course.Create(courseId, institutionId, "DL101", "Deadlines"));
            db.AcademicTerms.Add(new AcademicTerm { Id = termId, InstitutionId = institutionId,
                Code = $"T{tag}", Name = "Deadline Term", Status = "Active",
                StartsOn = new DateOnly(2026, 1, 1), EndsOn = new DateOnly(2028, 12, 31) });
            db.CourseSections.Add(new CourseSection { Id = sectionId, InstitutionId = institutionId,
                CourseId = courseId, TermId = termId, Code = $"S{tag}", Status = "Open", Capacity = 5 });
            db.LearningAssignments.Add(new LearningAssignment { Id = assignmentId, InstitutionId = institutionId,
                SectionId = sectionId, Title = "Deadline Check", Instructions = "Sample",
                IsPublished = true, DueAtUtc = DateTime.UtcNow.AddHours(1), MaxScore = 10 });
            await db.SaveChangesAsync();
        }

        var email = $"deadline-{Guid.NewGuid():N}@example.test";
        const string password = "Secure!DeadlineTest2026";
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser { Id = studentId, InstitutionId = institutionId, Email = email, UserName = email };
            Assert.True((await users.CreateAsync(user, password)).Succeeded);
            Assert.True((await users.AddToRoleAsync(user, AcademixRoles.Student)).Succeeded);
            var db = scope.ServiceProvider.GetRequiredService<AcademixDbContext>();
            db.StudentEnrollments.Add(new StudentEnrollment { InstitutionId = institutionId,
                SectionId = sectionId, StudentId = studentId });
            if (existingDraft)
                db.LearningSubmissions.Add(new LearningSubmission { InstitutionId = institutionId,
                    SectionId = sectionId, AssignmentId = assignmentId,
                    StudentId = studentId, Content = "first draft" });
            await db.SaveChangesAsync();
        }
        using var login = await fixture.Client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var auth = await login.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(auth);
        return (assignmentId, studentId, auth.AccessToken);
    }

    private async Task<bool> AssignmentLockWaitObservedAsync(CancellationToken cancellationToken)
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AcademixDbContext>();
        await db.Database.OpenConnectionAsync(cancellationToken);
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = @"SELECT COUNT(*) FROM pg_stat_activity
          WHERE pid <> pg_backend_pid()
            AND datname = current_database()
            AND wait_event_type = 'Lock'
            AND query LIKE '%lms_assignments%'
            AND query LIKE '%FOR UPDATE%'";
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt64(result) > 0;
    }

    private async Task WaitUntilAssignmentLockBlockedAsync(CancellationToken cancellationToken)
    {
        // Observe that the API has already reached the row-lock wait. This avoids
        // a race in which the server has not yet read the initially open assignment.
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(12));
        while (!timeout.IsCancellationRequested)
        {
            if (await AssignmentLockWaitObservedAsync(timeout.Token)) return;
            await Task.Delay(100, timeout.Token);
        }
        throw new Xunit.Sdk.XunitException("The HTTP request never waited on the assignment row lock.");
    }

    private static async Task LockAssignmentAsync(AcademixDbContext db, Guid assignmentId, CancellationToken ct)
    {
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.Transaction = db.Database.CurrentTransaction!.GetDbTransaction();
        command.CommandText = "SELECT \"Id\" FROM lms_assignments WHERE \"Id\" = @id FOR UPDATE";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "id";
        parameter.Value = assignmentId;
        command.Parameters.Add(parameter);
        Assert.NotNull(await command.ExecuteScalarAsync(ct));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ClosedAssignmentDuringRowLockWaitRejectsNewWork(bool submitExistingDraft)
    {
        var (assignmentId, studentId, token) = await SeedAsync(submitExistingDraft);
        await using var lockingScope = fixture.Services.CreateAsyncScope();
        var lockingDb = lockingScope.ServiceProvider.GetRequiredService<AcademixDbContext>();
        await using var tx = await lockingDb.Database.BeginTransactionAsync();
        await LockAssignmentAsync(lockingDb, assignmentId, CancellationToken.None);

        using var request = new HttpRequestMessage(
            submitExistingDraft ? HttpMethod.Post : HttpMethod.Put,
            submitExistingDraft ? $"/api/lms/assignments/{assignmentId}/submit" : $"/api/lms/assignments/{assignmentId}/my-draft");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (!submitExistingDraft) request.Content = JsonContent.Create(new { content = "new draft" });

        using var requestTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(25));
        var pending = fixture.Client.SendAsync(request, requestTimeout.Token);
        await WaitUntilAssignmentLockBlockedAsync(requestTimeout.Token);
        Assert.False(pending.IsCompleted);

        // The student has already passed the old pre-lock check. Deadline now changes
        // in the same transaction holding the assignment lock.
        await lockingDb.Database.ExecuteSqlInterpolatedAsync($"UPDATE lms_assignments SET \"DueAtUtc\"={DateTime.UtcNow.AddMinutes(-2)} WHERE \"Id\"={assignmentId}");
        await tx.CommitAsync();

        using var response = await pending;
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await using var checkScope = fixture.Services.CreateAsyncScope();
        var checkDb = checkScope.ServiceProvider.GetRequiredService<AcademixDbContext>();
        var submission = await checkDb.LearningSubmissions.AsNoTracking()
            .SingleOrDefaultAsync(x => x.AssignmentId == assignmentId && x.StudentId == studentId);
        if (submitExistingDraft)
        {
            Assert.NotNull(submission);
            Assert.Equal("Draft", submission.Status);
        }
        else Assert.Null(submission);
    }
}
