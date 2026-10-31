using Academix.Api.Services.Infrastructure;
using System.Security.Claims;
using Academix.Api.Data;
using Academix.Api.Domain.Academic;
using Academix.Api.Domain.Curriculum;
using Academix.Api.Domain.Identity;
using Academix.Api.Domain.Lifecycle;
using Academix.Api.Domain.Records;
using Academix.Api.DTOs.Lifecycle;
using Academix.Api.Middleware;
using Academix.Api.Services.Curriculum;
using Academix.Api.Services.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Academix.Api.Services.Lifecycle;

public sealed partial class LifecycleService(AcademixDbContext db, IdentitySecurity security,
    CurriculumService curriculum, UserManager<ApplicationUser> users, RetryingServiceWork retry, DegreeAuditService degreeAudit)
{
    private static AdmissionView Admission(AdmissionApplication a) =>
        new(a.Id, a.StudentId, a.ProgramId, a.TermId, a.Statement, a.Status,
            a.Version, a.CreatedAtUtc, a.ReviewedAtUtc, a.DecisionReason);
    private static GraduationView Graduation(GraduationApplication g) =>
        new(g.Id, g.StudentId, g.ProgramId, g.PlanId, g.Status, g.Version, g.CreatedAtUtc,
            g.ReviewedAtUtc, g.DecisionReason, g.CredentialNumber, g.IssuedAtUtc, g.SourceInvalidatedAtUtc);

    private async Task<ApplicationUser> Student(ClaimsPrincipal principal, Guid institutionId, CancellationToken ct)
    {
        var (user, roles) = await security.CurrentAsync(principal, ct);
        if (user.InstitutionId != institutionId || !roles.Contains(AcademixRoles.Student)) throw ApiFault.Forbidden();
        return user;
    }

    private void Log(Guid institution, Guid student, Guid actor, string kind, Guid id, string action, string reason)
    {
        db.LifecycleDecisionEvents.Add(new LifecycleDecisionEvent
        {
            InstitutionId = institution, StudentId = student, ActorId = actor,
            EntityKind = kind, EntityId = id, Action = action, Reason = reason
        });
        security.Audit(actor, institution, $"lifecycle.{kind}.{action}", id.ToString());
    }

    public async Task<AdmissionView> Apply(ClaimsPrincipal principal, Guid programId, SubmitAdmission body, CancellationToken ct)
    {
        var program = await db.AcademicPrograms.AsNoTracking().SingleOrDefaultAsync(x => x.Id == programId, ct)
            ?? throw ApiFault.NotFound();
        var student = await Student(principal, program.InstitutionId, ct);
        if (!program.IsActive || !await db.AcademicTerms.AnyAsync(x => x.Id == body.TermId &&
            x.InstitutionId == program.InstitutionId, ct)) throw ApiFault.Bad("Invalid program or admission term.");
        var statement = LifecyclePolicy.Description(body.Statement, 10, 1500);
        if (await db.StudentProgramMemberships.AnyAsync(x => x.StudentId == student.Id &&
            x.InstitutionId == program.InstitutionId, ct)) throw ApiFault.Conflict("Student already admitted.");
        if (await db.AdmissionApplications.AnyAsync(x => x.StudentId == student.Id &&
            x.InstitutionId == program.InstitutionId && x.Status == "Submitted", ct))
            throw ApiFault.Conflict("There is already a pending admission application.");
        var row = new AdmissionApplication { StudentId = student.Id, InstitutionId = program.InstitutionId,
            ProgramId = programId, TermId = body.TermId, Statement = statement };
        db.AdmissionApplications.Add(row);
        Log(row.InstitutionId, row.StudentId, student.Id, "admission", row.Id, "submitted", "Student application");
        await db.SaveChangesAsync(ct);
        return Admission(row);
    }

    public async Task<IReadOnlyList<AdmissionView>> MyAdmissions(ClaimsPrincipal principal, CancellationToken ct)
    {
        var (user, roles) = await security.CurrentAsync(principal, ct);
        if (user.InstitutionId is not Guid inst || !roles.Contains(AcademixRoles.Student)) throw ApiFault.Forbidden();
        return (await db.AdmissionApplications.AsNoTracking().Where(x => x.StudentId == user.Id && x.InstitutionId == inst)
            .OrderByDescending(x => x.CreatedAtUtc).Take(100).ToListAsync(ct)).Select(Admission).ToArray();
    }

    public async Task<IReadOnlyList<AdmissionView>> InstitutionAdmissions(ClaimsPrincipal principal, Guid inst,
        string? status, CancellationToken ct)
    {
        await security.RequireAdminAsync(principal, inst, ct);
        if (status is not null && status is not ("Submitted" or "Approved" or "Rejected")) throw ApiFault.Bad("Invalid status.");
        var query = db.AdmissionApplications.AsNoTracking().Where(x => x.InstitutionId == inst);
        if (status is not null) query = query.Where(x => x.Status == status);
        return (await query.OrderByDescending(x => x.CreatedAtUtc).Take(200).ToListAsync(ct)).Select(Admission).ToArray();
    }

    public Task<AdmissionView> DecideAdmission(ClaimsPrincipal principal, Guid id, LifecycleDecision body,
        CancellationToken ct)
        => retry.ExecuteAsync<LifecycleService, AdmissionView>(service => service.DecideAdmissionCore(principal, id, body, ct), ct);

    private async Task<AdmissionView> DecideAdmissionCore(ClaimsPrincipal principal, Guid id, LifecycleDecision body,
        CancellationToken ct)
    {
        var original = await db.AdmissionApplications.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw ApiFault.NotFound();
        var actor = await security.RequireAdminAsync(principal, original.InstitutionId, ct);
        LifecyclePolicy.DisallowSelfApproval(actor.Id, original.StudentId);
        var decision = LifecyclePolicy.Decision(body.Decision);
        var reason = LifecyclePolicy.Description(body.Reason, 5, 500);
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            // Lock the user first, consistently with existing RecordsService.Admit.
            var student = (await db.Users.FromSqlInterpolated(
                $"SELECT * FROM identity_users WHERE \"Id\" = {original.StudentId} FOR UPDATE").ToListAsync(ct)).SingleOrDefault()
                ?? throw ApiFault.NotFound();
            var row = (await db.AdmissionApplications.FromSqlInterpolated(
                $"SELECT * FROM admission_applications WHERE \"Id\" = {id} FOR UPDATE").ToListAsync(ct)).SingleOrDefault()
                ?? throw ApiFault.NotFound();
            actor = await security.RequireAdminAsync(principal, row.InstitutionId, ct);
            LifecyclePolicy.DisallowSelfApproval(actor.Id, row.StudentId);
            LifecyclePolicy.RequirePending(row.Status, row.Version, body.ExpectedVersion);
            if (decision == "Approve")
            {
                if (!student.IsActive || student.InstitutionId != row.InstitutionId ||
                    !await users.IsInRoleAsync(student, AcademixRoles.Student))
                    throw ApiFault.Conflict("Student is no longer eligible.");
                if (!await db.AcademicPrograms.AnyAsync(x => x.Id == row.ProgramId &&
                    x.InstitutionId == row.InstitutionId && x.IsActive, ct) ||
                    !await db.AcademicTerms.AnyAsync(x => x.Id == row.TermId && x.InstitutionId == row.InstitutionId, ct))
                    throw ApiFault.Conflict("Program or term changed.");
                if (await db.StudentProgramMemberships.AnyAsync(x => x.StudentId == row.StudentId &&
                    x.InstitutionId == row.InstitutionId, ct)) throw ApiFault.Conflict("Student already admitted.");
                db.StudentProgramMemberships.Add(new StudentProgramMembership { InstitutionId = row.InstitutionId,
                    StudentId = row.StudentId, ProgramId = row.ProgramId,
                    AdmissionTermId = row.TermId, CurrentTermId = row.TermId });
                db.StudentLifecycleEvents.Add(new StudentLifecycleEvent { InstitutionId = row.InstitutionId,
                    StudentId = row.StudentId, ActorId = actor.Id, Kind = "program.admitted",
                    Detail = $"Admission application {id}" });
            }
            row.Status = decision == "Approve" ? "Approved" : "Rejected";
            row.Version++; row.ReviewedAtUtc = DateTime.UtcNow; row.ReviewedById = actor.Id;
            row.DecisionReason = reason;
            Log(row.InstitutionId, row.StudentId, actor.Id, "admission", row.Id, row.Status, reason);
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
            return Admission(row);
        });
    }

    private async Task<TransferView> Transfer(TransferCreditPetition p, CancellationToken ct)
    {
        var courses = await db.TransferCreditLines.AsNoTracking().Where(x => x.PetitionId == p.Id)
            .OrderBy(x => x.TargetCourseId).Select(x => new TransferCourseView(x.TargetCourseId,
                x.OriginCourseCode, x.OriginCourseTitle, x.ProposedCreditHours)).ToListAsync(ct);
        return new TransferView(p.Id, p.StudentId, p.ProgramId, p.OriginInstitution, p.Status,
            p.Version, p.DecisionReason, courses);
    }

    public async Task<TransferView> RequestTransfer(ClaimsPrincipal principal, SubmitTransferCredit body,
        CancellationToken ct)
    {
        LifecyclePolicy.ValidateTransfer(body);
        var (user, roles) = await security.CurrentAsync(principal, ct);
        if (user.InstitutionId is not Guid inst || !roles.Contains(AcademixRoles.Student)) throw ApiFault.Forbidden();
        var membership = await db.StudentProgramMemberships.AsNoTracking().SingleOrDefaultAsync(x =>
            x.StudentId == user.Id && x.InstitutionId == inst && x.Status == "Active", ct)
            ?? throw ApiFault.Conflict("Active program membership required.");
        var courseIds = body.Courses.Select(x => x.TargetCourseId).ToArray();
        var validCount = await db.ProgramCourses.AsNoTracking().CountAsync(x => x.InstitutionId == inst &&
            x.ProgramId == membership.ProgramId && courseIds.Contains(x.CourseId), ct);
        if (validCount != courseIds.Length) throw ApiFault.Bad("All target courses must belong to the active program.");
        var p = new TransferCreditPetition { InstitutionId = inst, StudentId = user.Id, ProgramId = membership.ProgramId,
            OriginInstitution = body.OriginInstitution.Trim() };
        db.TransferCreditPetitions.Add(p);
        foreach (var course in body.Courses) db.TransferCreditLines.Add(new TransferCreditLine {
            InstitutionId = inst, PetitionId = p.Id, TargetCourseId = course.TargetCourseId,
            OriginCourseCode = course.OriginCourseCode.Trim(), OriginCourseTitle = course.OriginCourseTitle.Trim(),
            ProposedCreditHours = course.ProposedCreditHours });
        Log(inst, user.Id, user.Id, "transfer", p.Id, "submitted", "Transfer equivalency request");
        await db.SaveChangesAsync(ct);
        return await Transfer(p, ct);
    }

    public async Task<IReadOnlyList<TransferView>> MyTransfers(ClaimsPrincipal principal, CancellationToken ct)
    {
        var (user, roles) = await security.CurrentAsync(principal, ct);
        if (user.InstitutionId is not Guid inst || !roles.Contains(AcademixRoles.Student)) throw ApiFault.Forbidden();
        var rows = await db.TransferCreditPetitions.AsNoTracking().Where(x => x.InstitutionId == inst && x.StudentId == user.Id)
            .OrderByDescending(x => x.CreatedAtUtc).Take(100).ToListAsync(ct);
        var result = new List<TransferView>(); foreach (var p in rows) result.Add(await Transfer(p, ct)); return result;
    }

    public async Task<IReadOnlyList<TransferView>> InstitutionTransfers(ClaimsPrincipal principal, Guid inst, CancellationToken ct)
    {
        await security.RequireAdminAsync(principal, inst, ct);
        var rows = await db.TransferCreditPetitions.AsNoTracking().Where(x => x.InstitutionId == inst)
            .OrderByDescending(x => x.CreatedAtUtc).Take(200).ToListAsync(ct);
        var result = new List<TransferView>(); foreach (var p in rows) result.Add(await Transfer(p, ct)); return result;
    }

    public Task<TransferView> DecideTransfer(ClaimsPrincipal principal, Guid id, LifecycleDecision body,
        CancellationToken ct)
        => retry.ExecuteAsync<LifecycleService, TransferView>(service => service.DecideTransferCore(principal, id, body, ct), ct);

    private async Task<TransferView> DecideTransferCore(ClaimsPrincipal principal, Guid id, LifecycleDecision body,
        CancellationToken ct)
    {
        var original = await db.TransferCreditPetitions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw ApiFault.NotFound();
        var actor = await security.RequireAdminAsync(principal, original.InstitutionId, ct);
        LifecyclePolicy.DisallowSelfApproval(actor.Id, original.StudentId);
        var decision = LifecyclePolicy.Decision(body.Decision);
        var reason = LifecyclePolicy.Description(body.Reason, 5, 500);
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await db.Users.FromSqlInterpolated(
                $"SELECT * FROM identity_users WHERE \"Id\" = {original.StudentId} FOR UPDATE").ToListAsync(ct);
            var p = (await db.TransferCreditPetitions.FromSqlInterpolated(
                $"SELECT * FROM transfer_credit_petitions WHERE \"Id\" = {id} FOR UPDATE").ToListAsync(ct)).SingleOrDefault()
                ?? throw ApiFault.NotFound();
            actor = await security.RequireAdminAsync(principal, p.InstitutionId, ct);
            LifecyclePolicy.DisallowSelfApproval(actor.Id, p.StudentId);
            LifecyclePolicy.RequirePending(p.Status, p.Version, body.ExpectedVersion);
            if (decision == "Approve")
            {
                if (!await db.StudentProgramMemberships.AnyAsync(x => x.InstitutionId == p.InstitutionId &&
                    x.StudentId == p.StudentId && x.ProgramId == p.ProgramId && x.Status == "Active", ct))
                    throw ApiFault.Conflict("Program membership is no longer active.");
                var assignment = await db.StudentCurriculumAssignments.AsNoTracking()
                    .SingleOrDefaultAsync(x => x.StudentId == p.StudentId && x.InstitutionId == p.InstitutionId, ct)
                    ?? throw ApiFault.Conflict("An assigned published curriculum is required to approve equivalencies.");
                var approvedPlan = await db.CurriculumPlans.AsNoTracking()
                    .SingleAsync(x => x.Id == assignment.PlanId, ct);
                if (approvedPlan.ProgramId != p.ProgramId || approvedPlan.Status != "Published")
                    throw ApiFault.Conflict("Student's published curriculum no longer matches this program.");
                var targetCourses = await db.TransferCreditLines.AsNoTracking()
                    .Where(x => x.PetitionId == p.Id)
                    .Select(x => new { x.TargetCourseId, x.ProposedCreditHours }).ToListAsync(ct);
                var planCourses = await db.CurriculumCourses.AsNoTracking()
                    .Where(x => x.PlanId == assignment.PlanId)
                    .Select(x => new { x.CourseId, x.CreditHours }).ToListAsync(ct);
                if (targetCourses.Any(x => !planCourses.Any(c => c.CourseId == x.TargetCourseId &&
                    c.CreditHours <= x.ProposedCreditHours)))
                    throw ApiFault.Conflict("Equivalencies must fully satisfy courses in the assigned curriculum.");
                var targetIds = targetCourses.Select(x => x.TargetCourseId).ToArray();
                if (await db.TransferCreditLines.AsNoTracking().Join(db.TransferCreditPetitions.AsNoTracking(),
                    x => x.PetitionId, y => y.Id, (x, y) => new { x, y })
                    .AnyAsync(x => x.y.InstitutionId == p.InstitutionId && x.y.StudentId == p.StudentId &&
                        x.y.Status == "Approved" && targetIds.Contains(x.x.TargetCourseId), ct))
                    throw ApiFault.Conflict("A target course already has an approved equivalency.");
            }
            p.Status = decision == "Approve" ? "Approved" : "Rejected";
            p.Version++; p.ReviewedAtUtc = DateTime.UtcNow; p.ReviewedById = actor.Id; p.DecisionReason = reason;
            Log(p.InstitutionId, p.StudentId, actor.Id, "transfer", p.Id, p.Status, reason);
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
            return await Transfer(p, ct);
        });
    }

    public async Task<TransferView[]> ApprovedCredits(ClaimsPrincipal principal, Guid studentId, CancellationToken ct)
    {
        var student = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == studentId, ct)
            ?? throw ApiFault.NotFound();
        if (student.InstitutionId is not Guid inst) throw ApiFault.NotFound();
        await security.RequireStudentAsync(principal, studentId, inst, ct);
        var rows = await db.TransferCreditPetitions.AsNoTracking().Where(x => x.StudentId == studentId &&
            x.InstitutionId == inst && x.Status == "Approved").OrderBy(x => x.CreatedAtUtc).ToListAsync(ct);
        var result = new List<TransferView>(); foreach (var row in rows) result.Add(await Transfer(row, ct));
        return result.ToArray();
    }

    public async Task<IReadOnlyList<GraduationView>> MyGraduations(ClaimsPrincipal principal, CancellationToken ct)
    {
        var (user, roles) = await security.CurrentAsync(principal, ct);
        if (user.InstitutionId is not Guid inst || !roles.Contains(AcademixRoles.Student)) throw ApiFault.Forbidden();
        return (await db.GraduationApplications.AsNoTracking().Where(x => x.StudentId == user.Id &&
            x.InstitutionId == inst).OrderByDescending(x => x.CreatedAtUtc).Take(100).ToListAsync(ct))
            .Select(Graduation).ToArray();
    }

    public async Task<IReadOnlyList<GraduationView>> InstitutionGraduations(ClaimsPrincipal principal,
        Guid inst, CancellationToken ct)
    {
        await security.RequireAdminAsync(principal, inst, ct);
        return (await db.GraduationApplications.AsNoTracking().Where(x => x.InstitutionId == inst)
            .OrderByDescending(x => x.CreatedAtUtc).Take(200).ToListAsync(ct)).Select(Graduation).ToArray();
    }

    public Task<GraduationView> ReviewGraduation(ClaimsPrincipal principal, Guid id,
        LifecycleDecision body, CancellationToken ct)
        => retry.ExecuteAsync<LifecycleService, GraduationView>(service => service.ReviewGraduationCore(principal, id, body, ct), ct);

    private async Task<GraduationView> ReviewGraduationCore(ClaimsPrincipal principal, Guid id,
        LifecycleDecision body, CancellationToken ct)
    {
        var original = await db.GraduationApplications.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw ApiFault.NotFound();
        var actor = await security.RequireAdminAsync(principal, original.InstitutionId, ct);
        LifecyclePolicy.DisallowSelfApproval(actor.Id, original.StudentId);
        var decision = LifecyclePolicy.Decision(body.Decision);
        var reason = LifecyclePolicy.Description(body.Reason, 5, 500);

        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await Academix.Api.Services.Academic.RegistrationCoordination.Lock(db, original.InstitutionId, ct);
            await security.RequireAdminAsync(principal,original.InstitutionId,ct);
            var g = (await db.GraduationApplications.FromSqlInterpolated(
                $"SELECT * FROM graduation_applications WHERE \"Id\" = {id} FOR UPDATE").ToListAsync(ct)).SingleOrDefault()
                ?? throw ApiFault.NotFound();
            Academix.Api.Services.Advising.AcademicProgressRules.Version(g.Version,body.ExpectedVersion);
            // Review stages are captured at submission; changing administrative
            // policy must not strand an already approved request in an unreachable state.
            var required=g.RequiresDepartmentReview;
            Academix.Api.Services.Advising.AcademicProgressRules.GraduationReview(g.Status,decision=="Approve"&&required,g.DepartmentReviewedAtUtc.HasValue);
            if (decision == "Approve")
            {
                if(required&&g.DepartmentReviewedById==actor.Id)throw ApiFault.Forbidden();
                var evidence=await degreeAudit.Evaluate(principal,g.StudentId,ct);
                await Academix.Api.Services.Finance.FinanceDebt.RequireClearance(db,g.InstitutionId,g.StudentId,"Graduation",g.CreatedAtUtc,ct);
                if (evidence.Audit.PlanId != g.PlanId || evidence.Eligibility!="Eligible")
                    throw ApiFault.Conflict("Graduation eligibility changed; cannot approve.");
                g.ApprovalEvidenceJson=System.Text.Json.JsonSerializer.Serialize(evidence);
            }
            g.Status = decision == "Approve" ? "Approved" : "Rejected";
            g.Version++; g.ReviewedById = actor.Id; g.ReviewedAtUtc = DateTime.UtcNow; g.DecisionReason = reason;
            Log(g.InstitutionId, g.StudentId, actor.Id, "graduation", g.Id, g.Status, reason);
            OutboxService.Record(db,g.InstitutionId,"GraduationReviewed",g.Id,new{StudentId=g.StudentId,ApplicationId=g.Id,Status=g.Status});
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
            return Graduation(g);
        }
    }

    public Task<GraduationView> IssueGraduation(ClaimsPrincipal principal, Guid id,
        IssueGraduation body, CancellationToken ct)
        => retry.ExecuteAsync<LifecycleService, GraduationView>(service => service.IssueGraduationCore(principal, id, body, ct), ct);

    private async Task<GraduationView> IssueGraduationCore(ClaimsPrincipal principal, Guid id,
        IssueGraduation body, CancellationToken ct)
    {
        var original = await db.GraduationApplications.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw ApiFault.NotFound();
        var actor = await security.RequireAdminAsync(principal, original.InstitutionId, ct);
        LifecyclePolicy.DisallowSelfApproval(actor.Id, original.StudentId);

        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await Academix.Api.Services.Academic.RegistrationCoordination.Lock(db, original.InstitutionId, ct);
            await security.RequireAdminAsync(principal,original.InstitutionId,ct);
            var membership = (await db.StudentProgramMemberships.FromSqlInterpolated(
                $"SELECT * FROM student_program_memberships WHERE \"StudentId\" = {original.StudentId} AND \"InstitutionId\" = {original.InstitutionId} FOR UPDATE")
                .ToListAsync(ct)).SingleOrDefault() ?? throw ApiFault.Conflict("Program membership missing.");
            var g = (await db.GraduationApplications.FromSqlInterpolated(
                $"SELECT * FROM graduation_applications WHERE \"Id\" = {id} FOR UPDATE").ToListAsync(ct)).SingleOrDefault()
                ?? throw ApiFault.NotFound();
            if(g.Status=="Issued"){await tx.CommitAsync(ct);return Graduation(g);}
            if (g.Status != "Approved" || g.Version != body.ExpectedVersion)
                throw ApiFault.Conflict("Graduation review is not approved or the version is stale.");
            if (membership.Status != "Active" || membership.ProgramId != g.ProgramId)
                throw ApiFault.Conflict("Program membership changed.");
            var finalEvidence=await degreeAudit.Evaluate(principal,g.StudentId,ct);
            await Academix.Api.Services.Finance.FinanceDebt.RequireClearance(db,g.InstitutionId,g.StudentId,"Graduation",g.CreatedAtUtc,ct);
            if (finalEvidence.Audit.PlanId != g.PlanId || finalEvidence.Eligibility!="Eligible")
                throw ApiFault.Conflict("Graduation eligibility changed; cannot issue credential.");
            var departmentRequired=g.RequiresDepartmentReview;
            if(departmentRequired&&!g.DepartmentReviewedAtUtc.HasValue)throw ApiFault.Conflict("Required department review is missing.");
            g.FinalEvidenceJson=System.Text.Json.JsonSerializer.Serialize(finalEvidence);
            if (await db.GraduationApplications.AnyAsync(x => x.StudentId == g.StudentId &&
                x.InstitutionId == g.InstitutionId && x.Status == "Issued" && x.Id != g.Id, ct))
                throw ApiFault.Conflict("An issued credential already exists for the student.");
            g.Status = "Issued"; g.Version++; g.IssuedById = actor.Id; g.IssuedAtUtc = DateTime.UtcNow;
            g.CredentialNumber = $"ACX-{DateTime.UtcNow:yyyy}-{Guid.NewGuid():N}".ToUpperInvariant();
            membership.Status = "Completed"; membership.Version++;
            await db.Set<Academix.Api.Domain.Advising.AdvisorAssignment>().Where(x=>x.InstitutionId==g.InstitutionId&&x.StudentId==g.StudentId&&x.EndedAtUtc==null)
                .ExecuteUpdateAsync(s=>s.SetProperty(x=>x.EndedAtUtc,g.IssuedAtUtc),ct);
            db.StudentLifecycleEvents.Add(new StudentLifecycleEvent { InstitutionId = g.InstitutionId,
                StudentId = g.StudentId, ActorId = actor.Id, Kind = "program.completed",
                Detail = $"Graduation application {g.Id}" });
            Log(g.InstitutionId, g.StudentId, actor.Id, "graduation", g.Id, "issued", "Credential issued");
            OutboxService.Record(db,g.InstitutionId,"GraduationFinalized",g.Id,new{StudentId=g.StudentId,ApplicationId=g.Id,CredentialNumber=g.CredentialNumber});
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
            return Graduation(g);
        }
    }

    public async Task<GraduationView> Credential(ClaimsPrincipal principal, Guid studentId, CancellationToken ct)
    {
        var student = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == studentId, ct)
            ?? throw ApiFault.NotFound();
        if (student.InstitutionId is not Guid inst) throw ApiFault.NotFound();
        await security.RequireStudentAsync(principal, studentId, inst, ct);
        var g = await db.GraduationApplications.AsNoTracking().SingleOrDefaultAsync(x => x.InstitutionId == inst &&
            x.StudentId == studentId && x.Status == "Issued", ct) ?? throw ApiFault.NotFound();
        if(g.SourceInvalidatedAtUtc.HasValue)throw ApiFault.Conflict("Issued credential requires registrar reassessment after academic source changes.");
        return Graduation(g);
    }

    public async Task<IReadOnlyList<LifecycleEventView>> Events(ClaimsPrincipal principal, Guid inst,
        int page, CancellationToken ct)
    {
        await security.RequireAdminAsync(principal, inst, ct);
        if (page is < 1 or > 10000) throw ApiFault.Bad("Page must be between 1 and 10000.");
        return await db.LifecycleDecisionEvents.AsNoTracking().Where(x => x.InstitutionId == inst)
            .OrderByDescending(x => x.AtUtc).ThenByDescending(x => x.Id).Skip((page - 1) * 50).Take(50)
            .Select(x => new LifecycleEventView(x.Id, x.EntityKind, x.EntityId, x.Action,
                x.ActorId, x.Reason, x.AtUtc)).ToListAsync(ct);
    }
}


