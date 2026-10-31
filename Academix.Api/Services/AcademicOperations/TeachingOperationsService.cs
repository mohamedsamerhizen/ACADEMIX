using System.Security.Claims;
using System.Text.Json;
using Academix.Api.Data;
using Academix.Api.Services.Infrastructure;
using Academix.Api.Domain.AcademicOperations;
using Academix.Api.DTOs.AcademicOperations;
using Academix.Api.Middleware;
using Academix.Api.Services.Identity;
using Microsoft.EntityFrameworkCore;

namespace Academix.Api.Services.AcademicOperations;

public sealed class TeachingOperationsService(AcademixDbContext db, IdentitySecurity security, RetryingServiceWork retry)
{
    private async Task<Guid> Institution(ClaimsPrincipal principal, Guid sectionId, CancellationToken ct)
    {
        var institutionId = await db.CourseSections.AsNoTracking().Where(s => s.Id == sectionId)
            .Select(s => (Guid?)s.InstitutionId).SingleOrDefaultAsync(ct) ?? throw ApiFault.NotFound();
        await security.RequireSectionStaffAsync(principal, institutionId, sectionId, ct);
        return institutionId;
    }

    public async Task<IReadOnlyList<SlotView>> Slots(ClaimsPrincipal principal, Guid sectionId, CancellationToken ct)
    {
        var institutionId = await Institution(principal, sectionId, ct);
        return await db.TimetableSlots.AsNoTracking()
            .Where(s => s.InstitutionId == institutionId && s.SectionId == sectionId && s.IsActive)
            .OrderBy(s => s.Weekday).ThenBy(s => s.StartsAtMinute).ThenBy(s => s.Id)
            .Select(s => new SlotView(s.Id, s.SectionId, s.Weekday, s.StartsAtMinute,
                s.DurationMinutes, s.Room, s.IsActive, s.Version, s.RoomId)).ToListAsync(ct);
    }

    private static TimetableSlotState Snapshot(TimetableSlot slot)
        => new(slot.Weekday, slot.StartsAtMinute, slot.DurationMinutes,
            slot.Room, slot.RoomId, slot.IsActive, slot.Version);

    public async Task<IReadOnlyList<TimetableSlotRevisionView>> SlotHistory(ClaimsPrincipal principal,
        Guid sectionId, Guid slotId, CancellationToken ct)
    {
        var institutionId = await Institution(principal, sectionId, ct);
        if (!await db.TimetableSlots.AsNoTracking().AnyAsync(x => x.InstitutionId == institutionId &&
            x.SectionId == sectionId && x.Id == slotId, ct)) throw ApiFault.NotFound();
        var events = await db.TimetableSlotRevisions.AsNoTracking().Where(x =>
            x.InstitutionId == institutionId && x.SectionId == sectionId && x.SlotId == slotId)
            .OrderBy(x => x.AtUtc).ThenBy(x => x.Id).Take(500).ToListAsync(ct);
        return events.Select(x => new TimetableSlotRevisionView(x.Id, x.SlotId, x.Action, x.Reason,
            x.BeforeJson is null ? null : JsonSerializer.Deserialize<TimetableSlotState>(x.BeforeJson),
            JsonSerializer.Deserialize<TimetableSlotState>(x.AfterJson)!, x.ActorId, x.AtUtc)).ToList();
    }

    private async Task AddRevision(Guid institutionId, Guid sectionId, TimetableSlot slot,
        Guid actorId, string action, string reason, TimetableSlotState? before, CancellationToken ct)
    {
        var revision = new TimetableSlotRevision
        {
            InstitutionId = institutionId, SectionId = sectionId, SlotId = slot.Id,
            ActorId = actorId, Action = action, Reason = reason,
            BeforeJson = before is null ? null : JsonSerializer.Serialize(before),
            AfterJson = JsonSerializer.Serialize(Snapshot(slot))
        };
        db.TimetableSlotRevisions.Add(revision);
        // Every enrolled student receives a different durable event; never expose the staff-only reason.
        var recipients = await db.StudentEnrollments.AsNoTracking().Where(x =>
            x.InstitutionId == institutionId && x.SectionId == sectionId && x.Status == "Active")
            .Select(x => x.StudentId).Distinct().ToListAsync(ct);
        foreach (var studentId in recipients)
            OutboxService.Record(db, institutionId, "TimetableChanged", Guid.NewGuid(), new
            { StudentId = studentId, SectionId = sectionId, SlotId = slot.Id, RevisionId = revision.Id });
    }

