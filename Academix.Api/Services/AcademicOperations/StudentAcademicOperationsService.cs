using System.Text.Json;
using Academix.Api.Services.Academic;
using System.Security.Claims;
using Academix.Api.Data;
using Academix.Api.Services.Infrastructure;
using Academix.Api.Domain.AcademicOperations;
using Academix.Api.Domain.Identity;
using Academix.Api.DTOs.AcademicOperations;
using Academix.Api.Middleware;
using Academix.Api.Services.Identity;
using Microsoft.EntityFrameworkCore;

namespace Academix.Api.Services.AcademicOperations;

public sealed class StudentAcademicOperationsService(AcademixDbContext db, IdentitySecurity security, RetryingServiceWork retry)
{
    private async Task<(Guid Student, Guid Institution)> Student(ClaimsPrincipal principal, CancellationToken ct)
    {
        var (user, roles) = await security.CurrentAsync(principal, ct);
        if (user.InstitutionId is not Guid institutionId || !roles.Contains(AcademixRoles.Student))
            throw ApiFault.Forbidden();
        return (user.Id, institutionId);
    }

    public async Task<IReadOnlyList<SlotView>> Timetable(ClaimsPrincipal principal, CancellationToken ct)
    {
        var (studentId, inst) = await Student(principal, ct);
        var sections = db.StudentEnrollments.AsNoTracking().Where(x => x.StudentId == studentId &&
            x.InstitutionId == inst && x.Status == "Active").Select(x => x.SectionId);
        return await db.TimetableSlots.AsNoTracking().Where(x => x.InstitutionId == inst &&
            sections.Contains(x.SectionId) && x.IsActive)
            .OrderBy(x => x.Weekday).ThenBy(x => x.StartsAtMinute).ThenBy(x => x.Id)
            .Select(x => new SlotView(x.Id, x.SectionId, x.Weekday, x.StartsAtMinute,
                x.DurationMinutes, x.Room, x.IsActive, x.Version)).Take(500).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<StudentScheduleItem>> Schedule(ClaimsPrincipal principal,
        DateTime fromUtc, DateTime toUtc, CancellationToken ct)
    {
        AcademicOperationsPolicy.TimeRange(fromUtc, toUtc);
        var (studentId, inst) = await Student(principal, ct);
        var sections = db.StudentEnrollments.AsNoTracking().Where(x => x.StudentId == studentId &&
            x.InstitutionId == inst && x.Status == "Active").Select(x => x.SectionId);
        var sessions = await db.ClassSessions.AsNoTracking().Where(x => x.InstitutionId == inst &&
            sections.Contains(x.SectionId) && x.Status != "Cancelled" &&
            x.StartsAtUtc < toUtc && x.EndsAtUtc > fromUtc)
            .OrderBy(x => x.StartsAtUtc).Take(500)
            .Select(x => new StudentScheduleItem(x.Id, x.SectionId, "Session", x.Topic, x.StartsAtUtc, x.EndsAtUtc))
            .ToListAsync(ct);
        var events = await db.AcademicCalendarEvents.AsNoTracking().Where(x => x.InstitutionId == inst &&
            x.IsPublished && (x.Audience == "All" || x.Audience == "Students") &&
            (!x.SectionId.HasValue || sections.Contains(x.SectionId.Value)) &&
            x.StartsAtUtc < toUtc && x.EndsAtUtc > fromUtc)
            .OrderBy(x => x.StartsAtUtc).Take(500)
            .Select(x => new StudentScheduleItem(x.Id, x.SectionId ?? Guid.Empty, "CalendarEvent",
                x.Title, x.StartsAtUtc, x.EndsAtUtc)).ToListAsync(ct);
        return sessions.Concat(events).OrderBy(x => x.StartsAtUtc).ThenBy(x => x.Id).Take(500).ToArray();
    }

    public async Task<AttendanceSummary> Attendance(ClaimsPrincipal principal, Guid sectionId, CancellationToken ct)
    {
        var (studentId, inst) = await Student(principal, ct);
        if (!await db.StudentEnrollments.AnyAsync(x => x.InstitutionId == inst &&
            x.SectionId == sectionId && x.StudentId == studentId && x.Status == "Active", ct))
            throw ApiFault.NotFound();
        var sessions = db.ClassSessions.AsNoTracking().Where(x => x.InstitutionId == inst &&
            x.SectionId == sectionId && x.Status != "Cancelled" && x.StartsAtUtc <= DateTime.UtcNow)
            .Select(x => x.Id);
        var total = await sessions.CountAsync(ct);
        var query = db.AttendanceRecords.AsNoTracking().Where(x => x.InstitutionId == inst &&
            x.StudentId == studentId && x.SectionId == sectionId && sessions.Contains(x.SessionId));
        return new AttendanceSummary(studentId, sectionId, total,
            await query.CountAsync(x => x.Status == "Present", ct),
            await query.CountAsync(x => x.Status == "Late", ct),
            await query.CountAsync(x => x.Status == "Absent", ct),
            await query.CountAsync(x => x.Status == "Excused", ct));
    }

    public Task<AcademicRequestView> CreateRequest(ClaimsPrincipal principal, AcademicRequestCreate input, CancellationToken ct)
        =>retry.ExecuteAsync<StudentAcademicOperationsService,AcademicRequestView>(s=>s.CreateRequestCore(principal,input,ct),ct);
    private async Task<AcademicRequestView> CreateRequestCore(ClaimsPrincipal principal,
        AcademicRequestCreate input, CancellationToken ct)
    {
        if(input is null||input.ClientRequestId==Guid.Empty)throw ApiFault.Bad("Valid request required.");
        AcademicOperationsPolicy.Request(input);
        if(input.Kind!="General"&&input.SectionId is null)throw ApiFault.Bad("A section reference is required for executable academic requests.");
        if(input.Execution is not null)AcademicRequestExecutionRules.Validate(input.Kind,input.Execution);
        var (student, inst) = await Student(principal, ct);
        await using var tx=await db.Database.BeginTransactionAsync(ct);
        await RegistrationCoordination.Lock(db,inst,ct);
        var execution=input.Execution is null?null:JsonSerializer.Serialize(input.Execution);
        if(input.ClientRequestId.HasValue)
        {
            var prior=await db.StudentAcademicRequests.SingleOrDefaultAsync(x=>x.InstitutionId==inst&&x.StudentId==student&&x.ClientRequestId==input.ClientRequestId,ct);
            if(prior is not null)
            {if(prior.Kind!=input.Kind||prior.SectionId!=input.SectionId||prior.Subject!=input.Subject.Trim()||prior.Details!=input.Details.Trim()||!StoredJson.Same<Academix.Api.DTOs.Registrar.RequestExecutionData>(prior.ExecutionDataJson,execution))throw ApiFault.Conflict("Request key reused with different data.");return View(prior);}
        }
        if(input.Kind=="EnrollmentException"&&input.SectionId is Guid target&&!await db.CourseSections.AnyAsync(x=>x.Id==target&&x.InstitutionId==inst,ct))throw ApiFault.NotFound();
        if (input.Kind!="EnrollmentException" && input.SectionId is Guid sectionId && !await db.StudentEnrollments.AnyAsync(x =>
            x.InstitutionId == inst && x.SectionId == sectionId && x.StudentId == student &&
            x.Status == "Active", ct)) throw ApiFault.NotFound();
        var item = new StudentAcademicRequest { InstitutionId = inst, StudentId = student,
            SectionId = input.SectionId, Kind = input.Kind, ClientRequestId=input.ClientRequestId, ExecutionDataJson=execution, Subject = input.Subject.Trim(), Details = input.Details.Trim() };
        db.StudentAcademicRequests.Add(item);
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);
        return View(item);
    }

