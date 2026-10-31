using Academix.Api.Services.Infrastructure;
using System.Security.Claims;
using Academix.Api.Data;
using Academix.Api.Domain.Academic;
using Academix.Api.Domain.Governance;
using Academix.Api.Domain.Identity;
using Academix.Api.DTOs.Governance;
using Academix.Api.Middleware;
using Academix.Api.Services.Identity;
using Academix.Api.Services.Records;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Academix.Api.Services.Governance;

public sealed class GovernanceService(AcademixDbContext db, IdentitySecurity security, UserManager<ApplicationUser> users, RecordsService records, RetryingServiceWork retry)
{
    private async Task<ApplicationUser> Admin(ClaimsPrincipal principal, Guid institutionId, CancellationToken ct)
    {
        var user = await security.RequireAdminAsync(principal, institutionId, ct);
        if (!await db.Institutions.AnyAsync(x => x.Id == institutionId && x.IsActive, ct))
            throw ApiFault.NotFound();
        return user;
    }

    private static StaffChangeView Map(StaffChangeRequest r) => new(r.Id, r.InstitutionId, r.TargetUserId,
        r.RequestedBy, r.Action, r.Role, r.Reason, r.Status, r.ReviewedBy, r.ReviewReason,
        r.CreatedAtUtc, r.ReviewedAtUtc, r.Version);

    private static EnrollmentBatchView Map(EnrollmentBatch batch, IEnumerable<EnrollmentBatchItem> items) =>
        new(batch.Id, batch.InstitutionId, batch.SectionId, batch.ClientRequestId, batch.CreatedBy,
            batch.Status, batch.CreatedAtUtc, batch.CommittedAtUtc,
            items.OrderBy(x => x.StudentId).Select(x => new EnrollmentBatchItemView(x.StudentId, x.Result)).ToArray());

    private async Task<ApplicationUser> Target(Guid institutionId, Guid targetId, CancellationToken ct)
    {
        var target = await users.FindByIdAsync(targetId.ToString()) ?? throw ApiFault.NotFound();
        if (target.InstitutionId != institutionId) throw ApiFault.NotFound();
        var roles = await users.GetRolesAsync(target);
        if (roles.Contains(AcademixRoles.InstitutionAdministrator) || roles.Contains(AcademixRoles.SuperAdministrator))
            throw ApiFault.Forbidden();
        return target;
    }

    public async Task<StaffChangeView> CreateStaffRequest(ClaimsPrincipal principal, Guid institutionId,
        CreateStaffChange request, CancellationToken ct)
    {
        var actor = await Admin(principal, institutionId, ct);
        if (request.ClientRequestId == Guid.Empty || request.TargetUserId == Guid.Empty || request.TargetUserId == actor.Id)
            throw ApiFault.Bad("Invalid request identifier or target.");
        var (action, role, reason) = GovernancePolicy.StaffChange(request.Action, request.Role, request.Reason);
        var existing = await db.StaffChangeRequests.AsNoTracking().SingleOrDefaultAsync(x =>
            x.InstitutionId == institutionId && x.ClientRequestId == request.ClientRequestId, ct);
        if (existing is not null)
        {
            if (existing.RequestedBy != actor.Id || existing.TargetUserId != request.TargetUserId ||
                existing.Action != action || existing.Role != role || existing.Reason != reason)
                throw ApiFault.Conflict("Idempotency key reused with different details.");
            return Map(existing);
        }
        var target = await Target(institutionId, request.TargetUserId, ct);
        if (action is "AddRole" or "RemoveRole" && !target.IsActive)
            throw ApiFault.Conflict("Target must be active for a role change.");
        var entry = new StaffChangeRequest { InstitutionId = institutionId, ClientRequestId = request.ClientRequestId,
            TargetUserId = target.Id, RequestedBy = actor.Id, Action = action, Role = role, Reason = reason };
        db.StaffChangeRequests.Add(entry);
        security.Audit(actor.Id, institutionId, "governance.staff.requested", entry.Id.ToString());
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { throw ApiFault.Conflict("Request key already exists; retrieve the original request."); }
        return Map(entry);
    }

    public async Task<IReadOnlyList<StaffChangeView>> StaffRequests(ClaimsPrincipal principal,
        Guid institutionId, int offset, int limit, CancellationToken ct)
    {
        GovernancePolicy.Page(offset, limit);
        await Admin(principal, institutionId, ct);
        return (await db.StaffChangeRequests.AsNoTracking().Where(x => x.InstitutionId == institutionId)
            .OrderByDescending(x => x.CreatedAtUtc).ThenBy(x => x.Id).Skip(offset).Take(limit)
            .ToListAsync(ct)).Select(Map).ToArray();
    }

