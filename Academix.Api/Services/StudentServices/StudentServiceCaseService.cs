using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Http;
using Academix.Api.Data;
using Academix.Api.Domain.Identity;
using Academix.Api.Domain.StudentServices;
using Academix.Api.DTOs.StudentServices;
using Academix.Api.Middleware;
using Academix.Api.Services.Identity;
using Academix.Api.Services.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Academix.Api.Services.StudentServices;

public sealed class StudentServiceCaseService(AcademixDbContext db, IdentitySecurity security,
    RetryingServiceWork retry)
{
    private static ServiceDefinitionView Dto(StudentServiceDefinition value) => new(value.Id,
        value.InstitutionId, value.Code, value.Name, value.Description, value.SlaHours,
        value.IsActive, value.Version);
    private static ServiceCaseView Dto(StudentServiceCase value) => new(value.Id,
        value.InstitutionId, value.DefinitionId, value.StudentId, value.Subject, value.Details,
        value.Status, value.SubmittedAtUtc, value.UpdatedAtUtc, value.DueAtUtc,
        value.ClosedAtUtc, value.Version, value.CurrentQueueCode, value.AssignedStaffId,
        value.Priority, value.EscalatedAtUtc, value.DueAtUtc < DateTime.UtcNow &&
        value.Status is not ("Closed" or "Rejected" or "Cancelled" or "Resolved"));

    private async Task<ApplicationUser> Student(ClaimsPrincipal principal, CancellationToken ct)
    {
        var (user, roles) = await security.CurrentAsync(principal, ct);
        if (user.InstitutionId is null || !roles.Contains(AcademixRoles.Student)) throw ApiFault.Forbidden();
        return user;
    }

    public async Task<IReadOnlyList<ServiceDefinitionView>> Catalog(ClaimsPrincipal principal,
        Guid institution, bool activeOnly, CancellationToken ct)
    {
        var (user, roles) = await security.CurrentAsync(principal, ct);
        var admin = roles.Contains(AcademixRoles.SuperAdministrator) ||
            roles.Contains(AcademixRoles.InstitutionAdministrator) && user.InstitutionId == institution;
        if (!admin && (user.InstitutionId != institution || !roles.Contains(AcademixRoles.Student)))
            throw ApiFault.Forbidden();
        if (!admin) activeOnly = true;
        var list = await db.Set<StudentServiceDefinition>().AsNoTracking().Where(x =>
            x.InstitutionId == institution && (!activeOnly || x.IsActive))
            .OrderBy(x => x.Code).ToArrayAsync(ct);
        return list.Select(Dto).ToArray();
    }

    public Task<ServiceDefinitionView> SaveDefinition(ClaimsPrincipal principal, Guid institution,
        Guid? id, ServiceDefinitionInput input, CancellationToken ct)
        => retry.ExecuteAsync<StudentServiceCaseService, ServiceDefinitionView>(s =>
            s.SaveDefinitionCore(principal, institution, id, input, ct), ct);

    private async Task<ServiceDefinitionView> SaveDefinitionCore(ClaimsPrincipal principal,
        Guid institution, Guid? id, ServiceDefinitionInput input, CancellationToken ct)
    {
        StudentServiceRules.ValidateDefinition(input, !id.HasValue);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await LockInstitution(institution, ct);
        var actor = await security.RequireAdminAsync(principal, institution, ct);
        StudentServiceDefinition row;
        if (id is Guid existing)
        {
            row = await db.Set<StudentServiceDefinition>().SingleOrDefaultAsync(x =>
                x.Id == existing && x.InstitutionId == institution, ct) ?? throw ApiFault.NotFound();
            if (row.Version != input.ExpectedVersion) throw ApiFault.Conflict("Stale service definition version.");
            if (row.Code != input.Code) throw ApiFault.Conflict("Service category code cannot be changed.");
            row.Name = input.Name.Trim(); row.Description = input.Description.Trim();
            row.SlaHours = input.SlaHours; row.IsActive = input.IsActive; row.Version++;
        }
        else
        {
            if (await db.Set<StudentServiceDefinition>().AnyAsync(x =>
                x.InstitutionId == institution && x.Code == input.Code, ct))
                throw ApiFault.Conflict("Service category already exists.");
            row = new StudentServiceDefinition { InstitutionId = institution, Code = input.Code,
                Name = input.Name.Trim(), Description = input.Description.Trim(),
                SlaHours = input.SlaHours, IsActive = input.IsActive };
            db.Set<StudentServiceDefinition>().Add(row);
        }
        security.Audit(actor.Id, institution, "student-service.catalog.changed", row.Id.ToString());
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return Dto(row);
    }

    public Task<ServiceCaseView> Submit(ClaimsPrincipal principal, ServiceCaseSubmit input,
        CancellationToken ct) => retry.ExecuteAsync<StudentServiceCaseService, ServiceCaseView>(s =>
            s.SubmitCore(principal, input, ct), ct);

    private async Task<ServiceCaseView> SubmitCore(ClaimsPrincipal principal, ServiceCaseSubmit input,
        CancellationToken ct)
    {
        StudentServiceRules.ValidateSubmission(input);
        var student = await Student(principal, ct);
        var institution = student.InstitutionId!.Value;
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await LockInstitution(institution, ct);
        student=await Student(principal,ct);
        var existing = await db.Set<StudentServiceCase>().AsNoTracking().SingleOrDefaultAsync(x =>
            x.InstitutionId == institution && x.StudentId == student.Id &&
            x.ClientRequestId == input.ClientRequestId, ct);
        if (existing is not null)
        {
            if (existing.DefinitionId != input.DefinitionId || existing.Subject != input.Subject.Trim() ||
                existing.Details != input.Details.Trim())
                throw ApiFault.Conflict("Idempotency key reused with different service request details.");
            return Dto(existing);
        }
        var definition = await db.Set<StudentServiceDefinition>().AsNoTracking().SingleOrDefaultAsync(x =>
            x.Id == input.DefinitionId && x.InstitutionId == institution && x.IsActive, ct)
            ?? throw ApiFault.Bad("Service category unavailable.");
        var now = DateTime.UtcNow;
        var row = new StudentServiceCase { InstitutionId = institution, StudentId = student.Id,
            DefinitionId = definition.Id, ClientRequestId = input.ClientRequestId,
            CurrentQueueCode = definition.Code, Subject = input.Subject.Trim(), Details = input.Details.Trim(),
            SubmittedAtUtc = now, UpdatedAtUtc = now, DueAtUtc = now.AddHours(definition.SlaHours) };
        db.Set<StudentServiceCase>().Add(row);
        db.Set<StudentServiceCaseEvent>().Add(new StudentServiceCaseEvent {
            InstitutionId = institution, CaseId = row.Id, ActorId = student.Id,
            Action = "Submit", ToStatus = row.Status, PublicMessage = "Request submitted", OccurredAtUtc = now });
        OutboxService.Record(db, institution, "StudentServiceCaseChanged", Guid.NewGuid(),
            new { StudentId = row.StudentId, CaseId = row.Id, Status = row.Status });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return Dto(row);
    }

    public async Task<IReadOnlyList<ServiceCaseView>> Mine(ClaimsPrincipal principal, int page,
        CancellationToken ct)
    {
        var student = await Student(principal, ct);
        Page(page);
        var rows = await db.Set<StudentServiceCase>().AsNoTracking().Where(x =>
            x.InstitutionId == student.InstitutionId && x.StudentId == student.Id)
            .OrderByDescending(x => x.SubmittedAtUtc).ThenBy(x => x.Id)
            .Skip((page - 1) * 30).Take(30).ToArrayAsync(ct);
        return rows.Select(Dto).ToArray();
    }

    public async Task<IReadOnlyList<ServiceCaseView>> Queue(ClaimsPrincipal principal, Guid institution,
        string? status, string? queue, int page, CancellationToken ct)
    {
        var queues = await AllowedQueues(principal, institution, ct);
        Page(page);
        if (status is not null && status is not ("Submitted" or "InReview" or "AwaitingStudent" or "Resolved" or "Rejected" or "Closed" or "Cancelled"))
            throw ApiFault.Bad("Invalid status filter.");
        if (queue is not null && !StudentServiceRules.IsQueue(queue)) throw ApiFault.Bad("Invalid queue filter.");
        if (queue is not null && !queues.Contains(queue)) throw ApiFault.Forbidden();
        var rows = await db.Set<StudentServiceCase>().AsNoTracking().Where(x =>
            x.InstitutionId == institution && queues.Contains(x.CurrentQueueCode) &&
            (queue == null || x.CurrentQueueCode == queue) && (status == null || x.Status == status))
            .OrderBy(x => x.DueAtUtc).ThenBy(x => x.Id)
            .Skip((page - 1) * 30).Take(30).ToArrayAsync(ct);
        return rows.Select(Dto).ToArray();
    }

    public async Task<IReadOnlyList<ServiceCaseEventView>> StudentTimeline(ClaimsPrincipal principal,
        Guid id, CancellationToken ct)
    {
        var student = await Student(principal, ct);
        if (!await db.Set<StudentServiceCase>().AnyAsync(x => x.Id == id &&
            x.StudentId == student.Id && x.InstitutionId == student.InstitutionId, ct)) throw ApiFault.NotFound();
        return await db.Set<StudentServiceCaseEvent>().AsNoTracking().Where(x =>
            x.CaseId == id && x.InstitutionId == student.InstitutionId)
            .OrderBy(x => x.OccurredAtUtc).ThenBy(x => x.Id)
            .Select(x => new ServiceCaseEventView(x.Id, x.Action, x.FromStatus, x.ToStatus,
                x.PublicMessage, x.OccurredAtUtc)).ToArrayAsync(ct);
    }

    public async Task<IReadOnlyList<StaffServiceCaseEventView>> StaffTimeline(ClaimsPrincipal principal,
        Guid institution, Guid id, CancellationToken ct)
    {
        _ = await CaseForStaff(principal, institution, id, ct);
        return await db.Set<StudentServiceCaseEvent>().AsNoTracking().Where(x =>
            x.CaseId == id && x.InstitutionId == institution)
            .OrderBy(x => x.OccurredAtUtc).ThenBy(x => x.Id)
            .Select(x => new StaffServiceCaseEventView(x.Id, x.ActorId, x.Action, x.FromStatus,
                x.ToStatus, x.PublicMessage, x.PrivateStaffNote, x.OccurredAtUtc)).ToArrayAsync(ct);
    }

    public Task<ServiceCaseView> Act(ClaimsPrincipal principal, Guid institution, Guid id,
        ServiceCaseAction input, bool studentAction, CancellationToken ct)
        => retry.ExecuteAsync<StudentServiceCaseService, ServiceCaseView>(s =>
            s.ActCore(principal, institution, id, input, studentAction, ct), ct);

    private async Task<ServiceCaseView> ActCore(ClaimsPrincipal principal, Guid institution, Guid id,
        ServiceCaseAction input, bool studentAction, CancellationToken ct)
    {
        StudentServiceRules.ValidateAction(input, studentAction);
        var (actor, roles) = await security.CurrentAsync(principal, ct);
        if (studentAction && (actor.InstitutionId != institution ||
            !roles.Contains(AcademixRoles.Student)))
            throw ApiFault.NotFound();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await LockInstitution(institution, ct);
        if(studentAction)actor=await Student(principal,ct);
        var row = await db.Set<StudentServiceCase>().SingleOrDefaultAsync(x =>
            x.Id == id && x.InstitutionId == institution && (!studentAction || x.StudentId == actor.Id), ct)
            ?? throw ApiFault.NotFound();
        if (!studentAction)
            await RequireQueueAccess(principal, institution, row.CurrentQueueCode, row.AssignedStaffId, ct, true);
        if (row.Version != input.ExpectedVersion) throw ApiFault.Conflict("Stale case version.");
        var to = StudentServiceRules.Transition(row.Status, input.Action, studentAction);
        var from = row.Status;
        row.Status = to; row.Version++; row.UpdatedAtUtc = DateTime.UtcNow;
        if (to is "Closed" or "Rejected" or "Cancelled") row.ClosedAtUtc = row.UpdatedAtUtc;
        db.Set<StudentServiceCaseEvent>().Add(new StudentServiceCaseEvent {
            InstitutionId = institution, CaseId = row.Id, ActorId = actor.Id,
            Action = input.Action, FromStatus = from, ToStatus = to,
            PublicMessage = input.PublicMessage.Trim(), PrivateStaffNote = studentAction ? "" : input.PrivateStaffNote.Trim(),
            OccurredAtUtc = row.UpdatedAtUtc });
        OutboxService.Record(db, institution, "StudentServiceCaseChanged", Guid.NewGuid(),
            new { StudentId = row.StudentId, CaseId = row.Id, Status = to });
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return Dto(row);
    }


    // Authoritative grants plus CURRENT persisted roles. JWT role claims never authorize case operations.
    private async Task<(ApplicationUser User, bool Admin)> RequireQueueAccess(ClaimsPrincipal principal,
        Guid institution, string queue, Guid? assigned, CancellationToken ct, bool write)
    {
        var (actor, roles) = await security.CurrentAsync(principal, ct);
        var admin = roles.Contains(AcademixRoles.SuperAdministrator) ||
            (actor.InstitutionId == institution && roles.Contains(AcademixRoles.InstitutionAdministrator));
        if (admin) return (actor, true);
        if (actor.InstitutionId != institution || !IsStaffRole(roles) ||
            !await db.Set<StudentServiceStaffGrant>().AnyAsync(x => x.InstitutionId == institution &&
                x.QueueCode == queue && x.UserId == actor.Id && x.IsActive, ct)) throw ApiFault.Forbidden();
        if (write && assigned.HasValue && assigned != actor.Id) throw ApiFault.Forbidden();
        return (actor, false);
    }
    private static bool IsStaffRole(IReadOnlyCollection<string> roles)
        => roles.Contains(AcademixRoles.Instructor) || roles.Contains(AcademixRoles.TeachingAssistant);

    private async Task<string[]> AllowedQueues(ClaimsPrincipal principal, Guid institution, CancellationToken ct)
    {
        var (user, roles) = await security.CurrentAsync(principal, ct);
        if (roles.Contains(AcademixRoles.SuperAdministrator) ||
            (user.InstitutionId == institution && roles.Contains(AcademixRoles.InstitutionAdministrator)))
            return new[] { "GeneralSupport", "StudentAffairs", "ITSupport", "Library", "CampusServices" };
        if (user.InstitutionId != institution || !IsStaffRole(roles)) throw ApiFault.Forbidden();
        var queues = await db.Set<StudentServiceStaffGrant>().AsNoTracking()
            .Where(x => x.InstitutionId == institution && x.UserId == user.Id && x.IsActive)
            .Select(x => x.QueueCode).ToArrayAsync(ct);
        if (queues.Length == 0) throw ApiFault.Forbidden();
        return queues;
    }
    private async Task<StudentServiceCase> CaseForStaff(ClaimsPrincipal principal, Guid institution,
        Guid caseId, CancellationToken ct)
    {
        var row = await db.Set<StudentServiceCase>().AsNoTracking().SingleOrDefaultAsync(x =>
            x.InstitutionId == institution && x.Id == caseId, ct) ?? throw ApiFault.NotFound();
        await RequireQueueAccess(principal, institution, row.CurrentQueueCode, row.AssignedStaffId, ct, false);
        return row;
    }
    private static bool Terminal(string status)
        => status is "Closed" or "Cancelled" or "Rejected";
    private static void ActiveCase(StudentServiceCase row)
    {
        if (Terminal(row.Status) || row.Status == "Resolved") throw ApiFault.Conflict("Case is no longer active.");
    }

    public Task<ServiceStaffGrantView> SetStaffGrant(ClaimsPrincipal principal, Guid institution,
        Guid userId, string queue, ServiceStaffGrantInput input, CancellationToken ct)
        => retry.ExecuteAsync<StudentServiceCaseService, ServiceStaffGrantView>(s =>
            s.SetStaffGrantCore(principal, institution, userId, queue, input, ct), ct);

    private async Task<ServiceStaffGrantView> SetStaffGrantCore(ClaimsPrincipal principal, Guid institution,
        Guid userId, string queue, ServiceStaffGrantInput input, CancellationToken ct)
    {
        if (!StudentServiceRules.IsQueue(queue) || input is null) throw ApiFault.Bad("Invalid staff grant.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await LockInstitution(institution, ct);
        var actor = await security.RequireAdminAsync(principal, institution, ct);
        var target = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == userId &&
            x.InstitutionId == institution && x.IsActive, ct) ?? throw ApiFault.Bad("User not eligible for assignment.");
        var hasStaffRole = await (from ur in db.UserRoles
                                  join role in db.Roles on ur.RoleId equals role.Id
                                  where ur.UserId == target.Id &&
                                      (role.Name == AcademixRoles.Instructor || role.Name == AcademixRoles.TeachingAssistant ||
                                       role.Name == AcademixRoles.InstitutionAdministrator)
                                  select ur.UserId).AnyAsync(ct);
        if (!hasStaffRole) throw ApiFault.Bad("User lacks an active staff role.");
        var grant = await db.Set<StudentServiceStaffGrant>().SingleOrDefaultAsync(x =>
            x.InstitutionId == institution && x.UserId == userId && x.QueueCode == queue, ct);
        if (grant is null)
        {
            if (input.ExpectedVersion != 0) throw ApiFault.Conflict("Grant does not exist.");
            grant = new StudentServiceStaffGrant { InstitutionId = institution, UserId = userId,
                QueueCode = queue, IsActive = input.IsActive };
            db.Set<StudentServiceStaffGrant>().Add(grant);
        }
        else
        {
            if (grant.Version != input.ExpectedVersion) throw ApiFault.Conflict("Stale staff grant.");
            grant.IsActive = input.IsActive; grant.Version++;
        }
        // Revoking a grant also unassigns cases it can no longer own; no case status is changed.
        if (!input.IsActive)
        {
            var assigned = await db.Set<StudentServiceCase>().Where(x => x.InstitutionId == institution &&
                x.CurrentQueueCode == queue && x.AssignedStaffId == userId &&
                x.Status != "Closed" && x.Status != "Cancelled" && x.Status != "Rejected").ToArrayAsync(ct);
            foreach (var row in assigned)
            {
                row.AssignedStaffId = null; row.Version++; row.UpdatedAtUtc = DateTime.UtcNow;
                AddEvent(row, actor.Id, "Unassign", row.Status, row.Status, "Assignment ended after grant revocation", "");
            }
        }
        security.Audit(actor.Id, institution, "student-service.grant.changed", userId.ToString());
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return new ServiceStaffGrantView(grant.Id, grant.UserId, grant.QueueCode, grant.IsActive, grant.Version);
    }

    public async Task<IReadOnlyList<ServiceStaffGrantView>> StaffGrants(ClaimsPrincipal principal,
        Guid institution, string? queue, CancellationToken ct)
    {
        await security.RequireAdminAsync(principal, institution, ct);
        if (queue is not null && !StudentServiceRules.IsQueue(queue)) throw ApiFault.Bad("Invalid queue.");
        return await db.Set<StudentServiceStaffGrant>().AsNoTracking().Where(x => x.InstitutionId == institution &&
            (queue == null || x.QueueCode == queue))
            .OrderBy(x => x.QueueCode).ThenBy(x => x.UserId)
            .Select(x => new ServiceStaffGrantView(x.Id, x.UserId, x.QueueCode, x.IsActive, x.Version)).ToArrayAsync(ct);
    }

    private void AddEvent(StudentServiceCase row, Guid actor, string action, string from,
        string to, string publicMessage, string privateNote)
    {
        db.Set<StudentServiceCaseEvent>().Add(new StudentServiceCaseEvent {
            InstitutionId = row.InstitutionId, CaseId = row.Id, ActorId = actor,
            Action = action, FromStatus = from, ToStatus = to, PublicMessage = publicMessage,
            PrivateStaffNote = privateNote, OccurredAtUtc = DateTime.UtcNow });
    }
    private void Notify(StudentServiceCase row)
        => OutboxService.Record(db, row.InstitutionId, "StudentServiceCaseChanged", Guid.NewGuid(),
            new { StudentId = row.StudentId, CaseId = row.Id, Status = row.Status });

    public Task<ServiceCaseView> Assign(ClaimsPrincipal principal, Guid institution, Guid id,
        ServiceCaseAssign input, CancellationToken ct)
        => retry.ExecuteAsync<StudentServiceCaseService, ServiceCaseView>(s =>
            s.AssignCore(principal, institution, id, input, ct), ct);
    private async Task<ServiceCaseView> AssignCore(ClaimsPrincipal principal, Guid institution, Guid id,
        ServiceCaseAssign input, CancellationToken ct)
    {
        StudentServiceRules.ValidateAssignment(input);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await LockInstitution(institution, ct);
        var row = await db.Set<StudentServiceCase>().SingleOrDefaultAsync(x => x.Id == id &&
            x.InstitutionId == institution, ct) ?? throw ApiFault.NotFound();
        ActiveCase(row);
        var (actor, admin) = await RequireQueueAccess(principal, institution, row.CurrentQueueCode, row.AssignedStaffId, ct, true);
        if (row.Version != input.ExpectedVersion) throw ApiFault.Conflict("Stale case.");
        if (!admin && input.StaffId is Guid chosen && chosen != actor.Id) throw ApiFault.Forbidden();
        if (!admin && !input.StaffId.HasValue && row.AssignedStaffId != actor.Id) throw ApiFault.Forbidden();
        if (input.StaffId is Guid target)
        {
            var eligible = await db.Users.AsNoTracking().AnyAsync(x => x.Id == target &&
                x.InstitutionId == institution && x.IsActive, ct);
            var grant = await db.Set<StudentServiceStaffGrant>().AnyAsync(x => x.InstitutionId == institution &&
                x.UserId == target && x.QueueCode == row.CurrentQueueCode && x.IsActive, ct);
            var staffRole = await (from ur in db.UserRoles join role in db.Roles on ur.RoleId equals role.Id
                where ur.UserId == target && (role.Name == AcademixRoles.Instructor ||
                    role.Name == AcademixRoles.TeachingAssistant || role.Name == AcademixRoles.InstitutionAdministrator)
                select ur.UserId).AnyAsync(ct);
            if (!eligible || !grant || !staffRole) throw ApiFault.Bad("Staff member cannot own this queue.");
        }
        if (row.AssignedStaffId == input.StaffId) throw ApiFault.Conflict("Assignment unchanged.");
        row.AssignedStaffId = input.StaffId; row.Version++; row.UpdatedAtUtc = DateTime.UtcNow;
        AddEvent(row, actor.Id, input.StaffId.HasValue ? "Assign" : "Unassign", row.Status, row.Status,
            "Case handler updated", input.Reason.Trim());
        Notify(row);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return Dto(row);
    }

    public Task<ServiceCaseView> Refer(ClaimsPrincipal principal, Guid institution, Guid id,
        ServiceCaseRefer input, CancellationToken ct)
        => retry.ExecuteAsync<StudentServiceCaseService, ServiceCaseView>(s =>
            s.ReferCore(principal, institution, id, input, ct), ct);
    private async Task<ServiceCaseView> ReferCore(ClaimsPrincipal principal, Guid institution, Guid id,
        ServiceCaseRefer input, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await LockInstitution(institution, ct);
        var row = await db.Set<StudentServiceCase>().SingleOrDefaultAsync(x => x.Id == id &&
            x.InstitutionId == institution, ct) ?? throw ApiFault.NotFound();
        ActiveCase(row);
        var (actor, _) = await RequireQueueAccess(principal, institution, row.CurrentQueueCode, row.AssignedStaffId, ct, true);
        StudentServiceRules.ValidateReferral(input, row.CurrentQueueCode);
        if (row.Version != input.ExpectedVersion) throw ApiFault.Conflict("Stale case.");
        var previous = row.CurrentQueueCode;
        row.CurrentQueueCode = input.TargetQueue; row.AssignedStaffId = null;
        row.Version++; row.UpdatedAtUtc = DateTime.UtcNow;
        db.Set<StudentServiceReferral>().Add(new StudentServiceReferral {
            InstitutionId = institution, CaseId = row.Id, ActorId = actor.Id,
            FromQueue = previous, ToQueue = input.TargetQueue, Reason = input.Reason.Trim() });
        AddEvent(row, actor.Id, "Refer", row.Status, row.Status,
            "Request forwarded to another service desk", input.Reason.Trim());
        Notify(row);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return Dto(row);
    }

    public async Task<IReadOnlyList<ServiceReferralView>> Referrals(ClaimsPrincipal principal,
        Guid institution, Guid id, CancellationToken ct)
    {
        await CaseForStaff(principal, institution, id, ct);
        return await db.Set<StudentServiceReferral>().AsNoTracking().Where(x => x.InstitutionId == institution && x.CaseId == id)
            .OrderBy(x => x.OccurredAtUtc).ThenBy(x => x.Id)
            .Select(x => new ServiceReferralView(x.Id, x.ActorId, x.FromQueue,
                x.ToQueue, x.Reason, x.OccurredAtUtc)).ToArrayAsync(ct);
    }

    public Task<ServiceCaseView> Escalate(ClaimsPrincipal principal, Guid institution, Guid id,
        ServiceCaseEscalate input, CancellationToken ct)
        => retry.ExecuteAsync<StudentServiceCaseService, ServiceCaseView>(s =>
            s.EscalateCore(principal, institution, id, input, ct), ct);
    private async Task<ServiceCaseView> EscalateCore(ClaimsPrincipal principal, Guid institution, Guid id,
        ServiceCaseEscalate input, CancellationToken ct)
    {
        StudentServiceRules.ValidateEscalation(input);
        await using var tx = await db.Database.BeginTransactionAsync(ct); await LockInstitution(institution, ct);
        var row = await db.Set<StudentServiceCase>().SingleOrDefaultAsync(x => x.Id == id &&
            x.InstitutionId == institution, ct) ?? throw ApiFault.NotFound();
        ActiveCase(row);
        var (actor, _) = await RequireQueueAccess(principal, institution, row.CurrentQueueCode, row.AssignedStaffId, ct, true);
        if (row.Version != input.ExpectedVersion) throw ApiFault.Conflict("Stale case.");
        if (row.Priority == input.Priority) throw ApiFault.Conflict("Priority unchanged.");
        row.Priority = input.Priority; row.EscalatedAtUtc = DateTime.UtcNow;
        row.UpdatedAtUtc = row.EscalatedAtUtc.Value; row.Version++;
        AddEvent(row, actor.Id, "Escalate", row.Status, row.Status,
            "Case priority changed", input.Reason.Trim()); Notify(row);
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return Dto(row);
    }

    public async Task<ServiceCaseSlaReport> Report(ClaimsPrincipal principal, Guid institution,
        CancellationToken ct)
    {
        var queues = await AllowedQueues(principal, institution, ct);
        var now = DateTime.UtcNow;
        var rows = db.Set<StudentServiceCase>().AsNoTracking().Where(x => x.InstitutionId == institution &&
            queues.Contains(x.CurrentQueueCode));
        var totals = await rows.GroupBy(x => 1).Select(g => new {
            Total = g.Count(),
            Open = g.Count(x => x.Status != "Closed" && x.Status != "Rejected" && x.Status != "Cancelled" && x.Status != "Resolved"),
            Overdue = g.Count(x => x.DueAtUtc < now && x.Status != "Closed" && x.Status != "Rejected" && x.Status != "Cancelled" && x.Status != "Resolved"),
            Awaiting = g.Count(x => x.Status == "AwaitingStudent"),
            Resolved = g.Count(x => x.Status == "Resolved"),
            Closed = g.Count(x => x.Status == "Closed" || x.Status == "Rejected" || x.Status == "Cancelled"),
            Unassigned = g.Count(x => x.AssignedStaffId == null && x.Status != "Closed" && x.Status != "Rejected" && x.Status != "Cancelled" && x.Status != "Resolved"),
            Urgent = g.Count(x => x.Priority == "Urgent" && x.Status != "Closed" && x.Status != "Rejected" && x.Status != "Cancelled")
        }).SingleOrDefaultAsync(ct);
        var grouped = await rows.GroupBy(x => x.CurrentQueueCode).OrderBy(g => g.Key).Select(g => new ServiceCaseQueueReport(g.Key,
            g.Count(x => x.Status != "Closed" && x.Status != "Rejected" && x.Status != "Cancelled" && x.Status != "Resolved"),
            g.Count(x => x.DueAtUtc < now && x.Status != "Closed" && x.Status != "Rejected" && x.Status != "Cancelled" && x.Status != "Resolved"),
            g.Count(x => x.AssignedStaffId == null && x.Status != "Closed" && x.Status != "Rejected" && x.Status != "Cancelled" && x.Status != "Resolved")))
            .ToArrayAsync(ct);
        return new ServiceCaseSlaReport(totals?.Total ?? 0, totals?.Open ?? 0, totals?.Overdue ?? 0,
            totals?.Awaiting ?? 0, totals?.Resolved ?? 0, totals?.Closed ?? 0,
            totals?.Unassigned ?? 0, totals?.Urgent ?? 0, grouped);
    }

    public async Task<IReadOnlyList<ServiceAttachmentView>> Attachments(ClaimsPrincipal principal, Guid institution,
        Guid caseId, bool student, CancellationToken ct)
    {
        var row = await db.Set<StudentServiceCase>().AsNoTracking().SingleOrDefaultAsync(x =>
            x.InstitutionId == institution && x.Id == caseId, ct) ?? throw ApiFault.NotFound();
        if (student)
        {
            var user = await Student(principal, ct);
            if (user.InstitutionId != institution || user.Id != row.StudentId) throw ApiFault.NotFound();
        }
        else await RequireQueueAccess(principal, institution, row.CurrentQueueCode, row.AssignedStaffId, ct, false);
        return await db.Set<StudentServiceAttachment>().AsNoTracking().Where(x =>
                x.InstitutionId == institution && x.CaseId == caseId && (!student || x.Visibility == "StudentVisible"))
            .OrderBy(x => x.UploadedAtUtc).ThenBy(x => x.Id)
            .Select(x => new ServiceAttachmentView(x.Id, x.FileName, x.ContentType, x.Visibility,
                x.Size, x.Sha256, x.UploadedAtUtc)).ToArrayAsync(ct);
    }

    public async Task<(byte[] Bytes, string ContentType, string FileName)> Download(ClaimsPrincipal principal,
        Guid institution, Guid caseId, Guid attachmentId, bool student, CancellationToken ct)
    {
        var row = await db.Set<StudentServiceCase>().AsNoTracking().SingleOrDefaultAsync(x =>
            x.InstitutionId == institution && x.Id == caseId, ct) ?? throw ApiFault.NotFound();
        if (student)
        {
            var actor = await Student(principal, ct);
            if (actor.InstitutionId != institution || row.StudentId != actor.Id) throw ApiFault.NotFound();
        }
        else await RequireQueueAccess(principal, institution, row.CurrentQueueCode, row.AssignedStaffId, ct, false);
        var file = await db.Set<StudentServiceAttachment>().AsNoTracking().SingleOrDefaultAsync(x =>
            x.InstitutionId == institution && x.CaseId == caseId && x.Id == attachmentId &&
            (!student || x.Visibility == "StudentVisible"), ct) ?? throw ApiFault.NotFound();
        return (file.Content, file.ContentType, file.FileName);
    }

    public Task<ServiceAttachmentView> Upload(ClaimsPrincipal principal, Guid institution, Guid caseId,
        IFormFile file, string visibility, bool student, CancellationToken ct)
        => retry.ExecuteAsync<StudentServiceCaseService, ServiceAttachmentView>(s =>
            s.UploadCore(principal, institution, caseId, file, visibility, student, ct), ct);
    private async Task<ServiceAttachmentView> UploadCore(ClaimsPrincipal principal, Guid institution, Guid caseId,
        IFormFile file, string visibility, bool student, CancellationToken ct)
    {
        if (file is null || file.Length is < 1 or > 2097152 ||
            (visibility is not ("StudentVisible" or "StaffOnly")) || (student && visibility != "StudentVisible"))
            throw ApiFault.Bad("Invalid service case attachment.");
        var name = Path.GetFileName(file.FileName.Replace('\\', '/'));
        byte[] bytes;
        await using (var memory = new MemoryStream())
        {
            await file.CopyToAsync(memory, ct);
            bytes = memory.ToArray();
        }
        StudentServiceRules.ValidateAttachment(name, file.ContentType, file.Length, bytes);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await LockInstitution(institution, ct);
        var row = await db.Set<StudentServiceCase>().SingleOrDefaultAsync(x =>
            x.InstitutionId == institution && x.Id == caseId, ct) ?? throw ApiFault.NotFound();
        ActiveCase(row);
        ApplicationUser actor;
        if (student)
        {
            actor = await Student(principal, ct);
            if (actor.InstitutionId != institution || actor.Id != row.StudentId) throw ApiFault.NotFound();
        }
        else (actor, _) = await RequireQueueAccess(principal, institution, row.CurrentQueueCode, row.AssignedStaffId, ct, true);
        if (await db.Set<StudentServiceAttachment>().CountAsync(x => x.InstitutionId == institution && x.CaseId == caseId, ct) >= 10)
            throw ApiFault.Conflict("Case attachment limit reached.");
        var item = new StudentServiceAttachment { InstitutionId = institution, CaseId = caseId,
            UploadedBy = actor.Id, FileName = name, ContentType = file.ContentType, Visibility = visibility,
            Size = bytes.LongLength, Sha256 = Convert.ToHexString(SHA256.HashData(bytes)), Content = bytes };
        db.Set<StudentServiceAttachment>().Add(item);
        AddEvent(row, actor.Id, "Attach", row.Status, row.Status,
            "A case attachment was added", visibility == "StaffOnly" ? "Staff-only attachment" : "");
        row.Version++; row.UpdatedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return new ServiceAttachmentView(item.Id, item.FileName, item.ContentType, item.Visibility,
            item.Size, item.Sha256, item.UploadedAtUtc);
    }

    private async Task LockInstitution(Guid institution, CancellationToken ct)
    {
        if (await db.Institutions.FromSqlInterpolated(
            $"SELECT * FROM institutions WHERE \"Id\" = {institution} FOR UPDATE")
            .SingleOrDefaultAsync(ct) is null) throw ApiFault.NotFound();
    }
    private static void Page(int page)
    {
        if (page is < 1 or > 100000) throw ApiFault.Bad("Invalid page.");
    }
}
