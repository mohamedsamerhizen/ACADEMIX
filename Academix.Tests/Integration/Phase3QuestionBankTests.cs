using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Academix.Api.Data;
using Academix.Api.Domain.Academic;
using Academix.Api.Domain.Courses;
using Academix.Api.Domain.Identity;
using Academix.Api.DTOs.Examinations;
using Academix.Api.DTOs.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Academix.Tests.Integration;

// Runs only when PostgreSQL/Testcontainers is available. Does not require MinIO.
public sealed class Phase3QuestionBankTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private async Task<(Guid Id, string Token)> User(Guid institution, string role)
    {
        var id = Guid.NewGuid();
        var email = $"qb-{Guid.NewGuid():N}@example.test";
        const string password = "Secure!Phase3QuestionBank2026";
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var account = new ApplicationUser { Id = id, Email = email, UserName = email, InstitutionId = institution };
            Assert.True((await users.CreateAsync(account, password)).Succeeded);
            Assert.True((await users.AddToRoleAsync(account, role)).Succeeded);
        }
        using var login = await fixture.Client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        return (id, (await login.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken);
    }

    private async Task<HttpResponseMessage> Send(HttpMethod method, string url, string token, object? body = null)
    {
        using var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null) request.Content = JsonContent.Create(body);
        return await fixture.Client.SendAsync(request);
    }

    private static QuestionDraftRequest Draft(string stem = "Explain database transactions") =>
        new("SingleChoice", stem, "Medium", 2m,
            [new("a", "Atomicity"), new("b", "Availability")], ["a"], [], null, ["database"]);

    [Fact]
    public async Task StaffCanCreateReviseAndArchiveWithoutMutatingHistoricalRevisions()
    {
        var institutionId = Guid.NewGuid();
        var courseId = Guid.NewGuid();
        var termId = Guid.NewGuid();
        var sectionId = Guid.NewGuid();
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AcademixDbContext>();
            db.Institutions.Add(new Institution { Id = institutionId, Code = "U" + Guid.NewGuid().ToString("N")[..7], Name = "Exams College" });
            db.Courses.Add(Course.Create(courseId, institutionId, "EXAM101", "Examination Engineering"));
            db.AcademicTerms.Add(new AcademicTerm { Id = termId, InstitutionId = institutionId,
                Code = "TERM1", Name = "Term", Status = "Active", StartsOn = new DateOnly(2026, 10, 1), EndsOn = new DateOnly(2027, 6, 1) });
            db.CourseSections.Add(new CourseSection { Id = sectionId, InstitutionId = institutionId, CourseId = courseId,
                TermId = termId, Code = "A", Status = "Open", Capacity = 30 });
            await db.SaveChangesAsync();
        }
        var (staffId, instructor) = await User(institutionId, AcademixRoles.Instructor);
        var (_, student) = await User(institutionId, AcademixRoles.Student);
        var otherInstitution = Guid.NewGuid();
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AcademixDbContext>();
            db.Institutions.Add(new Institution { Id = otherInstitution, Code = "U" + Guid.NewGuid().ToString("N")[..7], Name = "Other College" });
            db.TeachingAssignments.Add(new TeachingAssignment { InstitutionId = institutionId,
                SectionId = sectionId, UserId = staffId, Kind = AcademixRoles.Instructor });
            await db.SaveChangesAsync();
        }
        var (_, outsider) = await User(otherInstitution, AcademixRoles.Instructor);

        using var created = await Send(HttpMethod.Post, $"/api/exams/courses/{courseId}/questions", instructor, Draft());
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var first = (await created.Content.ReadFromJsonAsync<BankQuestionDetail>())!;
        var id = first.Question.Id;
        Assert.Equal(1, first.Revision.RevisionNumber);
        using (var denied = await Send(HttpMethod.Get, $"/api/exams/questions/{id}", student))
            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        using (var hidden = await Send(HttpMethod.Get, $"/api/exams/questions/{id}", outsider))
            Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);

        using var edited = await Send(HttpMethod.Put, $"/api/exams/questions/{id}", instructor,
            new QuestionEditRequest(first.Question.Version, Draft("Explain ACID transactions")));
        Assert.Equal(HttpStatusCode.OK, edited.StatusCode);
        var second = (await edited.Content.ReadFromJsonAsync<BankQuestionDetail>())!;
        Assert.Equal(2, second.Question.CurrentRevision);
        Assert.NotEqual(first.Revision.RevisionId, second.Revision.RevisionId);
        using (var original = await Send(HttpMethod.Get, $"/api/exams/questions/{id}/revisions/1", instructor))
        {
            Assert.Equal(HttpStatusCode.OK, original.StatusCode);
            var previous = (await original.Content.ReadFromJsonAsync<QuestionRevisionResponse>())!;
            Assert.Equal("Explain database transactions", previous.Body.Stem);
        }
        using (var stale = await Send(HttpMethod.Put, $"/api/exams/questions/{id}", instructor,
                   new QuestionEditRequest(first.Question.Version, Draft())))
            Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using (var archived = await Send(HttpMethod.Put, $"/api/exams/questions/{id}/archive", instructor,
                   new QuestionArchiveRequest(second.Question.Version, true)))
            Assert.Equal(HttpStatusCode.OK, archived.StatusCode);
        using (var list = await Send(HttpMethod.Get, $"/api/exams/courses/{courseId}/questions", instructor))
        {
            Assert.Equal(HttpStatusCode.OK, list.StatusCode);
            var items = (await list.Content.ReadFromJsonAsync<List<BankQuestionSummary>>())!;
            Assert.DoesNotContain(items, q => q.Id == id);
        }
    }
}
