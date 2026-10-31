using Academix.Api.Services.Infrastructure;
using System.Security.Claims;
using Academix.Api.Data;
using Academix.Api.Domain.Academic;
using Academix.Api.Domain.Courses;
using Academix.Api.Domain.Curriculum;
using Academix.Api.Domain.Records;
using Academix.Api.DTOs.Curriculum;
using Academix.Api.Middleware;
using Academix.Api.Services.Identity;
using Microsoft.EntityFrameworkCore;

namespace Academix.Api.Services.Curriculum;

public sealed partial class CurriculumService(AcademixDbContext db, IdentitySecurity security, RetryingServiceWork retry, DegreeAuditService degreeAudit)
{
    private async Task<CurriculumPlan> Plan(Guid planId, CancellationToken ct)
        => await db.CurriculumPlans.AsNoTracking().SingleOrDefaultAsync(x => x.Id == planId, ct) ?? throw ApiFault.NotFound();

    private async Task<CurriculumPlanView> Map(CurriculumPlan plan, CancellationToken ct)
    {
        var rows = await db.CurriculumCourses.AsNoTracking().Where(x => x.PlanId == plan.Id)
            .Join(db.Courses.AsNoTracking(), x => x.CourseId, c => c.Id, (x, c) => new { Course = x, c.Code, c.Title })
            .OrderBy(x => x.Course.RecommendedTerm).ThenBy(x => x.Code)
            .Select(x => new CurriculumCourseView(x.Course.CourseId, x.Code, x.Title,
                x.Course.RequirementKind, x.Course.CreditHours, x.Course.RecommendedTerm)).ToListAsync(ct);
        var bands = await db.CurriculumGradeBands.AsNoTracking().Where(x => x.PlanId == plan.Id)
            .OrderBy(x => x.MinimumPercentage).Select(x => new CurriculumGradeBandView(
                x.MinimumPercentage, x.GradePoints, x.Letter)).ToListAsync(ct);
        return new CurriculumPlanView(plan.Id, plan.ProgramId, plan.CatalogYear, plan.Revision, plan.Name,
            plan.Status, plan.Version, plan.MinimumTotalCredits, plan.MinimumElectiveCredits,
            plan.MinimumCumulativeGpa, plan.PublishedAtUtc, rows, bands);
    }

    public async Task<CurriculumPlanView> Create(ClaimsPrincipal principal, Guid programId,
        CreateCurriculumPlan request, CancellationToken ct)
    {
        var program = await db.AcademicPrograms.AsNoTracking().SingleOrDefaultAsync(x => x.Id == programId, ct)
            ?? throw ApiFault.NotFound();
        await security.RequireAdminAsync(principal, program.InstitutionId, ct);
        if (!program.IsActive) throw ApiFault.Conflict("Program is inactive.");
        CurriculumPolicy.ValidatePlan(request);
        if (await db.CurriculumPlans.AnyAsync(x => x.InstitutionId == program.InstitutionId &&
            x.ProgramId == programId && x.CatalogYear == request.CatalogYear && x.Revision == request.Revision, ct))
            throw ApiFault.Conflict("Catalog revision already exists.");
        var p = new CurriculumPlan { InstitutionId = program.InstitutionId, ProgramId = programId,
            CatalogYear = request.CatalogYear, Revision = request.Revision, Name = request.Name.Trim(),
            MinimumTotalCredits = request.MinimumTotalCredits, MinimumElectiveCredits = request.MinimumElectiveCredits,
            MinimumCumulativeGpa = request.MinimumCumulativeGpa };
        db.CurriculumPlans.Add(p);
        await db.SaveChangesAsync(ct);
        return await Map(p, ct);
    }

