using System.Security.Claims;
using Academix.Api.Data;
using Academix.Api.Services.Infrastructure;
using Academix.Api.Domain.Academic;
using Academix.Api.Domain.Communications;
using Academix.Api.Domain.Identity;
using Academix.Api.DTOs.Communications;
using Academix.Api.Middleware;
using Academix.Api.Services.AdvancedLearning;
using Academix.Api.Services.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Academix.Api.Services.Communications;

public sealed class CommunicationService(
    AcademixDbContext db, AdvancedLearningAccess access, IdentitySecurity security,
    UserManager<ApplicationUser> users, RetryingServiceWork retry)
{
    internal async Task RecordAcademicUpdate(Guid institution,Guid student,Guid eventId,CancellationToken ct,bool financial=false)
    {
        if(db.Database.CurrentTransaction is null)throw new InvalidOperationException("Notification must commit with the Outbox acknowledgment.");
        var recipient=await db.Users.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==student&&x.InstitutionId==institution,ct)??throw ApiFault.NotFound();
        if(!recipient.IsActive)return;
        var kind=financial?"Finance":"AcademicProgress";
        if(!await db.InAppNotifications.AnyAsync(x=>x.InstitutionId==institution&&x.RecipientId==student&&x.Kind==kind&&x.SourceId==eventId,ct))
            db.InAppNotifications.Add(new(){InstitutionId=institution,RecipientId=student,Kind=kind,SourceId=eventId,
                Title=financial?"Student finance update":"Academic progress update",Body=financial?"Your student financial account has an update. Open your statement for details.":"An advising, academic requirement, or graduation update is available in your academic record."});
    }

    private static SectionThreadView ThreadView(SectionMessageThread t, Guid userId) =>
        new(t.Id, t.SectionId, t.MemberLowId == userId ? t.MemberHighId : t.MemberLowId,
            t.CreatedAtUtc, t.MemberLowId == userId ? t.LastReadLowAtUtc : t.LastReadHighAtUtc);
    private static SectionMessageView MessageView(SectionDirectMessage m) =>
        new(m.Id, m.ThreadId, m.SenderId, m.Body, m.CreatedAtUtc);
    private static InboxNotificationView NotificationView(InAppNotification n) =>
        new(n.Id, n.Kind, n.SourceId, n.Title, n.Body, n.CreatedAtUtc, n.ReadAtUtc);
    private static SectionAnnouncementView AnnouncementView(ScheduledSectionAnnouncement a) =>
        new(a.Id, a.SectionId, a.AuthorId, a.Title, a.Body, a.Audience,
            a.ScheduledForUtc, a.DispatchedAtUtc, a.IsCancelled, a.Version);

    private async Task<(ApplicationUser User, Guid InstitutionId)> CurrentInstitution(ClaimsPrincipal principal, CancellationToken ct)
    {
        var (user, _) = await security.CurrentAsync(principal, ct);
        if (user.InstitutionId is not Guid institutionId) throw ApiFault.Forbidden();
        return (user, institutionId);
    }

    private async Task<(SectionMessageThread Thread, Guid Actor)> MyThread(ClaimsPrincipal principal,
        Guid threadId, CancellationToken ct, bool forUpdate = false)
    {
        var (actor, institutionId) = await CurrentInstitution(principal, ct);
        var thread = forUpdate
            ? (await db.SectionMessageThreads.FromSqlInterpolated($"SELECT * FROM communication_threads WHERE \"Id\" = {threadId} FOR UPDATE")
                .ToListAsync(ct)).SingleOrDefault()
            : await db.SectionMessageThreads.SingleOrDefaultAsync(x => x.Id == threadId, ct);
        if (thread is null || thread.InstitutionId != institutionId ||
            (thread.MemberLowId != actor.Id && thread.MemberHighId != actor.Id)) throw ApiFault.NotFound();
        var section = await access.Section(thread.SectionId, ct);
        await access.Viewer(principal, section, ct);
        return (thread, actor.Id);
    }

    private async Task<bool> IsStaffMember(ApplicationUser user, CourseSection section, CancellationToken ct)
    {
        if (!user.IsActive || user.InstitutionId != section.InstitutionId) return false;
        var roles = (await users.GetRolesAsync(user)).ToArray();
        return roles.Contains(AcademixRoles.InstitutionAdministrator) ||
            await security.IsCurrentSectionTeacherAsync(user, roles, section.InstitutionId, section.Id, ct);
    }

    private async Task<bool> IsStudentMember(ApplicationUser user, CourseSection section, CancellationToken ct)
    {
        if (!user.IsActive || user.InstitutionId != section.InstitutionId || section.Status != "Open") return false;
        var roles = await users.GetRolesAsync(user);
        return roles.Contains(AcademixRoles.Student) &&
            await db.StudentEnrollments.AnyAsync(e => e.InstitutionId == section.InstitutionId &&
                e.SectionId == section.Id && e.StudentId == user.Id && e.Status == "Active", ct);
    }

    public async Task<SectionThreadView> StartThread(ClaimsPrincipal principal, Guid sectionId,
        StartSectionThread request, CancellationToken ct)
    {
        var section = await access.Section(sectionId, ct);
        var (actorId, actorStaff) = await access.Viewer(principal, section, ct);
        var target = await db.Users.SingleOrDefaultAsync(u => u.Id == request.RecipientId &&
            u.InstitutionId == section.InstitutionId && u.IsActive, ct);
        if (target is null) throw ApiFault.NotFound();
        var targetStaff = await IsStaffMember(target, section, ct);
        var targetStudent = !targetStaff && await IsStudentMember(target, section, ct);
        if (actorStaff == targetStaff || !(targetStaff || targetStudent)) throw ApiFault.Forbidden();
        var (low, high) = CommunicationPolicy.OrderedMembers(actorId, target.Id);
        var existing = await db.SectionMessageThreads.AsNoTracking().SingleOrDefaultAsync(t =>
            t.SectionId == sectionId && t.MemberLowId == low && t.MemberHighId == high, ct);
        if (existing is not null) return ThreadView(existing, actorId);
        var thread = new SectionMessageThread { InstitutionId = section.InstitutionId, SectionId = section.Id,
            MemberLowId = low, MemberHighId = high };
        db.SectionMessageThreads.Add(thread);
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException)
        {
            // Unique section/participant key is the authoritative concurrency guard.
            db.Entry(thread).State = EntityState.Detached;
            var winner = await db.SectionMessageThreads.AsNoTracking().SingleOrDefaultAsync(t =>
                t.SectionId == sectionId && t.MemberLowId == low && t.MemberHighId == high, ct);
            if (winner is null) throw;
            return ThreadView(winner, actorId);
        }
        return ThreadView(thread, actorId);
    }

    public async Task<IReadOnlyList<SectionThreadView>> Threads(ClaimsPrincipal principal, Guid sectionId,
        int offset, int limit, CancellationToken ct)
    {
        CommunicationPolicy.Page(offset, limit);
        var section = await access.Section(sectionId, ct);
        var (actor, _) = await access.Viewer(principal, section, ct);
        var threads = await db.SectionMessageThreads.AsNoTracking().Where(t => t.SectionId == sectionId &&
            (t.MemberLowId == actor || t.MemberHighId == actor))
            .OrderByDescending(t => t.CreatedAtUtc).ThenBy(t => t.Id).Skip(offset).Take(limit).ToListAsync(ct);
        return threads.Select(t => ThreadView(t, actor)).ToArray();
    }

    public async Task<IReadOnlyList<SectionMessageView>> Messages(ClaimsPrincipal principal, Guid threadId,
        int offset, int limit, CancellationToken ct)
    {
        CommunicationPolicy.Page(offset, limit);
        await MyThread(principal, threadId, ct);
        var messages = await db.SectionDirectMessages.AsNoTracking().Where(m => m.ThreadId == threadId)
            .OrderBy(m => m.CreatedAtUtc).ThenBy(m => m.Id).Skip(offset).Take(limit).ToListAsync(ct);
        return messages.Select(MessageView).ToArray();
    }

    public Task<SectionMessageView> Send(ClaimsPrincipal principal, Guid threadId,
        SendSectionMessage request, CancellationToken ct)
        => retry.ExecuteAsync<CommunicationService, SectionMessageView>(service => service.SendCore(principal, threadId, request, ct), ct);

    private async Task<SectionMessageView> SendCore(ClaimsPrincipal principal, Guid threadId,
        SendSectionMessage request, CancellationToken ct)
    {
        if (request.ClientMessageId == Guid.Empty) throw ApiFault.Bad("ClientMessageId is required.");
        var body = CommunicationPolicy.Text(request.Body, 4000, "message");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var (thread, actor) = await MyThread(principal, threadId, ct, forUpdate: true);
        var previous = await db.SectionDirectMessages.AsNoTracking().SingleOrDefaultAsync(m => m.ThreadId == threadId &&
            m.SenderId == actor && m.ClientMessageId == request.ClientMessageId, ct);
        if (previous is not null)
        {
            if (previous.Body != body) throw ApiFault.Conflict("ClientMessageId has already been used with different content.");
            await tx.CommitAsync(ct);
            return MessageView(previous);
        }
        var message = new SectionDirectMessage { InstitutionId = thread.InstitutionId, ThreadId = thread.Id,
            SenderId = actor, Body = body, ClientMessageId = request.ClientMessageId };
        db.SectionDirectMessages.Add(message);
        var recipientId = thread.MemberLowId == actor ? thread.MemberHighId : thread.MemberLowId;
        var preference = await db.CommunicationPreferences.AsNoTracking().SingleOrDefaultAsync(p =>
            p.UserId == recipientId && p.InstitutionId == thread.InstitutionId, ct);
        if (preference?.MessageNotifications != false)
            db.InAppNotifications.Add(new InAppNotification { InstitutionId = thread.InstitutionId,
                RecipientId = recipientId, Kind = "Message", SourceId = message.Id,
                Title = "New section message", Body = "A new private message was received." });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return MessageView(message);
    }

    public Task MarkThreadRead(ClaimsPrincipal principal, Guid threadId, CancellationToken ct)
        => retry.ExecuteAsync<CommunicationService>(service => service.MarkThreadReadCore(principal, threadId, ct), ct);

    private async Task MarkThreadReadCore(ClaimsPrincipal principal, Guid threadId, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var (thread, actor) = await MyThread(principal, threadId, ct, forUpdate: true);
        if (actor == thread.MemberLowId) thread.LastReadLowAtUtc = DateTime.UtcNow;
        else thread.LastReadHighAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async Task<NotificationInbox> Inbox(ClaimsPrincipal principal, int offset, int limit, CancellationToken ct)
    {
        CommunicationPolicy.Page(offset, limit);
        var (user, inst) = await CurrentInstitution(principal, ct);
        var query = db.InAppNotifications.AsNoTracking().Where(n => n.RecipientId == user.Id && n.InstitutionId == inst);
        var unread = await query.CountAsync(n => n.ReadAtUtc == null, ct);
        var rows = await query.OrderByDescending(n => n.CreatedAtUtc).ThenByDescending(n => n.Id)
            .Skip(offset).Take(limit).ToListAsync(ct);
        return new NotificationInbox(rows.Select(NotificationView).ToArray(), unread);
    }

    public async Task MarkNotificationRead(ClaimsPrincipal principal, Guid id, CancellationToken ct)
    {
        var (user, inst) = await CurrentInstitution(principal, ct);
        var item = await db.InAppNotifications.SingleOrDefaultAsync(n => n.Id == id &&
            n.RecipientId == user.Id && n.InstitutionId == inst, ct) ?? throw ApiFault.NotFound();
        if (item.ReadAtUtc is null) { item.ReadAtUtc = DateTime.UtcNow; await db.SaveChangesAsync(ct); }
    }

    public async Task<int> ReadAll(ClaimsPrincipal principal, CancellationToken ct)
    {
        var (user, inst) = await CurrentInstitution(principal, ct);
        return await db.InAppNotifications.Where(n => n.RecipientId == user.Id &&
            n.InstitutionId == inst && n.ReadAtUtc == null)
            .ExecuteUpdateAsync(s => s.SetProperty(n => n.ReadAtUtc, DateTime.UtcNow), ct);
    }

    public async Task<CommunicationPreferenceView> GetPreferences(ClaimsPrincipal principal, CancellationToken ct)
    {
        var (user, inst) = await CurrentInstitution(principal, ct);
        var pref = await db.CommunicationPreferences.AsNoTracking().SingleOrDefaultAsync(p =>
            p.UserId == user.Id && p.InstitutionId == inst, ct);
        return new CommunicationPreferenceView(pref?.MessageNotifications ?? true, pref?.AnnouncementNotifications ?? true);
    }

    public async Task<CommunicationPreferenceView> SetPreferences(ClaimsPrincipal principal,
        UpdateCommunicationPreference request, CancellationToken ct)
    {
        var (user, inst) = await CurrentInstitution(principal, ct);
        var pref = await db.CommunicationPreferences.SingleOrDefaultAsync(p => p.UserId == user.Id && p.InstitutionId == inst, ct);
        var wasAdded = pref is null;
        if (pref is null)
        {
            pref = new CommunicationPreference { UserId = user.Id, InstitutionId = inst };
            db.CommunicationPreferences.Add(pref);
        }
        pref.MessageNotifications = request.MessageNotifications;
        pref.AnnouncementNotifications = request.AnnouncementNotifications;
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) when (wasAdded)
        {
            // Concurrent first preference writes: unique user key wins; retry as an update.
            db.Entry(pref).State = EntityState.Detached;
            var existing = await db.CommunicationPreferences.SingleOrDefaultAsync(p => p.UserId == user.Id &&
                p.InstitutionId == inst, ct);
            if (existing is null) throw;
            existing.MessageNotifications = request.MessageNotifications;
            existing.AnnouncementNotifications = request.AnnouncementNotifications;
            await db.SaveChangesAsync(ct);
        }
        return new CommunicationPreferenceView(request.MessageNotifications, request.AnnouncementNotifications);
    }

    public async Task<SectionAnnouncementView> ScheduleAnnouncement(ClaimsPrincipal principal,
        Guid sectionId, ScheduleSectionAnnouncement request, CancellationToken ct)
    {
        var section = await access.Section(sectionId, ct);
        var actor = await access.Staff(principal, section, ct);
        CommunicationPolicy.Schedule(request.Audience, request.ScheduledForUtc);
        var item = new ScheduledSectionAnnouncement { InstitutionId = section.InstitutionId,
            SectionId = section.Id, AuthorId = actor,
            Title = CommunicationPolicy.Text(request.Title, 160, "announcement title"),
            Body = CommunicationPolicy.Text(request.Body, 4000, "announcement body"),
            Audience = request.Audience, ScheduledForUtc = request.ScheduledForUtc };
        db.ScheduledSectionAnnouncements.Add(item);
        await db.SaveChangesAsync(ct);
        return AnnouncementView(item);
    }

    public async Task<SectionAnnouncementView> ReviseAnnouncement(ClaimsPrincipal principal, Guid id,
        ReviseSectionAnnouncement request, CancellationToken ct)
    {
        var item = await db.ScheduledSectionAnnouncements.SingleOrDefaultAsync(a => a.Id == id, ct)
            ?? throw ApiFault.NotFound();
        await access.Staff(principal, await access.Section(item.SectionId, ct), ct);
        if (item.IsCancelled || item.DispatchedAtUtc.HasValue || item.ScheduledForUtc <= DateTime.UtcNow)
            throw ApiFault.Conflict("The announcement is already due or finalized.");
        if (item.Version != request.ExpectedVersion) throw ApiFault.Conflict("Announcement version changed.");
        CommunicationPolicy.Schedule(request.Audience, request.ScheduledForUtc);
        item.Title = CommunicationPolicy.Text(request.Title, 160, "announcement title");
        item.Body = CommunicationPolicy.Text(request.Body, 4000, "announcement body");
        item.Audience = request.Audience; item.ScheduledForUtc = request.ScheduledForUtc; item.Version++;
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { throw ApiFault.Conflict("Announcement changed concurrently."); }
        return AnnouncementView(item);
    }

    public async Task CancelAnnouncement(ClaimsPrincipal principal, Guid id, long expectedVersion, CancellationToken ct)
    {
        var item = await db.ScheduledSectionAnnouncements.SingleOrDefaultAsync(a => a.Id == id, ct)
            ?? throw ApiFault.NotFound();
        await access.Staff(principal, await access.Section(item.SectionId, ct), ct);
        if (item.Version != expectedVersion) throw ApiFault.Conflict("Announcement version changed.");
        if (item.DispatchedAtUtc.HasValue || item.ScheduledForUtc <= DateTime.UtcNow)
            throw ApiFault.Conflict("Due or dispatched announcements cannot be cancelled.");
        if (!item.IsCancelled)
        {
            item.IsCancelled = true; item.Version++;
            try { await db.SaveChangesAsync(ct); }
            catch (DbUpdateConcurrencyException) { throw ApiFault.Conflict("Announcement changed concurrently."); }
        }
    }

    public async Task<IReadOnlyList<SectionAnnouncementView>> Announcements(ClaimsPrincipal principal,
        Guid sectionId, int offset, int limit, CancellationToken ct)
    {
        CommunicationPolicy.Page(offset, limit);
        var section = await access.Section(sectionId, ct);
        var (_, staff) = await access.Viewer(principal, section, ct);
        var query = db.ScheduledSectionAnnouncements.AsNoTracking().Where(a => a.SectionId == sectionId);
        if (!staff)
        {
            var now = DateTime.UtcNow;
            query = query.Where(a => !a.IsCancelled && a.ScheduledForUtc <= now &&
                (a.Audience == "All" || a.Audience == "Students"));
        }
        var items = await query.OrderByDescending(a => a.ScheduledForUtc).ThenBy(a => a.Id)
            .Skip(offset).Take(limit).ToListAsync(ct);
        return items.Select(AnnouncementView).ToArray();
    }

    public Task<AnnouncementDispatchResult> DispatchScheduled(Guid id, CancellationToken ct)
        => retry.ExecuteAsync<CommunicationService, AnnouncementDispatchResult>(service => service.DispatchAnnouncementCore(null, id, ct), ct);

    public Task<AnnouncementDispatchResult> DispatchAnnouncement(ClaimsPrincipal principal, Guid id, CancellationToken ct)
        => retry.ExecuteAsync<CommunicationService, AnnouncementDispatchResult>(service => service.DispatchAnnouncementCore(principal, id, ct), ct);

    private async Task<AnnouncementDispatchResult> DispatchAnnouncementCore(ClaimsPrincipal? principal, Guid id, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var item = (await db.ScheduledSectionAnnouncements.FromSqlInterpolated(
            $"SELECT * FROM communication_announcements WHERE \"Id\" = {id} FOR UPDATE").ToListAsync(ct)).SingleOrDefault()
            ?? throw ApiFault.NotFound();
        var section = await access.Section(item.SectionId, ct);
        if (principal is not null) await access.Staff(principal, section, ct);
        else
        {
            if (!await db.Institutions.AnyAsync(x => x.Id == item.InstitutionId && x.IsActive, ct))
                throw ApiFault.Conflict("Institution inactive.");
            var author = await users.FindByIdAsync(item.AuthorId.ToString()) ?? throw ApiFault.Forbidden();
            var roles = await users.GetRolesAsync(author);
            if (!author.IsActive || !(roles.Contains(AcademixRoles.SuperAdministrator) ||
                roles.Contains(AcademixRoles.InstitutionAdministrator) && author.InstitutionId == item.InstitutionId ||
                await security.IsCurrentSectionTeacherAsync(author, roles.ToArray(), item.InstitutionId, item.SectionId, ct)))
                throw ApiFault.Forbidden();
        }
        if (item.IsCancelled || item.ScheduledForUtc > DateTime.UtcNow)
            throw ApiFault.Conflict("Cannot dispatch a cancelled or future announcement.");
        if (item.DispatchedAtUtc is DateTime already)
        {
            await tx.CommitAsync(ct);
            return new AnnouncementDispatchResult(id, 0, already);
        }
        var recipientIds = new HashSet<Guid>();
        if (item.Audience is "All" or "Students")
        {
            var enrolled = await db.StudentEnrollments.AsNoTracking()
                .Where(e => e.InstitutionId == section.InstitutionId && e.SectionId == section.Id && e.Status == "Active")
                .Select(e => e.StudentId).ToListAsync(ct);
            foreach (var studentId in enrolled) recipientIds.Add(studentId);
        }
        if (item.Audience is "All" or "Staff")
        {
            var staff = await db.TeachingAssignments.AsNoTracking().Where(a =>
                a.InstitutionId == section.InstitutionId && a.SectionId == section.Id)
                .Select(a => a.UserId).Distinct().ToListAsync(ct);
            foreach (var userId in staff) recipientIds.Add(userId);
        }

        var candidates = await db.Users.AsNoTracking().Where(u => recipientIds.Contains(u.Id) &&
            u.InstitutionId == section.InstitutionId && u.IsActive).ToListAsync(ct);
        var eligible = new List<Guid>();
        foreach (var user in candidates)
        {
            var ok = item.Audience switch
            {
                "Students" => await IsStudentMember(user, section, ct),
                "Staff" => await IsStaffMember(user, section, ct),
                _ => await IsStudentMember(user, section, ct) || await IsStaffMember(user, section, ct)
            };
            if (ok) eligible.Add(user.Id);
        }
        var optedOut = await db.CommunicationPreferences.AsNoTracking()
            .Where(p => p.InstitutionId == section.InstitutionId && eligible.Contains(p.UserId) && !p.AnnouncementNotifications)
            .Select(p => p.UserId).ToListAsync(ct);
        var muted = optedOut.ToHashSet();
        var count = 0;
        foreach (var userId in eligible.Distinct())
        {
            if (muted.Contains(userId)) continue;
            db.InAppNotifications.Add(new InAppNotification { InstitutionId = section.InstitutionId,
                RecipientId = userId, Kind = "Announcement", SourceId = item.Id, Title = item.Title, Body = item.Body });
            count++;
        }
        item.DispatchedAtUtc = DateTime.UtcNow;
        item.Version++;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new AnnouncementDispatchResult(item.Id, count, item.DispatchedAtUtc.Value);
    }
}
