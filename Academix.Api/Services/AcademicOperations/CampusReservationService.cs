using System.Security.Claims;
using Academix.Api.Data;
using Academix.Api.Domain.AcademicOperations;
using Academix.Api.Domain.Identity;
using Academix.Api.DTOs.AcademicOperations;
using Academix.Api.Middleware;
using Academix.Api.Services.Identity;
using Academix.Api.Services.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Academix.Api.Services.AcademicOperations;

/// <summary>Institution-serialized concrete room allocations; no changes to online exam attempt scheduling.</summary>
public sealed class CampusReservationService(AcademixDbContext db, IdentitySecurity security,
    RetryingServiceWork retry)
{
    private static CampusReservationView View(CampusRoomReservation r) => new(r.Id, r.InstitutionId,
        r.RoomId, r.Kind, r.SectionId, r.ExamId, r.InvigilatorId, r.SeatCount,
        r.StartsAtUtc, r.EndsAtUtc, r.Status, r.Version, r.CreatedBy, r.CreatedAtUtc,
        r.CancelledBy, r.CancelledAtUtc);

    public Task<CampusReservationView> Create(ClaimsPrincipal principal, Guid institutionId,
        CampusReservationCreate request, CancellationToken ct)
        => retry.ExecuteAsync<CampusReservationService, CampusReservationView>(service =>
            service.CreateCore(principal, institutionId, request, ct), ct);

    private async Task<CampusReservationView> CreateCore(ClaimsPrincipal principal, Guid institutionId,
        CampusReservationCreate request, CancellationToken ct)
    {
        CampusReservationPolicy.Validate(request, DateTime.MinValue);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        if (await db.Institutions.FromSqlInterpolated(
            $"SELECT * FROM institutions WHERE \"Id\" = {institutionId} FOR UPDATE")
            .SingleOrDefaultAsync(ct) is null) throw ApiFault.NotFound();
        var actor = await security.RequireAdminAsync(principal, institutionId, ct);
        var existing = await db.CampusRoomReservations.AsNoTracking().SingleOrDefaultAsync(x =>
            x.InstitutionId == institutionId && x.ClientRequestId == request.ClientRequestId, ct);
        if (existing is not null)
        {
            if (!CampusReservationPolicy.Matches(request, existing))
                throw ApiFault.Conflict("Idempotency key was used for a different allocation.");
            return View(existing);
        }
        CampusReservationPolicy.Validate(request, DateTime.UtcNow);
        var room = await db.CampusRooms.AsNoTracking().SingleOrDefaultAsync(x =>
            x.InstitutionId == institutionId && x.Id == request.RoomId && x.IsActive, ct)
            ?? throw ApiFault.Bad("Room unavailable.");
        if (!await db.CampusBuildings.AnyAsync(x => x.Id == room.BuildingId &&
            x.InstitutionId == institutionId && x.IsActive, ct))
            throw ApiFault.Conflict("Building unavailable.");
        if (await db.CampusRoomReservations.AnyAsync(x => x.InstitutionId == institutionId &&
            x.RoomId == room.Id && x.Status == "Active" &&
            x.StartsAtUtc < request.EndsAtUtc && x.EndsAtUtc > request.StartsAtUtc, ct))
            throw ApiFault.Conflict("Room is already allocated or closed in this interval.");
        // Existing sessions may have an explicit room or a legacy slot-backed room.
        if (await db.ClassSessions.AnyAsync(s => s.InstitutionId == institutionId &&
            s.Status != "Cancelled" && s.StartsAtUtc < request.EndsAtUtc &&
            s.EndsAtUtc > request.StartsAtUtc && (s.RoomId == room.Id ||
                (s.RoomId == null && s.TimetableSlotId.HasValue &&
                 db.TimetableSlots.Any(slot => slot.Id == s.TimetableSlotId &&
                     slot.InstitutionId == institutionId && slot.SectionId == s.SectionId &&
                     slot.RoomId == room.Id))), ct))
            throw ApiFault.Conflict("Room has an existing class session. Reschedule it before closing or reserving.");

        Guid? sectionId = null;
        if (request.Kind == "Exam")
        {
            var exam = await db.Exams.AsNoTracking().SingleOrDefaultAsync(x =>
                x.Id == request.ExamId && x.InstitutionId == institutionId && x.Status == "Published", ct)
                ?? throw ApiFault.Bad("Only a published examination in this institution can have a physical venue.");
            sectionId = exam.SectionId;
            var section = await db.CourseSections.AsNoTracking().SingleOrDefaultAsync(x =>
                x.Id == exam.SectionId && x.InstitutionId == institutionId && x.CourseId == exam.CourseId, ct)
                ?? throw ApiFault.Conflict("Exam section is unavailable.");
            if (request.SeatCount > room.Capacity || request.SeatCount > section.Capacity)
                throw ApiFault.Conflict("Reserved exam seats exceed room or section capacity.");
            var enrolledCount = await db.StudentEnrollments.CountAsync(x =>
                x.InstitutionId == institutionId && x.SectionId == section.Id && x.Status == "Active", ct);
            if (request.SeatCount < enrolledCount)
                throw ApiFault.Conflict("One physical exam venue must accommodate all currently enrolled students.");
            // An invigilator must belong to the actual section's current teaching staff.
            if (!await db.Users.AsNoTracking().AnyAsync(x => x.Id == request.InvigilatorId &&
                x.InstitutionId == institutionId && x.IsActive, ct))
                throw ApiFault.Bad("Invigilator must be an active institution user.");
            var currentRoles = await (from assignment in db.UserRoles
                join role in db.Roles on assignment.RoleId equals role.Id
                where assignment.UserId == request.InvigilatorId
                select role.Name!).ToArrayAsync(ct);
            var allowedKinds = TeachingRolePolicy.ActiveAssignmentKinds(currentRoles);
            if (allowedKinds.Length == 0 || !await db.TeachingAssignments.AnyAsync(x =>
                x.InstitutionId == institutionId && x.SectionId == exam.SectionId &&
                x.UserId == request.InvigilatorId && allowedKinds.Contains(x.Kind), ct))
                throw ApiFault.Bad("Invigilator must retain the correct teaching role and active section assignment.");
            if (await db.CampusRoomReservations.AnyAsync(x => x.InstitutionId == institutionId &&
                x.Status == "Active" && x.Kind == "Exam" &&
                x.StartsAtUtc < request.EndsAtUtc && x.EndsAtUtc > request.StartsAtUtc &&
                (x.SectionId == sectionId || x.InvigilatorId == request.InvigilatorId), ct))
                throw ApiFault.Conflict("Section or invigilator has another physical exam at this time.");
            var sessions = await db.ClassSessions.AsNoTracking().Where(x =>
                x.InstitutionId == institutionId && x.Status != "Cancelled" &&
                x.StartsAtUtc < request.EndsAtUtc && x.EndsAtUtc > request.StartsAtUtc)
                .Select(x => x.SectionId).Distinct().ToArrayAsync(ct);
            var examSections = await db.CampusRoomReservations.AsNoTracking().Where(x =>
                x.InstitutionId == institutionId && x.Status == "Active" && x.Kind == "Exam" &&
                x.SectionId.HasValue && x.StartsAtUtc < request.EndsAtUtc &&
                x.EndsAtUtc > request.StartsAtUtc)
                .Select(x => x.SectionId.GetValueOrDefault()).Distinct().ToArrayAsync(ct);
            var otherSections = sessions.Concat(examSections).Where(x => x != exam.SectionId).Distinct().ToArray();
            if (sessions.Contains(exam.SectionId))
                throw ApiFault.Conflict("Section has a class session during this examination.");
            if (otherSections.Length > 0)
            {
                var students = await db.StudentEnrollments.AsNoTracking().Where(x =>
                    x.InstitutionId == institutionId && x.SectionId == sectionId && x.Status == "Active")
                    .Select(x => x.StudentId).ToArrayAsync(ct);
                if (students.Length > 0 && await db.StudentEnrollments.AnyAsync(x =>
                    x.InstitutionId == institutionId && otherSections.Contains(x.SectionId) &&
                    x.Status == "Active" && students.Contains(x.StudentId), ct))
                    throw ApiFault.Conflict("A student has another class or physical exam at this time.");
                if (await db.TeachingAssignments.AnyAsync(x => x.InstitutionId == institutionId &&
                    otherSections.Contains(x.SectionId) && x.Kind == "Instructor" &&
                    x.UserId == request.InvigilatorId, ct))
                    throw ApiFault.Conflict("Invigilator is teaching another section at this time.");
            }
        }
        var reservation = new CampusRoomReservation
        {
            InstitutionId = institutionId, RoomId = room.Id, Kind = request.Kind,
            SectionId = sectionId, ExamId = request.ExamId, InvigilatorId = request.InvigilatorId,
            SeatCount = request.SeatCount, StartsAtUtc = request.StartsAtUtc,
            EndsAtUtc = request.EndsAtUtc, Reason = request.Reason.Trim(),
            ClientRequestId = request.ClientRequestId, CreatedBy = actor.Id
        };
        db.CampusRoomReservations.Add(reservation);
        if (sectionId.HasValue) await NotifyStudents(reservation, "PhysicalExamVenueChanged", ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return View(reservation);
    }

    private async Task NotifyStudents(CampusRoomReservation row, string eventKind, CancellationToken ct)
    {
        if (!row.SectionId.HasValue) return;
        var students = await db.StudentEnrollments.AsNoTracking().Where(x =>
            x.InstitutionId == row.InstitutionId && x.SectionId == row.SectionId.GetValueOrDefault() && x.Status == "Active")
            .Select(x => x.StudentId).Distinct().ToListAsync(ct);
        foreach (var studentId in students)
            OutboxService.Record(db, row.InstitutionId, eventKind, Guid.NewGuid(), new
            {
                StudentId = studentId, SectionId = row.SectionId, ExamId = row.ExamId,
                VenueBookingId = row.Id, Status = row.Status
            });
    }

    public Task<CampusReservationView> Cancel(ClaimsPrincipal principal, Guid institutionId, Guid id,
        CampusReservationCancel request, CancellationToken ct)
        => retry.ExecuteAsync<CampusReservationService, CampusReservationView>(service =>
            service.CancelCore(principal, institutionId, id, request, ct), ct);

    private async Task<CampusReservationView> CancelCore(ClaimsPrincipal principal, Guid institutionId,
        Guid id, CampusReservationCancel request, CancellationToken ct)
    {
        CampusReservationPolicy.ValidateCancel(request);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        if (await db.Institutions.FromSqlInterpolated(
            $"SELECT * FROM institutions WHERE \"Id\" = {institutionId} FOR UPDATE")
            .SingleOrDefaultAsync(ct) is null) throw ApiFault.NotFound();
        var actor = await security.RequireAdminAsync(principal, institutionId, ct);
        var row = await db.CampusRoomReservations.SingleOrDefaultAsync(x =>
            x.Id == id && x.InstitutionId == institutionId, ct) ?? throw ApiFault.NotFound();
        if (row.Status == "Cancelled") return View(row);
        if (row.StartsAtUtc <= DateTime.UtcNow)
            throw ApiFault.Conflict("A reservation already in progress cannot be cancelled retroactively.");
        if (row.Version != request.ExpectedVersion) throw ApiFault.Conflict("Reservation version changed.");
        row.Status = "Cancelled";
        row.Version++;
        row.CancelledBy = actor.Id;
        row.CancelledAtUtc = DateTime.UtcNow;
        row.CancellationReason = request.Reason.Trim();
        if (row.Kind == "Exam") await NotifyStudents(row, "PhysicalExamVenueChanged", ct);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return View(row);
    }

    public async Task<IReadOnlyList<CampusReservationView>> List(ClaimsPrincipal principal, Guid institutionId,
        Guid? roomId, DateTime fromUtc, DateTime toUtc, int page, CancellationToken ct)
    {
        await security.RequireAdminAsync(principal, institutionId, ct);
        CampusSchedulingRules.OccupancyInterval(fromUtc, toUtc);
        if (page is < 1 or > 10000) throw ApiFault.Bad("Invalid page.");
        var query = db.CampusRoomReservations.AsNoTracking().Where(x =>
            x.InstitutionId == institutionId && x.StartsAtUtc < toUtc && x.EndsAtUtc > fromUtc);
        if (roomId.HasValue) query = query.Where(x => x.RoomId == roomId.Value);
        var items = await query.OrderBy(x => x.StartsAtUtc).ThenBy(x => x.Id)
            .Skip((page - 1) * 50).Take(50).ToListAsync(ct);
        return items.Select(View).ToArray();
    }

    public async Task<IReadOnlyList<StudentExamVenueView>> MyExamVenues(ClaimsPrincipal principal,
        CancellationToken ct)
    {
        var (user, roles) = await security.CurrentAsync(principal, ct);
        if (!roles.Contains(AcademixRoles.Student) || user.InstitutionId is not Guid institutionId)
            throw ApiFault.Forbidden();
        var now = DateTime.UtcNow;
        var query = from reservation in db.CampusRoomReservations.AsNoTracking()
                    join enrollment in db.StudentEnrollments.AsNoTracking()
                        on new { reservation.InstitutionId, SectionId = reservation.SectionId.GetValueOrDefault() }
                        equals new { enrollment.InstitutionId, enrollment.SectionId }
                    join room in db.CampusRooms.AsNoTracking() on reservation.RoomId equals room.Id
                    join building in db.CampusBuildings.AsNoTracking() on room.BuildingId equals building.Id
                    where reservation.InstitutionId == institutionId && reservation.Kind == "Exam" &&
                          reservation.Status == "Active" && reservation.StartsAtUtc >= now &&
                          enrollment.StudentId == user.Id && enrollment.Status == "Active"
                    orderby reservation.StartsAtUtc, reservation.Id
                    select new StudentExamVenueView(reservation.ExamId.GetValueOrDefault(), enrollment.SectionId,
                        room.Id, room.Name, building.Name, reservation.StartsAtUtc, reservation.EndsAtUtc);
        return await query.Take(200).ToListAsync(ct);
    }
}