    public async Task<IReadOnlyList<CurriculumPlanView>> List(ClaimsPrincipal principal, Guid programId,
        CancellationToken ct)
    {
        var program = await db.AcademicPrograms.AsNoTracking().SingleOrDefaultAsync(x => x.Id == programId, ct)
            ?? throw ApiFault.NotFound();
        await security.RequireAdminAsync(principal, program.InstitutionId, ct);
        var plans = await db.CurriculumPlans.AsNoTracking().Where(x => x.ProgramId == programId)
            .OrderByDescending(x => x.CatalogYear).ThenByDescending(x => x.Revision).ToListAsync(ct);
        var result = new List<CurriculumPlanView>();
        foreach (var p in plans) result.Add(await Map(p, ct));
        return result;
    }

    public async Task<CurriculumPlanView> Get(ClaimsPrincipal principal, Guid planId, CancellationToken ct)
    {
        var plan = await Plan(planId, ct);
        await security.RequireAdminAsync(principal, plan.InstitutionId, ct);
        return await Map(plan, ct);
    }

    public Task<CurriculumPlanView> ReplaceCourses(ClaimsPrincipal principal, Guid planId,
        ReplaceCurriculumCourses request, CancellationToken ct)
        => retry.ExecuteAsync<CurriculumService, CurriculumPlanView>(service => service.ReplaceCoursesCore(principal, planId, request, ct), ct);

