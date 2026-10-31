using Academix.Api.Services.Infrastructure;
using System.Security.Claims;
using Academix.Api.Data;
using Academix.Api.Domain.Academic;
using Academix.Api.Domain.Assessment;
using Academix.Api.Domain.Courses;
using Academix.Api.Domain.Identity;
using Academix.Api.Domain.Records;
using Academix.Api.DTOs.Records;
using Academix.Api.Middleware;
using Academix.Api.Services.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Academix.Api.Services.Records;

public sealed class RecordsService(AcademixDbContext db, IdentitySecurity security,
    UserManager<ApplicationUser> users, RetryingServiceWork retry)
{
    private static MembershipView Map(StudentProgramMembership x) =>
        new(x.Id, x.StudentId, x.ProgramId, x.AdmissionTermId, x.CurrentTermId, x.Status, x.Version);

    private static StandingView Map(StudentTermStanding x) =>
        new(x.TermId, x.StudentId, x.Status, x.CompletedCourses, x.PassedCourses, x.AveragePercentage, x.Version);

    private async Task<ApplicationUser> Admin(ClaimsPrincipal principal, Guid institutionId, CancellationToken ct)
        => await security.RequireAdminAsync(principal, institutionId, ct);

    private async Task<Guid> StudentInstitution(ClaimsPrincipal principal, Guid studentId, CancellationToken ct)
    {
        var target = await users.FindByIdAsync(studentId.ToString()) ?? throw ApiFault.NotFound();
        if (target.InstitutionId is not Guid inst) throw ApiFault.NotFound();
        await security.RequireStudentAsync(principal, studentId, inst, ct);
        return inst;
    }

    private void Event(Guid inst, Guid student, Guid actor, string kind, string detail)
    {
        db.StudentLifecycleEvents.Add(new StudentLifecycleEvent {
            InstitutionId = inst, StudentId = student, ActorId = actor, Kind = kind, Detail = detail });
        security.Audit(actor, inst, "records." + kind, student.ToString());
    }

    public Task<MembershipView> Admit(ClaimsPrincipal principal, Guid programId, AdmitStudent request, CancellationToken ct)
        => retry.ExecuteAsync<RecordsService, MembershipView>(service => service.AdmitCore(principal, programId, request, ct), ct);

    private async Task<MembershipView> AdmitCore(ClaimsPrincipal principal, Guid programId, AdmitStudent request, CancellationToken ct)
    {
        await using var tx=await db.Database.BeginTransactionAsync(ct);
        var result=await AdmitWithinTransaction(principal, programId, request, ct);
        await tx.CommitAsync(ct);return result;
    }
    internal async Task<MembershipView> AdmitWithinTransaction(ClaimsPrincipal principal, Guid programId, AdmitStudent request, CancellationToken ct)
    {
        if(db.Database.CurrentTransaction is null)throw new InvalidOperationException("Academic transaction required.");

        await db.Users.FromSqlInterpolated($"SELECT * FROM identity_users WHERE \"Id\" = {request.StudentId} FOR UPDATE")
            .ToListAsync(ct);

        var program = await db.AcademicPrograms.AsNoTracking().SingleOrDefaultAsync(x => x.Id == programId, ct)
            ?? throw ApiFault.NotFound();
        var actor = await Admin(principal, program.InstitutionId, ct);
        var student = await users.FindByIdAsync(request.StudentId.ToString()) ?? throw ApiFault.NotFound();
        if (!program.IsActive || !student.IsActive || student.InstitutionId != program.InstitutionId ||
            !await users.IsInRoleAsync(student, AcademixRoles.Student)) throw ApiFault.Bad("Invalid student or program.");
        if (!await db.AcademicTerms.AnyAsync(x => x.Id == request.AdmissionTermId &&
            x.InstitutionId == program.InstitutionId, ct)) throw ApiFault.Bad("Invalid admission term.");
        if (await db.StudentProgramMemberships.AnyAsync(x => x.InstitutionId == program.InstitutionId &&
            x.StudentId == request.StudentId, ct)) throw ApiFault.Conflict("Student already has a program membership.");
        var item = new StudentProgramMembership { InstitutionId = program.InstitutionId,
            StudentId = request.StudentId, ProgramId = programId,
            AdmissionTermId = request.AdmissionTermId, CurrentTermId = request.AdmissionTermId };
        db.StudentProgramMemberships.Add(item);
        Event(program.InstitutionId, request.StudentId, actor.Id, "program.admitted", programId.ToString());
        await db.SaveChangesAsync(ct);
        return Map(item);
    }

    public async Task<MembershipView> Membership(ClaimsPrincipal principal, Guid studentId, CancellationToken ct)
    {
        var inst = await StudentInstitution(principal, studentId, ct);
        var record = await db.StudentProgramMemberships.AsNoTracking()
            .SingleOrDefaultAsync(x => x.StudentId == studentId && x.InstitutionId == inst, ct) ?? throw ApiFault.NotFound();
        return Map(record);
    }

    public Task<MembershipView> Advance(ClaimsPrincipal principal, Guid studentId,
        ProgressProgram request, CancellationToken ct)
        => retry.ExecuteAsync<RecordsService, MembershipView>(service => service.AdvanceCore(principal, studentId, request, ct), ct);

    private async Task<MembershipView> AdvanceCore(ClaimsPrincipal principal, Guid studentId,
        ProgressProgram request, CancellationToken ct)
    {
        var inst = await StudentInstitution(principal, studentId, ct);
        var actor = await Admin(principal, inst, ct);
        var reason = RecordsPolicy.Reason(request.Reason);
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var item = (await db.StudentProgramMemberships.FromSqlInterpolated(
                $"SELECT * FROM student_program_memberships WHERE \"StudentId\" = {studentId} AND \"InstitutionId\" = {inst} FOR UPDATE")
                .ToListAsync(ct)).SingleOrDefault() ?? throw ApiFault.NotFound();
            actor = await Admin(principal, inst, ct);
            if (item.Status != "Active" || item.Version != request.ExpectedVersion) throw ApiFault.Conflict("Stale program membership.");
            var now = await db.AcademicTerms.AsNoTracking().SingleAsync(x => x.Id == item.CurrentTermId, ct);
            var next = await db.AcademicTerms.AsNoTracking().SingleOrDefaultAsync(x => x.Id == request.NextTermId && x.InstitutionId == inst, ct)
                ?? throw ApiFault.NotFound();
            if (next.Id == now.Id || next.StartsOn <= now.StartsOn)
                throw ApiFault.Bad("Next term must begin after current term.");
            if (await db.CourseSections.AsNoTracking().Join(db.StudentEnrollments.AsNoTracking(),
                s => s.Id, e => e.SectionId, (s, e) => new { s, e })
                .AnyAsync(x => x.e.StudentId == studentId && x.e.Status == "Active" &&
                    x.s.TermId == item.CurrentTermId, ct))
                throw ApiFault.Conflict("Resolve current-term active enrollments before progression.");
            item.CurrentTermId = next.Id; item.Version++;
            Event(inst, studentId, actor.Id, "program.progressed", reason);
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
            return Map(item);
        });
    }

    public async Task<IReadOnlyList<PrerequisiteView>> Prerequisites(ClaimsPrincipal principal, Guid courseId, CancellationToken ct)
    {
        var course = await db.Courses.AsNoTracking().SingleOrDefaultAsync(x => x.Id == courseId, ct)
            ?? throw ApiFault.NotFound();
        await Admin(principal, course.InstitutionId, ct);
        return await db.CoursePrerequisites.AsNoTracking().Where(x => x.CourseId == courseId)
            .OrderBy(x => x.RequiredCourseId)
            .Select(x => new PrerequisiteView(x.RequiredCourseId, x.MinimumPercentage)).ToListAsync(ct);
    }

    public Task<IReadOnlyList<PrerequisiteView>> AddPrerequisite(ClaimsPrincipal principal, Guid courseId,
        AddPrerequisiteRequest request, CancellationToken ct)
        => retry.ExecuteAsync<RecordsService, IReadOnlyList<PrerequisiteView>>(service => service.AddPrerequisiteCore(principal, courseId, request, ct), ct);

    private async Task<IReadOnlyList<PrerequisiteView>> AddPrerequisiteCore(ClaimsPrincipal principal, Guid courseId,
        AddPrerequisiteRequest request, CancellationToken ct)
    {
        var course = await db.Courses.AsNoTracking().SingleOrDefaultAsync(x => x.Id == courseId, ct)
            ?? throw ApiFault.NotFound();
        await Admin(principal, course.InstitutionId, ct);
        RecordsPolicy.Percentage(request.MinimumPercentage);
        if (!await db.Courses.AnyAsync(x => x.Id == request.RequiredCourseId &&
            x.InstitutionId == course.InstitutionId && x.IsActive, ct)) throw ApiFault.Bad("Required course not found in institution.");
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            // Institution row serializes all prerequisite graph mutations.
            await db.Institutions.FromSqlInterpolated($"SELECT * FROM institutions WHERE \"Id\" = {course.InstitutionId} FOR UPDATE")
                .ToListAsync(ct);
            // Prevent mutations for courses already accepting enrollment.
            if (await db.CourseSections.AnyAsync(x => x.CourseId == courseId && x.Status == "Open", ct))
                throw ApiFault.Conflict("Close all open sections before editing prerequisites.");
            var edges = await db.CoursePrerequisites.AsNoTracking().Where(x => x.InstitutionId == course.InstitutionId)
                .Select(x => new { x.CourseId, x.RequiredCourseId }).ToListAsync(ct);
            var graph = edges.GroupBy(x => x.CourseId).ToDictionary(g => g.Key,
                g => g.Select(x => x.RequiredCourseId).ToArray());
            RecordsPolicy.DistinctCourses(courseId, request.RequiredCourseId, graph);
            if (edges.Any(x => x.CourseId == courseId && x.RequiredCourseId == request.RequiredCourseId))
                throw ApiFault.Conflict("Prerequisite already exists.");
            db.CoursePrerequisites.Add(new CoursePrerequisite { InstitutionId = course.InstitutionId,
                CourseId = courseId, RequiredCourseId = request.RequiredCourseId,
                MinimumPercentage = request.MinimumPercentage });
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        });
        return await Prerequisites(principal, courseId, ct);
    }

    public Task RemovePrerequisite(ClaimsPrincipal principal, Guid courseId, Guid requiredId, CancellationToken ct)
        => retry.ExecuteAsync<RecordsService>(service => service.RemovePrerequisiteCore(principal, courseId, requiredId, ct), ct);

    private async Task RemovePrerequisiteCore(ClaimsPrincipal principal, Guid courseId, Guid requiredId, CancellationToken ct)
    {
        var course = await db.Courses.AsNoTracking().SingleOrDefaultAsync(x => x.Id == courseId, ct)
            ?? throw ApiFault.NotFound();
        await Admin(principal, course.InstitutionId, ct);
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await db.Institutions.FromSqlInterpolated($"SELECT * FROM institutions WHERE \"Id\" = {course.InstitutionId} FOR UPDATE")
                .ToListAsync(ct);
            if (await db.CourseSections.AnyAsync(x => x.CourseId == courseId && x.Status == "Open", ct))
                throw ApiFault.Conflict("Close all open sections before editing prerequisites.");
            var item = await db.CoursePrerequisites.SingleOrDefaultAsync(x => x.CourseId == courseId &&
                x.RequiredCourseId == requiredId && x.InstitutionId == course.InstitutionId, ct)
                ?? throw ApiFault.NotFound();
            db.CoursePrerequisites.Remove(item);
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        });
    }

    // IMPORTANT: Invoked by both individual enrollment and bulk governance preview/commit.
    public async Task<EnrollmentEligibility> EligibilityForSection(Guid sectionId, Guid studentId, CancellationToken ct, bool recordDecision = false)
    {
        var section = await db.CourseSections.AsNoTracking().SingleOrDefaultAsync(x => x.Id == sectionId, ct)
            ?? throw ApiFault.NotFound();
        await AssessmentFreshness.Ensure(db,section.InstitutionId,studentId,ct);
        var required = await db.CoursePrerequisites.AsNoTracking().Where(x => x.InstitutionId == section.InstitutionId &&
            x.CourseId == section.CourseId).OrderBy(x => x.RequiredCourseId).ToListAsync(ct);

        var history = await (from g in db.GradebookResults.AsNoTracking()
            join s in db.CourseSections.AsNoTracking() on g.SectionId equals s.Id
            join policy in db.SectionGradePolicies.AsNoTracking()
                on new { g.SectionId, g.InstitutionId } equals new { policy.SectionId, policy.InstitutionId }
            where g.InstitutionId == section.InstitutionId && g.StudentId == studentId &&
                policy.PublishedAtUtc != null
            select new { s.CourseId, g.Percentage, g.Passed }).ToListAsync(ct);
        var membership = await db.StudentProgramMemberships.AsNoTracking()
            .SingleOrDefaultAsync(x => x.StudentId == studentId && x.InstitutionId == section.InstitutionId && x.Status == "Active", ct);
        var policyEngine = new Academix.Api.Services.Registrar.AcademicPolicyEngine(db);
        var activePolicy = await policyEngine.Active(section.InstitutionId, studentId, ct);
        var equivalent = !activePolicy.Rules.AllowEquivalency || membership is null ? new HashSet<Guid>() : (await (
            from line in db.TransferCreditLines.AsNoTracking()
            join petition in db.TransferCreditPetitions.AsNoTracking() on line.PetitionId equals petition.Id
            where petition.InstitutionId == section.InstitutionId && line.InstitutionId == section.InstitutionId &&
                petition.StudentId == studentId && petition.ProgramId == membership.ProgramId && petition.Status == "Approved"
            select line.TargetCourseId).ToListAsync(ct)).ToHashSet();
        if(activePolicy.Rules.AllowEquivalency)
        {
            var plan=await db.StudentCurriculumAssignments.AsNoTracking().Where(x=>x.StudentId==studentId&&x.InstitutionId==section.InstitutionId).Select(x=>(Guid?)x.PlanId).SingleOrDefaultAsync(ct);
            if(plan is Guid assignedPlan)
                equivalent.UnionWith((await Academix.Api.Services.Curriculum.RecognitionQualification.Load(
                    db,section.InstitutionId,studentId,assignedPlan,ct)).Select(x=>x.Credit.TargetCourseId));
        }
        var checks = required.Select(p =>
        {
            var grades = history.Where(x => x.CourseId == p.RequiredCourseId)
                .Select(x => (x.Percentage, x.Passed));
            var best = history.Where(x => x.CourseId == p.RequiredCourseId && x.Passed)
                .Select(x => (decimal?)x.Percentage).Max();
            return new PrerequisiteCheck(p.RequiredCourseId, p.MinimumPercentage, best,
                equivalent.Contains(p.RequiredCourseId) || RecordsPolicy.Satisfies(p.MinimumPercentage, grades));
        }).ToArray();
        var decision = await policyEngine.Evaluate(section.InstitutionId, studentId, sectionId, checks.All(x => x.Satisfied), ct);
        if(recordDecision)db.EligibilityDecisions.Add(new(){InstitutionId=section.InstitutionId,StudentId=studentId,SectionId=sectionId,PolicyId=decision.PolicyId,Eligible=decision.Eligible,
            SnapshotJson=System.Text.Json.JsonSerializer.Serialize(new{decision,Requirements=checks,Source="Registration"})});
        return new EnrollmentEligibility(sectionId, studentId, decision.Eligible, checks,decision.Reasons,decision.PolicyId);
    }

    public async Task<EnrollmentEligibility> Eligibility(ClaimsPrincipal principal, Guid sectionId,
        Guid studentId, CancellationToken ct)
    {
        var section = await db.CourseSections.AsNoTracking().SingleOrDefaultAsync(x => x.Id == sectionId, ct)
            ?? throw ApiFault.NotFound();
        await StudentInstitution(principal, studentId, ct);
        if ((await users.FindByIdAsync(studentId.ToString()))?.InstitutionId != section.InstitutionId)
            throw ApiFault.NotFound();
        return await EligibilityForSection(sectionId, studentId, ct);
    }

    // Shared legacy/modern withdrawal path: old route retains Withdraw semantics and reason.
    public Task<RegistrationView> ChangeRegistration(ClaimsPrincipal principal, Guid sectionId,
        Guid studentId, RegistrationChange request, CancellationToken ct)
        => retry.ExecuteAsync<RecordsService, RegistrationView>(service => service.ChangeRegistrationCore(principal, sectionId, studentId, request, ct), ct);

    private async Task<RegistrationView> ChangeRegistrationCore(ClaimsPrincipal principal, Guid sectionId,
        Guid studentId, RegistrationChange request, CancellationToken ct)
    {
        var section = await db.CourseSections.AsNoTracking().SingleOrDefaultAsync(x => x.Id == sectionId, ct)
            ?? throw ApiFault.NotFound();
        var inst = await StudentInstitution(principal, studentId, ct);
        if (inst != section.InstitutionId) throw ApiFault.NotFound();
        var actor = (await security.CurrentAsync(principal, ct)).User;
        var reason = RecordsPolicy.Reason(request.Reason);
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var lockedSection = (await db.CourseSections.FromSqlInterpolated(
                $"SELECT * FROM course_sections WHERE \"Id\" = {sectionId} FOR UPDATE")
                .ToListAsync(ct)).SingleOrDefault() ?? throw ApiFault.NotFound();
            var term = await db.AcademicTerms.AsNoTracking().SingleOrDefaultAsync(x => x.Id == lockedSection.TermId, ct)
                ?? throw ApiFault.NotFound();
            var published = await db.SectionGradePolicies.AnyAsync(x => x.SectionId == sectionId && x.PublishedAtUtc != null, ct);
            var status = RecordsPolicy.Transition(term.StartsOn, term.EndsOn,
                DateOnly.FromDateTime(DateTime.UtcNow), request.Action, published);
            var enrollment = await db.StudentEnrollments.SingleOrDefaultAsync(x => x.SectionId == sectionId &&
                x.StudentId == studentId && x.InstitutionId == inst, ct) ?? throw ApiFault.NotFound();
            if (enrollment.Status != "Active") throw ApiFault.Conflict("Student is not actively enrolled.");
            enrollment.Status = status;
            Event(inst, studentId, actor.Id, "enrollment." + status.ToLowerInvariant(), reason);
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
            return new RegistrationView(sectionId, studentId, status);
        });
    }

    public Task<TranscriptSyncResult> SyncTranscript(ClaimsPrincipal principal, Guid sectionId, CancellationToken ct)
        => retry.ExecuteAsync<RecordsService, TranscriptSyncResult>(service => service.SyncTranscriptCore(principal, sectionId, ct), ct);

    private async Task<TranscriptSyncResult> SyncTranscriptCore(ClaimsPrincipal principal, Guid sectionId, CancellationToken ct)
    {
        await using var tx=await db.Database.BeginTransactionAsync(ct);
        var result=await SyncTranscriptWithinTransaction(principal,sectionId,ct);await tx.CommitAsync(ct);return result;
    }
    internal async Task<TranscriptSyncResult> SyncTranscriptWithinTransaction(ClaimsPrincipal principal, Guid sectionId, CancellationToken ct)
    {
        if(db.Database.CurrentTransaction is null)throw new InvalidOperationException("Academic transaction required.");

        var section = await db.CourseSections.AsNoTracking().SingleOrDefaultAsync(x => x.Id == sectionId, ct)
            ?? throw ApiFault.NotFound();
        await Academix.Api.Services.Academic.RegistrationCoordination.Lock(db, section.InstitutionId, ct);
        var actor = await Admin(principal, section.InstitutionId, ct);
        await db.CourseSections.FromSqlInterpolated(
            $"SELECT * FROM course_sections WHERE \"Id\" = {sectionId} FOR UPDATE").ToListAsync(ct);
        // Gradebook correction locks this policy row, so snapshot sync and corrections serialize.
        await db.SectionGradePolicies.FromSqlInterpolated(
            $"SELECT * FROM gradebook_policies WHERE \"SectionId\" = {sectionId} FOR UPDATE")
            .ToListAsync(ct);
        await AssessmentFreshness.Ensure(db,section.InstitutionId,null,ct,sectionId);
        var course = await db.Courses.AsNoTracking().SingleAsync(x => x.Id == section.CourseId, ct);
        var grades = await db.GradebookResults.AsNoTracking().Where(x => x.SectionId == sectionId &&
            x.InstitutionId == section.InstitutionId).ToListAsync(ct);
        if (grades.Count == 0 || !await db.SectionGradePolicies.AnyAsync(x => x.SectionId == sectionId &&
            x.PublishedAtUtc != null, ct)) throw ApiFault.Conflict("No published section results.");
        var current = await db.StudentTranscriptEntries.Where(x => x.SectionId == sectionId).ToListAsync(ct);
        var index = current.ToDictionary(x => x.StudentId);
        int inserted = 0, updated = 0, unchanged = 0;
        foreach (var g in grades)
        {
            if (!index.TryGetValue(g.StudentId, out var x))
            {
                db.StudentTranscriptEntries.Add(new StudentTranscriptEntry { InstitutionId = section.InstitutionId,
                    SectionId = sectionId, CourseId = course.Id, TermId = section.TermId, StudentId = g.StudentId,
                    CourseCode = course.Code, CourseTitle = course.Title, Percentage = g.Percentage,
                    Passed = g.Passed, Letter = g.Letter, GradebookVersion = g.Version });
                Event(section.InstitutionId, g.StudentId, actor.Id, "transcript.created", sectionId.ToString());
                inserted++;
            }
            else if (x.GradebookVersion != g.Version || x.Percentage != g.Percentage ||
                x.Passed != g.Passed || x.Letter != g.Letter)
            {
                Event(section.InstitutionId, g.StudentId, actor.Id, "transcript.corrected",
                    $"Section {sectionId}: {x.Percentage} -> {g.Percentage}");
                x.Percentage = g.Percentage; x.Passed = g.Passed; x.Letter = g.Letter;
                x.GradebookVersion = g.Version; x.Version++; x.SyncedAtUtc = DateTime.UtcNow;
                updated++;
            }
            else unchanged++;
        }
        await db.SaveChangesAsync(ct);
        return new TranscriptSyncResult(sectionId, inserted, updated, unchanged);
    }

    public Task<TranscriptView> Transcript(ClaimsPrincipal principal, Guid studentId, CancellationToken ct)
        => retry.ExecuteAsync<RecordsService,TranscriptView>(s=>s.TranscriptCore(principal,studentId,ct),ct);

    private async Task<TranscriptView> TranscriptCore(ClaimsPrincipal principal, Guid studentId, CancellationToken ct)
    {
        await using var tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead,ct);
        var inst = await StudentInstitution(principal, studentId, ct);
        await AcademicFreshness.Ensure(db,inst,studentId,ct);
        var items = await db.StudentTranscriptEntries.AsNoTracking().Where(x => x.InstitutionId == inst &&
            x.StudentId == studentId).OrderBy(x => x.TermId).ThenBy(x => x.CourseCode).ToListAsync(ct);
        var result = new TranscriptView(studentId, items.Select(x => new TranscriptLine(x.SectionId, x.CourseId, x.TermId,
            x.CourseCode, x.CourseTitle, x.Percentage, x.Letter, x.Passed, x.Version)).ToArray(),
            items.Count == 0 ? null : decimal.Round(items.Average(x => x.Percentage), 2),
            items.Count, items.Count(x => x.Passed));
        await tx.CommitAsync(ct); return result;
    }

    public Task<StandingView> RecalculateStanding(ClaimsPrincipal principal, Guid termId,
        Guid studentId, CancellationToken ct)
        => retry.ExecuteAsync<RecordsService, StandingView>(service => service.RecalculateStandingCore(principal, termId, studentId, ct), ct);

    private async Task<StandingView> RecalculateStandingCore(ClaimsPrincipal principal, Guid termId,
        Guid studentId, CancellationToken ct)
    {
        var inst = await StudentInstitution(principal, studentId, ct);
        var actor = await Admin(principal, inst, ct);
        if (!await db.AcademicTerms.AnyAsync(x => x.Id == termId && x.InstitutionId == inst, ct))
            throw ApiFault.NotFound();
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await Academix.Api.Services.Academic.RegistrationCoordination.Lock(db, inst, ct);
            await AcademicFreshness.Ensure(db, inst, studentId, ct);
            await db.Users.FromSqlInterpolated($"SELECT * FROM identity_users WHERE \"Id\" = {studentId} FOR UPDATE")
                .ToListAsync(ct);
            actor = await Admin(principal, inst, ct);
            var grades = await db.StudentTranscriptEntries.AsNoTracking().Where(x => x.StudentId == studentId &&
                x.InstitutionId == inst && x.TermId == termId).Select(x => new { x.Percentage, x.Passed }).ToListAsync(ct);
            var policyData = grades.Select(x => (x.Percentage, x.Passed)).ToArray();
            var entry = await db.StudentTermStandings.SingleOrDefaultAsync(x => x.StudentId == studentId &&
                x.InstitutionId == inst && x.TermId == termId, ct);
            var status = RecordsPolicy.Standing(policyData);
            if (entry is null)
            {
                entry = new StudentTermStanding { InstitutionId = inst, StudentId = studentId, TermId = termId };
                db.StudentTermStandings.Add(entry);
            }
            else entry.Version++;
            entry.Status = status; entry.CompletedCourses = grades.Count;
            entry.PassedCourses = grades.Count(x => x.Passed);
            entry.AveragePercentage = grades.Count == 0 ? null : decimal.Round(grades.Average(x => x.Percentage), 2);
            entry.CalculatedAtUtc = DateTime.UtcNow;
            Event(inst, studentId, actor.Id, "standing.recalculated", status);
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
            return Map(entry);
        });
    }

    public Task<StandingView> Standing(ClaimsPrincipal principal, Guid termId, Guid studentId, CancellationToken ct)
        => retry.ExecuteAsync<RecordsService,StandingView>(s=>s.StandingCore(principal,termId,studentId,ct),ct);

    private async Task<StandingView> StandingCore(ClaimsPrincipal principal, Guid termId, Guid studentId, CancellationToken ct)
    {
        await using var tx=await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead,ct);
        var inst = await StudentInstitution(principal, studentId, ct);
        await AcademicFreshness.Ensure(db,inst,studentId,ct);
        var item = await db.StudentTermStandings.AsNoTracking().SingleOrDefaultAsync(x => x.InstitutionId == inst &&
            x.StudentId == studentId && x.TermId == termId, ct) ?? throw ApiFault.NotFound();
        if(await AcademicFreshness.StandingStale(db,inst,studentId,termId,item.CalculatedAtUtc,ct))
            throw ApiFault.Conflict("Academic standing is stale. Recalculate it from the synchronized transcript before continuing.");
        var result=Map(item); await tx.CommitAsync(ct); return result;
    }

    public async Task<IReadOnlyList<LifecycleEventView>> Events(ClaimsPrincipal principal, Guid inst, int page, CancellationToken ct)
    {
        await Admin(principal, inst, ct);
        if (page < 1 || page > 100000) throw ApiFault.Bad("Invalid page.");
        return await db.StudentLifecycleEvents.AsNoTracking().Where(x => x.InstitutionId == inst)
            .OrderByDescending(x => x.AtUtc).ThenBy(x => x.Id).Skip((page - 1) * 50).Take(50)
            .Select(x => new LifecycleEventView(x.Id, x.StudentId, x.ActorId, x.Kind, x.Detail, x.AtUtc)).ToListAsync(ct);
    }
}