    public async Task<IReadOnlyList<AcademicRequestView>> MyRequests(ClaimsPrincipal principal, int page,
        CancellationToken ct)
    {
        if (page is < 1 or > 10000) throw ApiFault.Bad("Page must be between 1 and 10000.");
        var (student, inst) = await Student(principal, ct);
        return await db.StudentAcademicRequests.AsNoTracking().Where(x =>
            x.InstitutionId == inst && x.StudentId == student)
            .OrderByDescending(x => x.SubmittedAtUtc).ThenByDescending(x => x.Id)
            .Skip((page - 1) * 20).Take(20)
            .Select(x => new AcademicRequestView(x.Id, x.SectionId, x.Kind, x.Subject, x.Details,
                x.Status, x.SubmittedAtUtc, x.DecidedAtUtc, x.DecisionReason, x.Version, x.FulfilledAtUtc, x.FulfillmentResult, x.ExecutionDataJson)).ToListAsync(ct);
    }

    public Task<AcademicRequestView> CancelRequest(ClaimsPrincipal principal,
        Guid requestId, long expectedVersion, CancellationToken ct)
        => retry.ExecuteAsync<StudentAcademicOperationsService, AcademicRequestView>(service => service.CancelRequestCore(principal, requestId, expectedVersion, ct), ct);

    private async Task<AcademicRequestView> CancelRequestCore(ClaimsPrincipal principal,
        Guid requestId, long expectedVersion, CancellationToken ct)
    {
        var (student, inst) = await Student(principal, ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var request = await db.StudentAcademicRequests.FromSqlInterpolated(
            $"SELECT * FROM student_academic_requests WHERE \"Id\" = {requestId} AND \"InstitutionId\" = {inst} FOR UPDATE")
            .SingleOrDefaultAsync(ct) ?? throw ApiFault.NotFound();
        if (request.StudentId != student) throw ApiFault.NotFound();
        if (request.Status != "Pending" || request.Version != expectedVersion)
            throw ApiFault.Conflict("Request is no longer pending or its version changed.");
        request.Status = "Cancelled"; request.Version++;
        db.AcademicRequestHistories.Add(new AcademicRequestHistory { InstitutionId = inst,
            RequestId = request.Id, ActorId = student, FromStatus = "Pending", ToStatus = "Cancelled",
            Reason = "Withdrawn by student" });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return View(request);
    }

    public static AcademicRequestView View(StudentAcademicRequest x) => new(x.Id, x.SectionId,
        x.Kind, x.Subject, x.Details, x.Status, x.SubmittedAtUtc, x.DecidedAtUtc,
        x.DecisionReason, x.Version, x.FulfilledAtUtc, x.FulfillmentResult, x.ExecutionDataJson);
}