    public Task<SlotView> SaveSlot(ClaimsPrincipal principal, Guid sectionId, Guid? slotId,
        SlotUpsert request, CancellationToken ct)
        => retry.ExecuteAsync<TeachingOperationsService, SlotView>(service => service.SaveSlotCore(principal, sectionId, slotId, request, ct), ct);

    private async Task<SlotView> SaveSlotCore(ClaimsPrincipal principal, Guid sectionId, Guid? slotId,
        SlotUpsert request, CancellationToken ct)
    {
        AcademicOperationsPolicy.Slot(request);
        var reason = CampusSchedulingRules.RevisionReason(request.ChangeReason,
            slotId.HasValue ? "Timetable slot updated" : "Timetable slot created");
        var institutionId = await Institution(principal, sectionId, ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        // Serialize institution timetable/room writers to prevent cross-section allocation races.
        if (await db.Institutions.FromSqlInterpolated(
            $"SELECT * FROM institutions WHERE \"Id\" = {institutionId} FOR UPDATE")
            .SingleOrDefaultAsync(ct) is null) throw ApiFault.NotFound();
        var section = await db.CourseSections.FromSqlInterpolated(
            $"SELECT * FROM course_sections WHERE \"Id\" = {sectionId} AND \"InstitutionId\" = {institutionId} FOR UPDATE")
            .SingleOrDefaultAsync(ct) ?? throw ApiFault.NotFound();
        TimetableSlot slot;
        if (slotId is Guid existingId)
        {
            slot = await db.TimetableSlots.SingleOrDefaultAsync(s => s.Id == existingId &&
                s.InstitutionId == institutionId && s.SectionId == sectionId, ct) ?? throw ApiFault.NotFound();
            if (request.ExpectedVersion != slot.Version) throw ApiFault.Conflict("Slot version changed.");
        }
        else
        {
            if (request.ExpectedVersion is not null) throw ApiFault.Bad("New slots cannot have ExpectedVersion.");
            slot = new TimetableSlot { InstitutionId = institutionId, SectionId = section.Id };
            db.TimetableSlots.Add(slot);
        }
        var before = slotId.HasValue ? Snapshot(slot) : null;
        // Null from a legacy client does not silently discard an already managed room assignment.
        if (request.RemoveRoomAssignment && request.RoomId.HasValue)
            throw ApiFault.Bad("Cannot assign and remove a room in one request.");
        var selectedRoomId = request.RemoveRoomAssignment ? null : request.RoomId ?? slot.RoomId;
        if (slotId.HasValue && selectedRoomId != slot.RoomId && await db.ClassSessions.AnyAsync(x =>
            x.InstitutionId == institutionId && x.SectionId == sectionId && x.TimetableSlotId == slot.Id &&
            x.Status == "Planned" && x.StartsAtUtc > DateTime.UtcNow, ct))
            throw ApiFault.Conflict("Cannot change the room while future sessions reference this slot.");
        CampusRoom? room = null;
        if (selectedRoomId.HasValue || slot.RoomId.HasValue)
        {
            // Only institution administrators can allocate or change managed physical rooms.
            await security.RequireAdminAsync(principal, institutionId, ct);
            if (selectedRoomId.HasValue)
            {
                room = await db.CampusRooms.AsNoTracking().SingleOrDefaultAsync(r =>
                    r.Id == selectedRoomId.Value && r.InstitutionId == institutionId && r.IsActive, ct)
                    ?? throw ApiFault.Bad("Managed room does not exist or is inactive.");
                if (!await db.CampusBuildings.AnyAsync(b => b.Id == room.BuildingId &&
                    b.InstitutionId == institutionId && b.IsActive, ct))
                    throw ApiFault.Conflict("The building is closed.");
                if (room.Capacity < section.Capacity)
                    throw ApiFault.Conflict("Room capacity is below the section capacity.");
            }
        }
        var overlapping = await db.TimetableSlots.AsNoTracking().Where(x => x.InstitutionId == institutionId &&
            x.IsActive && x.Id != slot.Id && x.Weekday == request.Weekday &&
            x.StartsAtMinute < request.StartsAtMinute + request.DurationMinutes &&
            request.StartsAtMinute < x.StartsAtMinute + x.DurationMinutes)
            .Select(x => new { x.SectionId, x.RoomId }).ToListAsync(ct);
        if (overlapping.Any(x => x.SectionId == sectionId))
            throw ApiFault.Conflict("A timetable slot overlaps another slot in this section.");
        if (selectedRoomId.HasValue && overlapping.Any(x => x.RoomId == selectedRoomId.Value))
            throw ApiFault.Conflict("The managed room is already reserved at that time.");
        var otherSections = overlapping.Select(x => x.SectionId).Distinct().ToArray();
        if (otherSections.Length > 0)
        {
            var instructors = await db.TeachingAssignments.AsNoTracking().Where(x =>
                x.InstitutionId == institutionId && x.SectionId == sectionId && x.Kind == "Instructor")
                .Select(x => x.UserId).ToListAsync(ct);
            if (instructors.Count > 0 && await db.TeachingAssignments.AnyAsync(x =>
                x.InstitutionId == institutionId && otherSections.Contains(x.SectionId) &&
                x.Kind == "Instructor" && instructors.Contains(x.UserId), ct))
                throw ApiFault.Conflict("An instructor has overlapping timetable assignments.");
            var students = await db.StudentEnrollments.AsNoTracking().Where(x =>
                x.InstitutionId == institutionId && x.SectionId == sectionId && x.Status == "Active")
                .Select(x => x.StudentId).ToListAsync(ct);
            if (students.Count > 0 && await db.StudentEnrollments.AnyAsync(x =>
                x.InstitutionId == institutionId && otherSections.Contains(x.SectionId) &&
                x.Status == "Active" && students.Contains(x.StudentId), ct))
                throw ApiFault.Conflict("A student is enrolled in overlapping sections.");
        }
        slot.Weekday = request.Weekday;
        slot.StartsAtMinute = request.StartsAtMinute;
        slot.DurationMinutes = request.DurationMinutes;
        slot.RoomId = selectedRoomId;
        slot.Room = room?.Name ?? request.Room?.Trim() ?? "";
        slot.IsActive = true;
        if (before is not null && CampusSchedulingRules.SameSlot(before, Snapshot(slot)))
        {
            await tx.CommitAsync(ct);
            return new SlotView(slot.Id, slot.SectionId, slot.Weekday,
                slot.StartsAtMinute, slot.DurationMinutes, slot.Room, slot.IsActive, slot.Version, slot.RoomId);
        }
        if (slotId.HasValue) slot.Version++;
        var actor = await security.RequireSectionStaffAsync(principal, institutionId, sectionId, ct);
        await AddRevision(institutionId, sectionId, slot, actor.Id,
            slotId.HasValue ? "Updated" : "Created", reason, before, ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new SlotView(slot.Id, slot.SectionId, slot.Weekday,
            slot.StartsAtMinute, slot.DurationMinutes, slot.Room, slot.IsActive, slot.Version, slot.RoomId);
    }

    public Task DeactivateSlot(ClaimsPrincipal principal, Guid sectionId, Guid slotId,
        long expectedVersion, string? reason, CancellationToken ct)
        => retry.ExecuteAsync<TeachingOperationsService>(service =>
            service.DeactivateSlotCore(principal, sectionId, slotId, expectedVersion, reason, ct), ct);

    private async Task DeactivateSlotCore(ClaimsPrincipal principal, Guid sectionId, Guid slotId,
        long expectedVersion, string? reason, CancellationToken ct)
    {
        var validatedReason = CampusSchedulingRules.RevisionReason(reason, "Timetable slot deactivated");
        var institutionId = await Institution(principal, sectionId, ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        // Match SaveSlot lock order to prevent deactivation racing a room or timetable reassignment.
        if (await db.Institutions.FromSqlInterpolated(
            $"SELECT * FROM institutions WHERE \"Id\" = {institutionId} FOR UPDATE")
            .SingleOrDefaultAsync(ct) is null) throw ApiFault.NotFound();
        var slot = await db.TimetableSlots.FromSqlInterpolated(
            $"SELECT * FROM academic_timetable_slots WHERE \"Id\" = {slotId} AND \"SectionId\" = {sectionId} AND \"InstitutionId\" = {institutionId} FOR UPDATE")
            .SingleOrDefaultAsync(ct) ?? throw ApiFault.NotFound();
        if (slot.Version != expectedVersion) throw ApiFault.Conflict("Slot version changed.");
        if (!slot.IsActive) throw ApiFault.Conflict("Slot is already inactive.");
        if (await db.ClassSessions.AnyAsync(x => x.InstitutionId == institutionId &&
            x.SectionId == sectionId && x.TimetableSlotId == slotId &&
            x.Status == "Planned" && x.StartsAtUtc > DateTime.UtcNow, ct))
            throw ApiFault.Conflict("Resolve future planned sessions before deactivating this slot.");
        var actor = await security.RequireSectionStaffAsync(principal, institutionId, sectionId, ct);
        var before = Snapshot(slot);
        slot.IsActive = false;
        slot.Version++;
        await AddRevision(institutionId, sectionId, slot, actor.Id,
            "Deactivated", validatedReason, before, ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    public async Task<IReadOnlyList<SessionView>> Sessions(ClaimsPrincipal principal, Guid sectionId,
        DateTime fromUtc, DateTime toUtc, CancellationToken ct)
    {
        AcademicOperationsPolicy.TimeRange(fromUtc, toUtc);
        var institutionId = await Institution(principal, sectionId, ct);
        return await db.ClassSessions.AsNoTracking().Where(x => x.InstitutionId == institutionId &&
            x.SectionId == sectionId && x.StartsAtUtc < toUtc && x.EndsAtUtc > fromUtc)
            .OrderBy(x => x.StartsAtUtc).ThenBy(x => x.Id)
            .Select(x => new SessionView(x.Id, x.SectionId, x.StartsAtUtc, x.EndsAtUtc,
                x.Topic, x.Status, x.TimetableSlotId, x.Version, x.RoomId)).Take(500).ToListAsync(ct);
    }

    private static ClassSessionState SessionSnapshot(ClassSession session)
        => new(session.StartsAtUtc, session.EndsAtUtc, session.Topic, session.Status,
            session.TimetableSlotId, session.Version, session.RoomId);

    /// <summary>Must run within the institution-wide transaction lock for every writer.</summary>
    private async Task<CampusRoom?> ValidateRoom(Guid institutionId, Guid sectionId,
        Guid? roomId, CancellationToken ct)
    {
        if (!roomId.HasValue) return null;
        var room = await db.CampusRooms.AsNoTracking().SingleOrDefaultAsync(x =>
            x.Id == roomId.Value && x.InstitutionId == institutionId && x.IsActive, ct)
            ?? throw ApiFault.Conflict("The selected room is unavailable.");
        if (!await db.CampusBuildings.AsNoTracking().AnyAsync(x =>
            x.Id == room.BuildingId && x.InstitutionId == institutionId && x.IsActive, ct))
            throw ApiFault.Conflict("The selected room's building is closed.");
        var capacity = await db.CourseSections.AsNoTracking().Where(x =>
            x.Id == sectionId && x.InstitutionId == institutionId).Select(x => x.Capacity).SingleAsync(ct);
        if (room.Capacity < capacity) throw ApiFault.Conflict("Room capacity is below section capacity.");
        return room;
    }

    private async Task<Guid?> SlotRoom(Guid institutionId, Guid sectionId, Guid? slotId, CancellationToken ct)
    {
        if (!slotId.HasValue) return null;
        var slot = await db.TimetableSlots.AsNoTracking().Where(x =>
            x.Id == slotId.Value && x.InstitutionId == institutionId && x.SectionId == sectionId && x.IsActive)
            .Select(x => new { x.RoomId }).SingleOrDefaultAsync(ct)
            ?? throw ApiFault.Conflict("The linked timetable slot is unavailable.");
        return slot.RoomId;
    }

    /// <summary>Caller holds the institution lock. Covers persisted standalone and linked allocations.</summary>
    private async Task EnsureSessionAvailability(Guid institutionId, Guid sectionId, Guid? roomId,
        Guid? ignoreSessionId, DateTime startUtc, DateTime endUtc, CancellationToken ct)
    {
        var overlaps = await db.ClassSessions.AsNoTracking().Where(x =>
                x.InstitutionId == institutionId && x.Status != "Cancelled" &&
                (!ignoreSessionId.HasValue || x.Id != ignoreSessionId.Value) &&
                x.StartsAtUtc < endUtc && x.EndsAtUtc > startUtc)
            .Select(x => new { x.SectionId, x.RoomId, x.TimetableSlotId }).ToListAsync(ct);
        if (overlaps.Any(x => x.SectionId == sectionId))
            throw ApiFault.Conflict("Session overlaps another active session in this section.");

        if (roomId.HasValue)
        {
            if (overlaps.Any(x => x.RoomId == roomId.Value))
                throw ApiFault.Conflict("Managed room already has an active session at that time.");
            // Protect legacy sessions with no persisted room, which may predate the backfill.
            var legacySlots = overlaps.Where(x => !x.RoomId.HasValue && x.TimetableSlotId.HasValue)
                .Select(x => x.TimetableSlotId.GetValueOrDefault()).Distinct().ToArray();
            if (legacySlots.Length > 0 && await db.TimetableSlots.AsNoTracking().AnyAsync(x =>
                x.InstitutionId == institutionId && legacySlots.Contains(x.Id) && x.RoomId == roomId, ct))
                throw ApiFault.Conflict("Managed room is occupied by a linked legacy session.");
        }

        // Dated maintenance and physical-exam room reservations share the institution lock.
        var bookedExams = await db.CampusRoomReservations.AsNoTracking().Where(r =>
            r.InstitutionId == institutionId && r.Kind == "Exam" && r.Status == "Active" &&
            r.StartsAtUtc < endUtc && r.EndsAtUtc > startUtc)
            .Select(r => new { r.SectionId, r.InvigilatorId }).ToListAsync(ct);
        if (roomId.HasValue && await db.CampusRoomReservations.AnyAsync(r =>
            r.InstitutionId == institutionId && r.RoomId == roomId.Value && r.Status == "Active" &&
            r.StartsAtUtc < endUtc && r.EndsAtUtc > startUtc, ct))
            throw ApiFault.Conflict("Managed room is closed or reserved for a physical exam.");
        if (bookedExams.Any(r => r.SectionId == sectionId))
            throw ApiFault.Conflict("Section has a physical exam at that time.");
        var overlappingExamSections = bookedExams.Where(x => x.SectionId.HasValue)
            .Select(x => x.SectionId.GetValueOrDefault()).Distinct().ToArray();
        var sections = overlaps.Select(x => x.SectionId).Concat(overlappingExamSections).Distinct().ToArray();
        if (sections.Length == 0) return;
        var instructors = await db.TeachingAssignments.AsNoTracking().Where(x =>
            x.InstitutionId == institutionId && x.SectionId == sectionId && x.Kind == "Instructor")
            .Select(x => x.UserId).ToArrayAsync(ct);
        if (instructors.Length > 0 && await db.TeachingAssignments.AnyAsync(x =>
            x.InstitutionId == institutionId && sections.Contains(x.SectionId) &&
            x.Kind == "Instructor" && instructors.Contains(x.UserId), ct))
            throw ApiFault.Conflict("Instructor is already scheduled in another session.");
        var students = await db.StudentEnrollments.AsNoTracking().Where(x =>
            x.InstitutionId == institutionId && x.SectionId == sectionId && x.Status == "Active")
            .Select(x => x.StudentId).ToArrayAsync(ct);
        if (students.Length > 0 && await db.StudentEnrollments.AnyAsync(x =>
            x.InstitutionId == institutionId && sections.Contains(x.SectionId) &&
            x.Status == "Active" && students.Contains(x.StudentId), ct))
            throw ApiFault.Conflict("Student is already scheduled in another session.");
    }

    public async Task<IReadOnlyList<ClassSessionRevisionView>> SessionHistory(ClaimsPrincipal principal,
        Guid sessionId, CancellationToken ct)
    {
        var session = await db.ClassSessions.AsNoTracking().Where(x => x.Id == sessionId)
            .Select(x => new { x.InstitutionId, x.SectionId }).SingleOrDefaultAsync(ct)
            ?? throw ApiFault.NotFound();
        await security.RequireSectionStaffAsync(principal, session.InstitutionId, session.SectionId, ct);
        var revisions = await db.ClassSessionRevisions.AsNoTracking().Where(x =>
            x.InstitutionId == session.InstitutionId && x.SectionId == session.SectionId && x.SessionId == sessionId)
            .OrderBy(x => x.AtUtc).ThenBy(x => x.Id).Take(500).ToListAsync(ct);
        return revisions.Select(x => new ClassSessionRevisionView(x.Id, x.SessionId, x.Reason,
            JsonSerializer.Deserialize<ClassSessionState>(x.BeforeJson)!,
            JsonSerializer.Deserialize<ClassSessionState>(x.AfterJson)!, x.ActorId, x.AtUtc)).ToArray();
    }

    public Task<SessionView> RescheduleSession(ClaimsPrincipal principal, Guid sessionId,
        SessionRescheduleInput request, CancellationToken ct)
        => retry.ExecuteAsync<TeachingOperationsService, SessionView>(service =>
            service.RescheduleSessionCore(principal, sessionId, request, ct), ct);

    private async Task<SessionView> RescheduleSessionCore(ClaimsPrincipal principal, Guid sessionId,
        SessionRescheduleInput request, CancellationToken ct)
    {
        var scope = await db.ClassSessions.AsNoTracking().Where(x => x.Id == sessionId)
            .Select(x => new { x.InstitutionId, x.SectionId }).SingleOrDefaultAsync(ct)
            ?? throw ApiFault.NotFound();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        // Same lock order as timetable writers and session creators: institution, then target session.
        if (await db.Institutions.FromSqlInterpolated($"SELECT * FROM institutions WHERE \"Id\" = {scope.InstitutionId} FOR UPDATE")
            .SingleOrDefaultAsync(ct) is null) throw ApiFault.NotFound();
        var actor=await security.RequireSectionStaffAsync(principal,scope.InstitutionId,scope.SectionId,ct);
        var session = await db.ClassSessions.FromSqlInterpolated($"SELECT * FROM academic_class_sessions WHERE \"Id\" = {sessionId} AND \"InstitutionId\" = {scope.InstitutionId} FOR UPDATE")
            .SingleOrDefaultAsync(ct) ?? throw ApiFault.NotFound();
        if (session.Version != request.ExpectedVersion) throw ApiFault.Conflict("Session version changed.");
        if (session.Status != "Planned") throw ApiFault.Conflict("Only planned sessions may be rescheduled.");
        if (session.StartsAtUtc <= DateTime.UtcNow)
            throw ApiFault.Conflict("A session that has started cannot be rescheduled.");
        if (await db.AttendanceRecords.AnyAsync(x => x.InstitutionId == scope.InstitutionId &&
            x.SessionId == sessionId, ct)) throw ApiFault.Conflict("Cannot reschedule recorded attendance.");
        var topic = request.Topic?.Trim() ?? session.Topic;
        CampusSchedulingRules.Reschedule(DateTime.UtcNow, request.StartsAtUtc, request.EndsAtUtc,
            topic, request.Reason);
        var slotRoom = await SlotRoom(scope.InstitutionId, scope.SectionId, session.TimetableSlotId, ct);
        var newRoomId = CampusSchedulingRules.ResolveSessionRoom(session.TimetableSlotId,
            slotRoom, session.RoomId, request.RoomId, request.RemoveRoomAssignment);
        if (newRoomId != session.RoomId && !session.TimetableSlotId.HasValue)
            await security.RequireAdminAsync(principal, scope.InstitutionId, ct);
        await ValidateRoom(scope.InstitutionId, scope.SectionId, newRoomId, ct);
        var before = SessionSnapshot(session);
        if (before.StartsAtUtc == request.StartsAtUtc && before.EndsAtUtc == request.EndsAtUtc &&
            before.Topic == topic && before.RoomId == newRoomId)
        {
            await tx.CommitAsync(ct);
            return View(session);
        }
        await EnsureSessionAvailability(scope.InstitutionId, scope.SectionId, newRoomId,
            session.Id, request.StartsAtUtc, request.EndsAtUtc, ct);
        session.StartsAtUtc = request.StartsAtUtc;
        session.EndsAtUtc = request.EndsAtUtc;
        session.Topic = topic;
        session.RoomId = newRoomId;
        session.Version++;
        var revision = new ClassSessionRevision
        {
            InstitutionId = scope.InstitutionId, SectionId = scope.SectionId, SessionId = session.Id,
            ActorId = actor.Id, Reason = request.Reason.Trim(),
            BeforeJson = JsonSerializer.Serialize(before),
            AfterJson = JsonSerializer.Serialize(SessionSnapshot(session))
        };
        db.ClassSessionRevisions.Add(revision);
        var studentIds = await db.StudentEnrollments.AsNoTracking().Where(x =>
            x.InstitutionId == scope.InstitutionId && x.SectionId == scope.SectionId && x.Status == "Active")
            .Select(x => x.StudentId).Distinct().ToListAsync(ct);
        foreach (var studentId in studentIds)
            OutboxService.Record(db, scope.InstitutionId, "ClassSessionRescheduled", Guid.NewGuid(), new
            { StudentId = studentId, SectionId = scope.SectionId, SessionId = session.Id, RevisionId = revision.Id });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return View(session);
    }

    public Task<SessionView> CreateSession(ClaimsPrincipal principal, Guid sectionId,
        SessionCreate request, CancellationToken ct)
        => retry.ExecuteAsync<TeachingOperationsService, SessionView>(service => service.CreateSessionCore(principal, sectionId, request, ct), ct);

    private async Task<SessionView> CreateSessionCore(ClaimsPrincipal principal, Guid sectionId,
        SessionCreate request, CancellationToken ct)
    {
        AcademicOperationsPolicy.Interval(request.StartsAtUtc, request.EndsAtUtc, 1);
        if (request.EndsAtUtc - request.StartsAtUtc > TimeSpan.FromHours(12) ||
            request.Topic is null || request.Topic.Trim().Length is < 2 or > 200)
            throw ApiFault.Bad("Invalid session topic or duration.");
        var institutionId = await Institution(principal, sectionId, ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        if (await db.Institutions.FromSqlInterpolated(
            $"SELECT * FROM institutions WHERE \"Id\" = {institutionId} FOR UPDATE")
            .SingleOrDefaultAsync(ct) is null) throw ApiFault.NotFound();
        var section = await db.CourseSections.FromSqlInterpolated(
            $"SELECT * FROM course_sections WHERE \"Id\" = {sectionId} AND \"InstitutionId\" = {institutionId} FOR UPDATE")
            .SingleOrDefaultAsync(ct) ?? throw ApiFault.NotFound();
        await security.RequireSectionStaffAsync(principal,institutionId,sectionId,ct);
        var slotRoom = await SlotRoom(institutionId, sectionId, request.TimetableSlotId, ct);
        var assignedRoomId = CampusSchedulingRules.ResolveSessionRoom(request.TimetableSlotId,
            slotRoom, null, request.RoomId, false);
        if (request.RoomId.HasValue && !request.TimetableSlotId.HasValue)
            await security.RequireAdminAsync(principal, institutionId, ct);
        await ValidateRoom(institutionId, sectionId, assignedRoomId, ct);
        await EnsureSessionAvailability(institutionId, sectionId, assignedRoomId,
            null, request.StartsAtUtc, request.EndsAtUtc, ct);
        var session = new ClassSession { InstitutionId = institutionId, SectionId = sectionId,
            TimetableSlotId = request.TimetableSlotId, RoomId = assignedRoomId,
            StartsAtUtc = request.StartsAtUtc,
            EndsAtUtc = request.EndsAtUtc, Topic = request.Topic.Trim() };
        db.ClassSessions.Add(session);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return View(session);
    }

    public async Task<SessionView> SetSessionStatus(ClaimsPrincipal principal, Guid sessionId,
        string status, long expectedVersion, CancellationToken ct)
    {
        if (status is not ("Completed" or "Cancelled")) throw ApiFault.Bad("Invalid session status.");
        var session = await db.ClassSessions.SingleOrDefaultAsync(x => x.Id == sessionId, ct) ?? throw ApiFault.NotFound();
        await security.RequireSectionStaffAsync(principal, session.InstitutionId, session.SectionId, ct);
        if (session.Version != expectedVersion) throw ApiFault.Conflict("Session version changed.");
        if (session.Status != "Planned") throw ApiFault.Conflict("Session has already been finalized.");
        if (status == "Completed" && DateTime.UtcNow < session.StartsAtUtc)
            throw ApiFault.Conflict("A session cannot be completed before it starts.");
        if (status == "Cancelled" && await db.AttendanceRecords.AnyAsync(x =>
            x.InstitutionId == session.InstitutionId && x.SessionId == session.Id, ct))
            throw ApiFault.Conflict("A session with recorded attendance cannot be cancelled.");
        session.Status = status; session.Version++;
        await db.SaveChangesAsync(ct);
        return View(session);
    }

    public Task<IReadOnlyList<AttendanceView>> RecordAttendance(ClaimsPrincipal principal,
        Guid sessionId, AttendanceBatch request, CancellationToken ct)
        => retry.ExecuteAsync<TeachingOperationsService, IReadOnlyList<AttendanceView>>(service => service.RecordAttendanceCore(principal, sessionId, request, ct), ct);

    private async Task<IReadOnlyList<AttendanceView>> RecordAttendanceCore(ClaimsPrincipal principal,
        Guid sessionId, AttendanceBatch request, CancellationToken ct)
    {
        AcademicOperationsPolicy.AttendanceBatch(request);
        var scope = await db.ClassSessions.AsNoTracking().Where(x => x.Id == sessionId)
            .Select(x => new { x.InstitutionId, x.SectionId }).SingleOrDefaultAsync(ct) ?? throw ApiFault.NotFound();
        var user = await security.RequireSectionStaffAsync(principal, scope.InstitutionId, scope.SectionId, ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var session = await db.ClassSessions.FromSqlInterpolated(
            $"SELECT * FROM academic_class_sessions WHERE \"Id\" = {sessionId} AND \"InstitutionId\" = {scope.InstitutionId} FOR UPDATE")
            .SingleOrDefaultAsync(ct) ?? throw ApiFault.NotFound();
        if (session.Status == "Cancelled" || DateTime.UtcNow < session.StartsAtUtc)
            throw ApiFault.Conflict("Attendance is not open for this session.");
        var studentIds = request.Records.Select(x => x.StudentId).ToArray();
        var enrolled = await db.StudentEnrollments.AsNoTracking()
            .Where(x => x.InstitutionId == scope.InstitutionId && x.SectionId == scope.SectionId &&
                x.Status == "Active" && studentIds.Contains(x.StudentId))
            .Select(x => x.StudentId).ToListAsync(ct);
        if (enrolled.Distinct().Count() != studentIds.Length)
            throw ApiFault.Bad("Every attendance entry must belong to an active section enrollment.");
        var records = await db.AttendanceRecords.Where(x => x.InstitutionId == scope.InstitutionId &&
            x.SessionId == sessionId && studentIds.Contains(x.StudentId)).ToListAsync(ct);
        foreach (var input in request.Records)
        {
            var record = records.SingleOrDefault(x => x.StudentId == input.StudentId);
            if (record is null)
            {
                record = new AttendanceRecord { InstitutionId = scope.InstitutionId,
                    SectionId = scope.SectionId, SessionId = sessionId, StudentId = input.StudentId };
                db.AttendanceRecords.Add(record);
                records.Add(record);
            }
            AttendanceRecordMutation.Apply(record,input.Status,input.Note.Trim(),user.Id,DateTime.UtcNow);
        }
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return records.OrderBy(x => x.StudentId).Select(x => new AttendanceView(
            x.StudentId, x.Status, x.Note, x.RecordedAtUtc)).ToArray();
    }

    public async Task<IReadOnlyList<AttendanceView>> SessionAttendance(ClaimsPrincipal principal,
        Guid sessionId, CancellationToken ct)
    {
        var scope = await db.ClassSessions.AsNoTracking().Where(x => x.Id == sessionId)
            .Select(x => new { x.InstitutionId, x.SectionId }).SingleOrDefaultAsync(ct) ?? throw ApiFault.NotFound();
        await security.RequireSectionStaffAsync(principal, scope.InstitutionId, scope.SectionId, ct);
        return await db.AttendanceRecords.AsNoTracking().Where(x => x.InstitutionId == scope.InstitutionId &&
            x.SessionId == sessionId && x.SectionId == scope.SectionId)
            .OrderBy(x => x.StudentId).Select(x => new AttendanceView(
                x.StudentId, x.Status, x.Note, x.RecordedAtUtc)).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<AttendanceSummary>> SectionAttendance(ClaimsPrincipal principal,
        Guid sectionId, CancellationToken ct)
    {
        var institutionId = await Institution(principal, sectionId, ct);
        var sessionIds = db.ClassSessions.Where(x => x.InstitutionId == institutionId &&
            x.SectionId == sectionId && x.Status != "Cancelled" && x.StartsAtUtc <= DateTime.UtcNow)
            .Select(x => x.Id);
        var total = await sessionIds.CountAsync(ct);
        // List EVERY active student, including those with no attendance entries yet.
        // Unrecorded attendance remains unrecorded; it is not silently classified Absent.
        return await db.StudentEnrollments.AsNoTracking()
            .Where(e => e.InstitutionId == institutionId && e.SectionId == sectionId && e.Status == "Active")
            .OrderBy(e => e.StudentId)
            .Select(e => new AttendanceSummary(e.StudentId, sectionId, total,
                db.AttendanceRecords.Count(a => a.StudentId == e.StudentId &&
                    a.InstitutionId == institutionId && a.SectionId == sectionId &&
                    sessionIds.Contains(a.SessionId) && a.Status == "Present"),
                db.AttendanceRecords.Count(a => a.StudentId == e.StudentId &&
                    a.InstitutionId == institutionId && a.SectionId == sectionId &&
                    sessionIds.Contains(a.SessionId) && a.Status == "Late"),
                db.AttendanceRecords.Count(a => a.StudentId == e.StudentId &&
                    a.InstitutionId == institutionId && a.SectionId == sectionId &&
                    sessionIds.Contains(a.SessionId) && a.Status == "Absent"),
                db.AttendanceRecords.Count(a => a.StudentId == e.StudentId &&
                    a.InstitutionId == institutionId && a.SectionId == sectionId &&
                    sessionIds.Contains(a.SessionId) && a.Status == "Excused")))
            .ToListAsync(ct);
    }

    private static SessionView View(ClassSession x) => new(x.Id, x.SectionId, x.StartsAtUtc,
        x.EndsAtUtc, x.Topic, x.Status, x.TimetableSlotId, x.Version, x.RoomId);
}
