using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Academix.Api.Data;
using Academix.Api.Domain.Academic;
using Academix.Api.Domain.Courses;
using Academix.Api.Domain.Identity;
using Academix.Api.DTOs.Communications;
using Academix.Api.DTOs.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Academix.Tests.Integration;

// Requires an actual PostgreSQL/Testcontainers database and all pending migrations.
public sealed class Phase10CommunicationsTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private const string Password = "Valid!TestPassword2026";

    private async Task<(Guid Id, string Token)> User(Guid institution, string role)
    {
        var id = Guid.NewGuid(); var email = $"communications-{Guid.NewGuid():N}@example.test";
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser { Id = id, Email = email, UserName = email, InstitutionId = institution };
            Assert.True((await manager.CreateAsync(user, Password)).Succeeded);
            Assert.True((await manager.AddToRoleAsync(user, role)).Succeeded);
        }
        using var login = await fixture.Client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, Password));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        return (id, (await login.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken);
    }

    private async Task<(Guid Institution, Guid Section)> Seed()
    {
        var inst = Guid.NewGuid(); var section = Guid.NewGuid(); var course = Guid.NewGuid(); var term = Guid.NewGuid();
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AcademixDbContext>();
        db.Institutions.Add(new Institution { Id = inst, Code = "CM" + Guid.NewGuid().ToString("N")[..8], Name = "Communications Institute" });
        db.Courses.Add(Course.Create(course, inst, "COM101", "Communications"));
        db.AcademicTerms.Add(new AcademicTerm { Id = term, InstitutionId = inst, Code = "FALL", Name = "Fall",
            StartsOn = new DateOnly(2026, 1, 1), EndsOn = new DateOnly(2027, 1, 1) });
        db.CourseSections.Add(new CourseSection { Id = section, InstitutionId = inst, CourseId = course, TermId = term,
            Code = "SEC1", Status = "Open", Capacity = 100 });
        await db.SaveChangesAsync();
        return (inst, section);
    }

    private async Task<HttpResponseMessage> Send(HttpMethod method, string uri, string token, object? body = null)
    {
        using var request = new HttpRequestMessage(method, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null) request.Content = JsonContent.Create(body);
        return await fixture.Client.SendAsync(request);
    }

    [Fact]
    public async Task AuthorizedMessagingInboxPreferencesAndAnnouncements()
    {
        var (inst, section) = await Seed();
        var admin = await User(inst, AcademixRoles.InstitutionAdministrator);
        var student = await User(inst, AcademixRoles.Student);
        var otherStudent = await User(inst, AcademixRoles.Student);
        var (anotherInstitution, _) = await Seed();
        var outsider = await User(anotherInstitution, AcademixRoles.Student);
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AcademixDbContext>();
            // Outsider has no usable institution; recipient is never a member of the section.
            db.StudentEnrollments.Add(new StudentEnrollment { InstitutionId = inst, SectionId = section,
                StudentId = student.Id, Status = "Active" });
            await db.SaveChangesAsync();
        }
        var prefix = "/api/communications";
        using var forbiddenStudentThread = await Send(HttpMethod.Post, $"{prefix}/sections/{section}/threads", student.Token,
            new StartSectionThread(otherStudent.Id));
        Assert.NotEqual(HttpStatusCode.OK, forbiddenStudentThread.StatusCode);

        using var create = await Send(HttpMethod.Post, $"{prefix}/sections/{section}/threads", admin.Token,
            new StartSectionThread(student.Id));
        Assert.Equal(HttpStatusCode.OK, create.StatusCode);
        var thread = (await create.Content.ReadFromJsonAsync<SectionThreadView>())!;
        using var same = await Send(HttpMethod.Post, $"{prefix}/sections/{section}/threads", student.Token,
            new StartSectionThread(admin.Id));
        Assert.Equal(thread.Id, (await same.Content.ReadFromJsonAsync<SectionThreadView>())!.Id);

        var key = Guid.NewGuid();
        using var sent = await Send(HttpMethod.Post, $"{prefix}/threads/{thread.Id}/messages", admin.Token,
            new SendSectionMessage(key, "  Review chapter one  "));
        Assert.Equal(HttpStatusCode.OK, sent.StatusCode);
        var message = (await sent.Content.ReadFromJsonAsync<SectionMessageView>())!;
        Assert.Equal("Review chapter one", message.Body);
        using var replay = await Send(HttpMethod.Post, $"{prefix}/threads/{thread.Id}/messages", admin.Token,
            new SendSectionMessage(key, "Review chapter one"));
        Assert.Equal(message.Id, (await replay.Content.ReadFromJsonAsync<SectionMessageView>())!.Id);
        using var keyConflict = await Send(HttpMethod.Post, $"{prefix}/threads/{thread.Id}/messages", admin.Token,
            new SendSectionMessage(key, "Different content"));
        Assert.Equal(HttpStatusCode.Conflict, keyConflict.StatusCode);
        using var outsiderRead = await Send(HttpMethod.Get, $"{prefix}/threads/{thread.Id}/messages", otherStudent.Token);
        Assert.NotEqual(HttpStatusCode.OK, outsiderRead.StatusCode);
        using var invalidInstRead = await Send(HttpMethod.Get, $"{prefix}/threads/{thread.Id}/messages", outsider.Token);
        Assert.NotEqual(HttpStatusCode.OK, invalidInstRead.StatusCode);

        using var inbox = await Send(HttpMethod.Get, $"{prefix}/notifications", student.Token);
        Assert.Equal(HttpStatusCode.OK, inbox.StatusCode);
        var items = (await inbox.Content.ReadFromJsonAsync<NotificationInbox>())!;
        Assert.Equal(1, items.UnreadCount);
        Assert.Single(items.Items);
        using var read = await Send(HttpMethod.Put, $"{prefix}/notifications/{items.Items[0].Id}/read", student.Token);
        Assert.Equal(HttpStatusCode.NoContent, read.StatusCode);

        using var preference = await Send(HttpMethod.Put, $"{prefix}/preferences", student.Token,
            new UpdateCommunicationPreference(false, true));
        Assert.False((await preference.Content.ReadFromJsonAsync<CommunicationPreferenceView>())!.MessageNotifications);
        using var mutedMessage = await Send(HttpMethod.Post, $"{prefix}/threads/{thread.Id}/messages", admin.Token,
            new SendSectionMessage(Guid.NewGuid(), "This still reaches the conversation"));
        Assert.Equal(HttpStatusCode.OK, mutedMessage.StatusCode);
        using var history = await Send(HttpMethod.Get, $"{prefix}/threads/{thread.Id}/messages", student.Token);
        Assert.Equal(2, (await history.Content.ReadFromJsonAsync<List<SectionMessageView>>())!.Count);

        var future = DateTime.UtcNow.AddMinutes(10);
        using var createFuture = await Send(HttpMethod.Post, $"{prefix}/sections/{section}/announcements", admin.Token,
            new ScheduleSectionAnnouncement("Lecture details", "Please read before the next lecture", "Students", future));
        Assert.Equal(HttpStatusCode.OK, createFuture.StatusCode);
        var futureAnnouncement = (await createFuture.Content.ReadFromJsonAsync<SectionAnnouncementView>())!;
        using var unreleased = await Send(HttpMethod.Get, $"{prefix}/sections/{section}/announcements", student.Token);
        Assert.Empty((await unreleased.Content.ReadFromJsonAsync<List<SectionAnnouncementView>>())!);
        using var cancel = await Send(HttpMethod.Delete, $"{prefix}/announcements/{futureAnnouncement.Id}?expectedVersion=1", admin.Token);
        Assert.Equal(HttpStatusCode.NoContent, cancel.StatusCode);

        using var badAudience = await Send(HttpMethod.Post, $"{prefix}/sections/{section}/announcements", student.Token,
            new ScheduleSectionAnnouncement("Denied", "Not authorized", "Students", future));
        Assert.NotEqual(HttpStatusCode.OK, badAudience.StatusCode);
        using var due = await Send(HttpMethod.Post, $"{prefix}/sections/{section}/announcements", admin.Token,
            new ScheduleSectionAnnouncement("Grade release", "Updated timetable is available", "Students", DateTime.UtcNow.AddSeconds(-30)));
        Assert.Equal(HttpStatusCode.OK, due.StatusCode);
        var announcement = (await due.Content.ReadFromJsonAsync<SectionAnnouncementView>())!;
        using var visible = await Send(HttpMethod.Get, $"{prefix}/sections/{section}/announcements", student.Token);
        Assert.Single((await visible.Content.ReadFromJsonAsync<List<SectionAnnouncementView>>())!);
        using var dispatch = await Send(HttpMethod.Post, $"{prefix}/announcements/{announcement.Id}/dispatch", admin.Token);
        Assert.Equal(HttpStatusCode.OK, dispatch.StatusCode);
        Assert.Equal(1, (await dispatch.Content.ReadFromJsonAsync<AnnouncementDispatchResult>())!.NotificationsCreated);
        using var repeat = await Send(HttpMethod.Post, $"{prefix}/announcements/{announcement.Id}/dispatch", admin.Token);
        Assert.Equal(0, (await repeat.Content.ReadFromJsonAsync<AnnouncementDispatchResult>())!.NotificationsCreated);
        using var inboxAfter = await Send(HttpMethod.Get, $"{prefix}/notifications", student.Token);
        Assert.Equal(2, (await inboxAfter.Content.ReadFromJsonAsync<NotificationInbox>())!.Items.Count);
    }
}