    private async Task<CurriculumPlanView> ReplaceCoursesCore(ClaimsPrincipal principal, Guid planId,
        ReplaceCurriculumCourses request, CancellationToken ct)
    {
        CurriculumPolicy.ValidateCourses(request.Courses);
        var initial = await Plan(planId, ct);
        await security.RequireAdminAsync(principal, initial.InstitutionId, ct);
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var p = (await db.CurriculumPlans.FromSqlInterpolated(
                $"SELECT * FROM curriculum_plans WHERE \"Id\" = {planId} FOR UPDATE").ToListAsync(ct)).SingleOrDefault()
                ?? throw ApiFault.NotFound();
            await security.RequireAdminAsync(principal, p.InstitutionId, ct);
            if (p.Status != "Draft" || p.Version != request.ExpectedVersion)
                throw ApiFault.Conflict("Curriculum is published or has a stale version.");
            var courseIds = request.Courses.Select(x => x.CourseId).ToArray();
            var valid = await db.ProgramCourses.AsNoTracking().Join(db.Courses.AsNoTracking(), pc => pc.CourseId,
                c => c.Id, (pc, c) => new { pc, c })
                .Where(x => x.pc.ProgramId == p.ProgramId && x.pc.InstitutionId == p.InstitutionId &&
                    courseIds.Contains(x.pc.CourseId) && x.c.InstitutionId == p.InstitutionId && x.c.IsActive)
                .Select(x => x.c.Id).Distinct().CountAsync(ct);
            if (valid != courseIds.Length) throw ApiFault.Bad("Every curriculum course must belong to this program and institution.");
            var old = await db.CurriculumCourses.Where(x => x.PlanId == p.Id).ToListAsync(ct);
            // Group membership references these rows. Replacing a draft course set
            // clears its groups; published curricula remain immutable.
            await db.Set<Academix.Api.Domain.Advising.CurriculumElectiveGroupCourse>().Where(x=>x.PlanId==p.Id).ExecuteDeleteAsync(ct);
            await db.Set<Academix.Api.Domain.Advising.CurriculumElectiveGroup>().Where(x=>x.PlanId==p.Id).ExecuteDeleteAsync(ct);
            db.CurriculumCourses.RemoveRange(old);
            await db.SaveChangesAsync(ct); // prevent transient uniqueness conflicts on replacement
            db.CurriculumCourses.AddRange(request.Courses.Select(x => new CurriculumCourse {
                PlanId = p.Id, InstitutionId = p.InstitutionId, CourseId = x.CourseId,
                RequirementKind = x.RequirementKind, CreditHours = x.CreditHours,
                RecommendedTerm = x.RecommendedTerm }));
            p.Version++;
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return await Map(p, ct);
        });
    }

    public Task<CurriculumPlanView> ReplaceScale(ClaimsPrincipal principal, Guid planId,
        ReplaceCurriculumScale request, CancellationToken ct)
        => retry.ExecuteAsync<CurriculumService, CurriculumPlanView>(service => service.ReplaceScaleCore(principal, planId, request, ct), ct);

    private async Task<CurriculumPlanView> ReplaceScaleCore(ClaimsPrincipal principal, Guid planId,
        ReplaceCurriculumScale request, CancellationToken ct)
    {
        CurriculumPolicy.ValidateScale(request.Bands);
        var initial = await Plan(planId, ct);
        await security.RequireAdminAsync(principal, initial.InstitutionId, ct);
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var p = (await db.CurriculumPlans.FromSqlInterpolated(
                $"SELECT * FROM curriculum_plans WHERE \"Id\" = {planId} FOR UPDATE").ToListAsync(ct)).SingleOrDefault()
                ?? throw ApiFault.NotFound();
            await security.RequireAdminAsync(principal, p.InstitutionId, ct);
            if (p.Status != "Draft" || p.Version != request.ExpectedVersion)
                throw ApiFault.Conflict("Curriculum is published or has a stale version.");
            var old = await db.CurriculumGradeBands.Where(x => x.PlanId == p.Id).ToListAsync(ct);
            db.CurriculumGradeBands.RemoveRange(old);
            await db.SaveChangesAsync(ct); // prevent transient uniqueness conflicts on replacement
            db.CurriculumGradeBands.AddRange(request.Bands.Select(b => new CurriculumGradeBand {
                PlanId = p.Id, InstitutionId = p.InstitutionId, MinimumPercentage = b.MinimumPercentage,
                GradePoints = b.GradePoints, Letter = b.Letter.Trim() }));
            p.Version++;
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return await Map(p, ct);
        });
    }

    public Task<CurriculumPlanView> Publish(ClaimsPrincipal principal, Guid planId,
        PublishCurriculumPlan request, CancellationToken ct)
        => retry.ExecuteAsync<CurriculumService, CurriculumPlanView>(service => service.PublishCore(principal, planId, request, ct), ct);

    private async Task<CurriculumPlanView> PublishCore(ClaimsPrincipal principal, Guid planId,
        PublishCurriculumPlan request, CancellationToken ct)
    {
        var initial = await Plan(planId, ct);
        await security.RequireAdminAsync(principal, initial.InstitutionId, ct);
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var p = (await db.CurriculumPlans.FromSqlInterpolated(
                $"SELECT * FROM curriculum_plans WHERE \"Id\" = {planId} FOR UPDATE").ToListAsync(ct)).SingleOrDefault()
                ?? throw ApiFault.NotFound();
            await security.RequireAdminAsync(principal, p.InstitutionId, ct);
            if (p.Status != "Draft" || p.Version != request.ExpectedVersion)
                throw ApiFault.Conflict("Curriculum is published or has a stale version.");
            var courses = await db.CurriculumCourses.AsNoTracking().Where(x => x.PlanId == p.Id)
                .Select(x => new CurriculumCourseInput(x.CourseId, x.RequirementKind, x.CreditHours, x.RecommendedTerm))
                .ToListAsync(ct);
            var bands = await db.CurriculumGradeBands.AsNoTracking().Where(x => x.PlanId == p.Id)
                .Select(x => new CurriculumBandInput(x.MinimumPercentage, x.GradePoints, x.Letter)).ToListAsync(ct);
            CurriculumPolicy.ValidateCourses(courses); CurriculumPolicy.ValidateScale(bands);
            if (!courses.Any(x => x.RequirementKind == "Required") ||
                courses.Sum(x => x.CreditHours) < p.MinimumTotalCredits ||
                courses.Where(x => x.RequirementKind == "Elective").Sum(x => x.CreditHours) < p.MinimumElectiveCredits)
                throw ApiFault.Bad("Curriculum requirements cannot satisfy the declared graduation credits.");
            p.Status = "Published"; p.PublishedAtUtc = DateTime.UtcNow; p.Version++;
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
            return await Map(p, ct);
        });
    }

    public Task<StudentCurriculumView> Assign(ClaimsPrincipal principal, Guid studentId,
        AssignStudentCurriculum request, CancellationToken ct)
        => retry.ExecuteAsync<CurriculumService, StudentCurriculumView>(service => service.AssignCore(principal, studentId, request, ct), ct);

    private async Task<StudentCurriculumView> AssignCore(ClaimsPrincipal principal, Guid studentId, AssignStudentCurriculum request, CancellationToken ct)
    {
        await using var tx=await db.Database.BeginTransactionAsync(ct);
        var result=await AssignWithinTransaction(principal, studentId, request, ct);
        await tx.CommitAsync(ct);return result;
    }
    internal async Task<StudentCurriculumView> AssignWithinTransaction(ClaimsPrincipal principal, Guid studentId, AssignStudentCurriculum request, CancellationToken ct)
    {
        if(db.Database.CurrentTransaction is null)throw new InvalidOperationException("Academic transaction required.");

        var plan = await Plan(request.PlanId, ct);
        var actor = await security.RequireAdminAsync(principal, plan.InstitutionId, ct);
        if (plan.Status != "Published") throw ApiFault.Conflict("Only published curricula may be assigned.");
        var user = (await db.Users.FromSqlInterpolated(
            $"SELECT * FROM identity_users WHERE \"Id\" = {studentId} FOR UPDATE").ToListAsync(ct)).SingleOrDefault()
            ?? throw ApiFault.NotFound();
        actor = await security.RequireAdminAsync(principal, plan.InstitutionId, ct);
        if (!user.IsActive || user.InstitutionId != plan.InstitutionId)
            throw ApiFault.Bad("Student does not belong to this institution.");
        var membership = await db.StudentProgramMemberships.AsNoTracking().SingleOrDefaultAsync(x =>
            x.StudentId == studentId && x.InstitutionId == plan.InstitutionId &&
            x.ProgramId == plan.ProgramId && x.Status == "Active", ct);
        if (membership is null) throw ApiFault.Conflict("Active membership in the matching academic program is required.");
        if (await db.StudentCurriculumAssignments.AnyAsync(x => x.StudentId == studentId &&
            x.InstitutionId == plan.InstitutionId, ct))
            throw ApiFault.Conflict("A curriculum is already assigned. Explicit reassignment is not supported.");
        var item = new StudentCurriculumAssignment { InstitutionId = plan.InstitutionId,
            StudentId = studentId, PlanId = plan.Id, AssignedById = actor.Id };
        db.StudentCurriculumAssignments.Add(item);
        security.Audit(actor.Id, plan.InstitutionId, "curriculum.assigned", studentId.ToString());
        await db.SaveChangesAsync(ct);
        return new StudentCurriculumView(studentId, plan.Id, plan.ProgramId, plan.CatalogYear,
            plan.Name, item.AssignedAtUtc);
    }

    private async Task<(StudentCurriculumAssignment Assignment, CurriculumPlan Plan)> Assigned(
        ClaimsPrincipal principal, Guid studentId, CancellationToken ct)
    {
        var assignment = await db.StudentCurriculumAssignments.AsNoTracking().SingleOrDefaultAsync(x => x.StudentId == studentId, ct)
            ?? throw ApiFault.NotFound();
        await security.RequireStudentAsync(principal, studentId, assignment.InstitutionId, ct);
        var plan = await db.CurriculumPlans.AsNoTracking().SingleAsync(x => x.Id == assignment.PlanId &&
            x.InstitutionId == assignment.InstitutionId, ct);
        return (assignment, plan);
    }

    public async Task<StudentCurriculumView> StudentPlan(ClaimsPrincipal principal, Guid studentId, CancellationToken ct)
    {
        var (a, p) = await Assigned(principal, studentId, ct);
        return new StudentCurriculumView(studentId, p.Id, p.ProgramId, p.CatalogYear, p.Name, a.AssignedAtUtc);
    }

    public async Task<CurriculumPlanView> StudentPlanDetails(ClaimsPrincipal principal,
        Guid studentId, CancellationToken ct)
    {
        var (_, plan) = await Assigned(principal, studentId, ct);
        return await Map(plan, ct);
    }

    private async Task<(CurriculumPlan Plan, List<CurriculumCourseView> Courses,
        List<CurriculumBandInput> Bands, List<TranscriptAttempt> Attempts)> StudentData(
        ClaimsPrincipal principal, Guid studentId, CancellationToken ct)
    {
        var (assignment, plan) = await Assigned(principal, studentId, ct);
        await AcademicFreshness.Ensure(db, assignment.InstitutionId, studentId, ct);
        var courses = await db.CurriculumCourses.AsNoTracking().Where(x => x.PlanId == plan.Id)
            .Join(db.Courses.AsNoTracking(), x => x.CourseId, c => c.Id, (x, c) => new CurriculumCourseView(
                x.CourseId, c.Code, c.Title, x.RequirementKind, x.CreditHours, x.RecommendedTerm)).ToListAsync(ct);
        var bands = await db.CurriculumGradeBands.AsNoTracking().Where(x => x.PlanId == plan.Id)
            .Select(x => new CurriculumBandInput(x.MinimumPercentage, x.GradePoints, x.Letter)).ToListAsync(ct);
        var attempts = await db.StudentTranscriptEntries.AsNoTracking().Where(x =>
            x.StudentId == studentId && x.InstitutionId == assignment.InstitutionId)
            .Select(x => new TranscriptAttempt(x.CourseId, x.TermId, x.CourseCode, x.Percentage, x.Passed))
            .ToListAsync(ct);
        return (plan, courses, bands, attempts);
    }

    public async Task<StudentCurriculumAudit> Audit(ClaimsPrincipal principal, Guid studentId, CancellationToken ct)
        => (await degreeAudit.Evaluate(principal, studentId, ct)).Audit;
    public async Task<StudentGpaReport> Gpa(ClaimsPrincipal principal, Guid studentId, CancellationToken ct)
    {
        var data = await StudentData(principal, studentId, ct);
        var map = data.Courses.ToDictionary(x => x.CourseId);
        // This curriculum uses BestAttempt for cumulative GPA, preventing duplicate credit from retakes.
        var relevant = data.Attempts.Where(x => map.ContainsKey(x.CourseId)).ToArray();
        GpaCourseLine Line(TranscriptAttempt x)
        {
            var band = CurriculumPolicy.Band(x.Percentage, data.Bands);
            var c = map[x.CourseId];
            return new GpaCourseLine(x.CourseId, x.TermId, x.CourseCode, c.CreditHours, x.Percentage,
                x.Passed, band.Letter, band.GradePoints);
        }
        var rows = CurriculumPolicy.BestAttempts(relevant).Select(Line).ToList();
        // Term GPA reflects the grade recorded in each term; cumulative GPA applies BestAttempt across terms.
        var terms = relevant.GroupBy(x => x.TermId).Select(g =>
        {
            var lines = CurriculumPolicy.BestAttempts(g).Select(Line).OrderBy(x => x.CourseCode).ToList();
            return new GpaTermView(g.Key,
                CurriculumPolicy.WeightedGpa(lines.Select(x => (x.CreditHours, x.GradePoints))),
                lines.Sum(x => x.CreditHours), lines);
        }).OrderBy(x => x.TermId).ToList();
        return new StudentGpaReport(studentId,
            CurriculumPolicy.WeightedGpa(rows.Select(x => (x.CreditHours, x.GradePoints))),
            rows.Sum(x => x.CreditHours), rows.Where(x => x.Passed).Sum(x => x.CreditHours), terms);
    }
}