    public async Task<StaffChangeView> StaffRequest(ClaimsPrincipal principal, Guid institutionId, Guid id, CancellationToken ct)
    {
        await Admin(principal, institutionId, ct);
        var request = await db.StaffChangeRequests.AsNoTracking().SingleOrDefaultAsync(x =>
            x.Id == id && x.InstitutionId == institutionId, ct) ?? throw ApiFault.NotFound();
        return Map(request);
    }

    public Task<StaffChangeView> DecideStaffRequest(ClaimsPrincipal principal,
        Guid institutionId, Guid id, DecideStaffChange decision, bool approve, CancellationToken ct)
        => retry.ExecuteAsync<GovernanceService, StaffChangeView>(service => service.DecideStaffRequestCore(principal, institutionId, id, decision, approve, ct), ct);

    private async Task<StaffChangeView> DecideStaffRequestCore(ClaimsPrincipal principal,
        Guid institutionId, Guid id, DecideStaffChange decision, bool approve, CancellationToken ct)
    {
        var actor = await Admin(principal, institutionId, ct);
        var reason = GovernancePolicy.RequiredText(decision.Reason, 1000, "review reason");
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var request = (await db.StaffChangeRequests
                .FromSqlInterpolated($"SELECT * FROM governance_staff_requests WHERE \"Id\" = {id} FOR UPDATE")
                .ToListAsync(ct)).SingleOrDefault();
            if (request is null || request.InstitutionId != institutionId) throw ApiFault.NotFound();
            // Re-read permissions when processing, never trust role claims from an old JWT.
            await Admin(principal, institutionId, ct);
            GovernancePolicy.Review(actor.Id, request.RequestedBy, request.TargetUserId,
                request.Version, decision.ExpectedVersion, request.Status);
            if (approve)
            {
                // Serialize changes to the target account as well as the request row.
                var targetRow = (await db.Users
                    .FromSqlInterpolated($"SELECT * FROM identity_users WHERE \"Id\" = {request.TargetUserId} FOR UPDATE")
                    .ToListAsync(ct)).SingleOrDefault() ?? throw ApiFault.NotFound();
                var target = await Target(institutionId, targetRow.Id, ct);
                IdentityResult result;
                switch (request.Action)
                {
                    case "AddRole":
                        if (!target.IsActive || await users.IsInRoleAsync(target, request.Role))
                            throw ApiFault.Conflict("Role change is no longer applicable.");
                        result = await users.AddToRoleAsync(target, request.Role);
                        break;
                    case "RemoveRole":
                        if (!target.IsActive || !await users.IsInRoleAsync(target, request.Role))
                            throw ApiFault.Conflict("Role change is no longer applicable.");
                        result = await users.RemoveFromRoleAsync(target, request.Role);
                        break;
                    case "Activate":
                    case "Deactivate":
                        if (target.IsActive == (request.Action == "Activate"))
                            throw ApiFault.Conflict("Account state already matches request.");
                        target.IsActive = request.Action == "Activate";
                        result = await users.UpdateAsync(target);
                        break;
                    default: throw ApiFault.Bad("Unsupported stored action.");
                }
                if (!result.Succeeded) throw ApiFault.Conflict("Identity rejected this change.");
                // Old sessions must not silently retain stale credentials/role claims.
                result = await users.UpdateSecurityStampAsync(target);
                if (!result.Succeeded) throw ApiFault.Conflict("Could not revoke existing target sessions.");
                await db.RefreshSessions.Where(x => x.UserId == target.Id && x.RevokedAtUtc == null)
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedAtUtc, DateTime.UtcNow), ct);
            }
            request.Status = approve ? "Approved" : "Rejected";
            request.ReviewedBy = actor.Id;
            request.ReviewReason = reason;
            request.ReviewedAtUtc = DateTime.UtcNow;
            request.Version++;
            security.Audit(actor.Id, institutionId, approve ? "governance.staff.approved" : "governance.staff.rejected", id.ToString());
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return Map(request);
        });
    }

    public Task<StaffChangeView> CancelStaffRequest(ClaimsPrincipal principal,
        Guid institutionId, Guid id, long expectedVersion, CancellationToken ct)
        => retry.ExecuteAsync<GovernanceService, StaffChangeView>(service => service.CancelStaffRequestCore(principal, institutionId, id, expectedVersion, ct), ct);

    private async Task<StaffChangeView> CancelStaffRequestCore(ClaimsPrincipal principal,
        Guid institutionId, Guid id, long expectedVersion, CancellationToken ct)
    {
        var actor = await Admin(principal, institutionId, ct);
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var request = (await db.StaffChangeRequests
                .FromSqlInterpolated($"SELECT * FROM governance_staff_requests WHERE \"Id\" = {id} FOR UPDATE")
                .ToListAsync(ct)).SingleOrDefault();
            if (request is null || request.InstitutionId != institutionId) throw ApiFault.NotFound();
            if (request.RequestedBy != actor.Id) throw ApiFault.Forbidden();
            if (request.Status != "Pending" || request.Version != expectedVersion)
                throw ApiFault.Conflict("Request is no longer pending or version is stale.");
            request.Status = "Cancelled"; request.Version++;
            request.ReviewedAtUtc = DateTime.UtcNow;
            security.Audit(actor.Id, institutionId, "governance.staff.cancelled", id.ToString());
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
            return Map(request);
        });
    }

    public async Task<EnrollmentBatchView> CreateBatch(ClaimsPrincipal principal, Guid institutionId,
        CreateEnrollmentBatch request, CancellationToken ct)
    {
        var actor = await Admin(principal, institutionId, ct);
        if (request.ClientRequestId == Guid.Empty || request.SectionId == Guid.Empty)
            throw ApiFault.Bad("Invalid batch or section ID.");
        var studentIds = GovernancePolicy.BatchStudents(request.StudentIds);
        var existing = await db.EnrollmentBatches.AsNoTracking().SingleOrDefaultAsync(x =>
            x.InstitutionId == institutionId && x.ClientRequestId == request.ClientRequestId, ct);
        if (existing is not null)
        {
            var existingItems = await db.EnrollmentBatchItems.AsNoTracking().Where(x => x.BatchId == existing.Id)
                .OrderBy(x => x.StudentId).ToListAsync(ct);
            if (existing.CreatedBy != actor.Id || existing.SectionId != request.SectionId ||
                !existingItems.Select(x => x.StudentId).SequenceEqual(studentIds))
                throw ApiFault.Conflict("Idempotency key reused with different batch details.");
            return Map(existing, existingItems);
        }
        if (!await db.CourseSections.AnyAsync(x => x.Id == request.SectionId && x.InstitutionId == institutionId, ct))
            throw ApiFault.NotFound();
        var batch = new EnrollmentBatch { InstitutionId = institutionId, SectionId = request.SectionId,
            ClientRequestId = request.ClientRequestId, CreatedBy = actor.Id };
        var items = studentIds.Select(studentId => new EnrollmentBatchItem { InstitutionId = institutionId,
            BatchId = batch.Id, StudentId = studentId }).ToArray();
        db.EnrollmentBatches.Add(batch);
        db.EnrollmentBatchItems.AddRange(items);
        security.Audit(actor.Id, institutionId, "governance.batch.created", batch.Id.ToString());
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { throw ApiFault.Conflict("Batch key already exists; retrieve the original batch."); }
        return Map(batch, items);
    }

    public async Task<IReadOnlyList<EnrollmentBatchView>> Batches(ClaimsPrincipal principal, Guid institutionId,
        int offset, int limit, CancellationToken ct)
    {
        GovernancePolicy.Page(offset, limit); await Admin(principal, institutionId, ct);
        var batches = await db.EnrollmentBatches.AsNoTracking().Where(x => x.InstitutionId == institutionId)
            .OrderByDescending(x => x.CreatedAtUtc).ThenBy(x => x.Id).Skip(offset).Take(limit).ToListAsync(ct);
        var batchIds = batches.Select(x => x.Id).ToArray();
        var items = await db.EnrollmentBatchItems.AsNoTracking().Where(x => batchIds.Contains(x.BatchId)).ToListAsync(ct);
        return batches.Select(x => Map(x, items.Where(y => y.BatchId == x.Id))).ToArray();
    }

    public async Task<EnrollmentBatchView> Batch(ClaimsPrincipal principal, Guid institutionId, Guid batchId, CancellationToken ct)
    {
        await Admin(principal, institutionId, ct);
        var batch = await db.EnrollmentBatches.AsNoTracking().SingleOrDefaultAsync(x =>
            x.Id == batchId && x.InstitutionId == institutionId, ct) ?? throw ApiFault.NotFound();
        var items = await db.EnrollmentBatchItems.AsNoTracking().Where(x => x.BatchId == batchId).ToListAsync(ct);
        return Map(batch, items);
    }

    private async Task<BatchPreview> EvaluateBatch(EnrollmentBatch batch,
        IReadOnlyList<EnrollmentBatchItem> items, CourseSection section, CancellationToken ct, bool recordDecisions = false)
    {
        var issues = new List<string>();
        if (batch.Status != "Draft") issues.Add("Batch is already committed.");
        if (!await db.AcademicTerms.AsNoTracking().AnyAsync(t => t.Id == section.TermId &&
            t.InstitutionId == section.InstitutionId && t.Status == "Active", ct))
            issues.Add("Academic term is not active.");
        if (section.Status != "Open") issues.Add("Section is not open.");
        if (await db.SectionGradePolicies.AsNoTracking().AnyAsync(x => x.SectionId == section.Id && x.PublishedAtUtc != null, ct))
            issues.Add("Final gradebook was already published.");
        var studentIds = items.Select(x => x.StudentId).ToArray();
        var students = await db.Users.AsNoTracking().Where(x => studentIds.Contains(x.Id))
            .Select(x => new { x.Id, x.InstitutionId, x.IsActive }).ToListAsync(ct);
        var studentRoles = await (from ur in db.UserRoles.AsNoTracking()
            join role in db.Roles.AsNoTracking() on ur.RoleId equals role.Id
            where studentIds.Contains(ur.UserId) && role.Name == AcademixRoles.Student
            select ur.UserId).Distinct().ToListAsync(ct);
        var roleIds = studentRoles.ToHashSet();
        var userMap = students.ToDictionary(x => x.Id);
        foreach (var item in items)
        {
            if (!userMap.TryGetValue(item.StudentId, out var student) || !student.IsActive ||
                student.InstitutionId != batch.InstitutionId || !roleIds.Contains(item.StudentId))
                issues.Add($"Invalid or inactive student: {item.StudentId}.");
        }
        foreach (var item in items)
        {
            var eligibility=await records.EligibilityForSection(section.Id,item.StudentId,ct,recordDecisions);
            if(!eligibility.Eligible)issues.Add($"Enrollment rejected for {item.StudentId}: {string.Join(", ",eligibility.RejectionReasons??[])}.");
        }
        var already = await db.StudentEnrollments.AsNoTracking()
            .Where(x => x.SectionId == section.Id && studentIds.Contains(x.StudentId))
            .Select(x => new { x.StudentId, x.Status }).ToListAsync(ct);
        foreach (var enrollment in already.Where(x => x.Status == "Active"))
            issues.Add($"Already enrolled: {enrollment.StudentId}.");
        var enrolled = await db.StudentEnrollments.AsNoTracking().CountAsync(x =>
            x.SectionId == section.Id && x.Status == "Active", ct);
        if (enrolled + items.Count > section.Capacity)
            issues.Add($"Insufficient capacity: {enrolled} existing + {items.Count} requested > {section.Capacity}.");
        return new BatchPreview(batch.Id, issues.Count == 0, section.Capacity, enrolled, issues,
            items.OrderBy(x => x.StudentId).Select(x => new EnrollmentBatchItemView(x.StudentId, x.Result)).ToArray());
    }

    public async Task<BatchPreview> PreviewBatch(ClaimsPrincipal principal, Guid institutionId, Guid batchId, CancellationToken ct)
    {
        await Admin(principal, institutionId, ct);
        var batch = await db.EnrollmentBatches.AsNoTracking().SingleOrDefaultAsync(x =>
            x.Id == batchId && x.InstitutionId == institutionId, ct) ?? throw ApiFault.NotFound();
        var section = await db.CourseSections.AsNoTracking().SingleOrDefaultAsync(x =>
            x.Id == batch.SectionId && x.InstitutionId == institutionId, ct) ?? throw ApiFault.NotFound();
        var items = await db.EnrollmentBatchItems.AsNoTracking().Where(x => x.BatchId == batchId).ToListAsync(ct);
        return await EvaluateBatch(batch, items, section, ct);
    }

    public Task<EnrollmentBatchView> CommitBatch(ClaimsPrincipal principal,
        Guid institutionId, Guid batchId, CancellationToken ct)
        => retry.ExecuteAsync<GovernanceService, EnrollmentBatchView>(service => service.CommitBatchCore(principal, institutionId, batchId, ct), ct);

    private async Task<EnrollmentBatchView> CommitBatchCore(ClaimsPrincipal principal,
        Guid institutionId, Guid batchId, CancellationToken ct)
    {
        var actor = await Admin(principal, institutionId, ct);
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var known = await db.EnrollmentBatches.AsNoTracking().SingleOrDefaultAsync(x =>
                x.Id == batchId && x.InstitutionId == institutionId, ct) ?? throw ApiFault.NotFound();
            await Academix.Api.Services.Academic.RegistrationCoordination.Lock(db, institutionId, ct);
            // SAME section-row lock as AcademicOperations.Enroll and UpdateSection.
            var section = (await db.CourseSections.FromSqlInterpolated(
                $"SELECT * FROM course_sections WHERE \"Id\" = {known.SectionId} FOR UPDATE")
                .ToListAsync(ct)).SingleOrDefault() ?? throw ApiFault.NotFound();
            if (section.InstitutionId != institutionId) throw ApiFault.NotFound();
            var batch = (await db.EnrollmentBatches.FromSqlInterpolated(
                $"SELECT * FROM governance_enrollment_batches WHERE \"Id\" = {batchId} FOR UPDATE")
                .ToListAsync(ct)).SingleOrDefault() ?? throw ApiFault.NotFound();
            if (batch.InstitutionId != institutionId || batch.SectionId != section.Id) throw ApiFault.NotFound();
            await Admin(principal, institutionId, ct);
            var items = await db.EnrollmentBatchItems.Where(x => x.BatchId == batchId)
                .OrderBy(x => x.StudentId).ToListAsync(ct);
            if (batch.Status == "Committed")
            {
                await tx.CommitAsync(ct);
                return Map(batch, items);
            }
            var preview = await EvaluateBatch(batch, items, section, ct,true);
            if (!preview.Ready) throw ApiFault.Conflict(string.Join(" ", preview.Issues.Take(5)));
            var ids = items.Select(x => x.StudentId).ToArray();
            await Academix.Api.Services.AcademicOperations.CampusIntegrity.Enrollment(db,institutionId,section.Id,ids,ct);
            var enrollments = await db.StudentEnrollments.Where(x => x.SectionId == section.Id && ids.Contains(x.StudentId))
                .ToListAsync(ct);
            var existing = enrollments.ToDictionary(x => x.StudentId);
            foreach (var item in items)
            {
                if (existing.TryGetValue(item.StudentId, out var enrollment))
                {
                    enrollment.Status = "Active";
                    enrollment.EnrolledAtUtc = DateTime.UtcNow;
                    item.Result = "Reactivated";
                }
                else
                {
                    db.StudentEnrollments.Add(new StudentEnrollment { InstitutionId = institutionId,
                        SectionId = section.Id, StudentId = item.StudentId });
                    item.Result = "Enrolled";
                }
            }
            batch.Status = "Committed"; batch.CommittedAtUtc = DateTime.UtcNow; batch.CommittedBy = actor.Id;
            security.Audit(actor.Id, institutionId, "governance.batch.committed", batch.Id.ToString());
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return Map(batch, items);
        });
    }

    public async Task<IReadOnlyList<GovernanceEventView>> Events(ClaimsPrincipal principal, Guid institutionId,
        int offset, int limit, CancellationToken ct)
    {
        GovernancePolicy.Page(offset, limit); await Admin(principal, institutionId, ct);
        return await db.SecurityAuditEvents.AsNoTracking().Where(x => x.InstitutionId == institutionId &&
            x.Action.StartsWith("governance."))
            .OrderByDescending(x => x.AtUtc).ThenBy(x => x.Id).Skip(offset).Take(limit)
            .Select(x => new GovernanceEventView(x.Id, x.ActorId, x.Action, x.Target, x.AtUtc)).ToListAsync(ct);
    }

    public async Task<GovernanceOverview> Overview(ClaimsPrincipal principal, Guid institutionId, CancellationToken ct)
    {
        await Admin(principal, institutionId, ct);
        return new GovernanceOverview(
            await db.StaffChangeRequests.AsNoTracking().CountAsync(x => x.InstitutionId == institutionId && x.Status == "Pending", ct),
            await db.EnrollmentBatches.AsNoTracking().CountAsync(x => x.InstitutionId == institutionId && x.Status == "Draft", ct),
            await db.EnrollmentBatches.AsNoTracking().CountAsync(x => x.InstitutionId == institutionId && x.Status == "Committed", ct),
            await db.SecurityAuditEvents.AsNoTracking().CountAsync(x => x.InstitutionId == institutionId && x.Action.StartsWith("governance."), ct));
    }
}
