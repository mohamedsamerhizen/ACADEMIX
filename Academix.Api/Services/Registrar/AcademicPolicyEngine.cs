using System.Text.Json;
using Academix.Api.Data;
using Academix.Api.DTOs.Registrar;
using Academix.Api.Middleware;
using Microsoft.EntityFrameworkCore;

namespace Academix.Api.Services.Registrar;

public static class AcademicPolicyRulesValidator
{
    public static void Validate(PolicyDraft? draft)
    {
        if (draft?.Rules is null || draft.EffectiveFromUtc.Kind != DateTimeKind.Utc || draft.EffectiveUntilUtc.Kind != DateTimeKind.Utc ||
            draft.EffectiveFromUtc >= draft.EffectiveUntilUtc || draft.Rules.MaximumTermCredits is <= 0 or > 100 ||
            draft.Rules.MaximumTermCredits is decimal credits && decimal.Round(credits, 2) != credits)
            throw ApiFault.Bad("Valid UTC effective interval and credit limit (0-100] required.");
    }

    public static IReadOnlyList<string> Evaluate(AcademicPolicyRules rules, bool prerequisites, bool hasHold,
        decimal current, decimal requested, IReadOnlySet<string> exceptions)
    {
        var reasons = new List<string>();
        if (hasHold) reasons.Add("ActiveAcademicHold"); // Holds cannot be bypassed by an exception.
        if (rules.EnforcePrerequisites && !prerequisites && !exceptions.Contains("Prerequisites")) reasons.Add("Prerequisites");
        if (rules.MaximumTermCredits is decimal maximum && current + requested > maximum && !exceptions.Contains("CreditLoad")) reasons.Add("CreditLoad");
        if (rules.RequireEnrollmentApproval && !exceptions.Contains("EnrollmentApproval")) reasons.Add("EnrollmentApproval");
        return reasons;
    }
}

public sealed class AcademicPolicyEngine(AcademixDbContext db)
{
    public async Task<(Guid? Id, AcademicPolicyRules Rules)> Active(Guid institutionId, Guid studentId, CancellationToken ct)
    {
        var program = await db.StudentProgramMemberships.AsNoTracking().Where(x => x.InstitutionId == institutionId && x.StudentId == studentId && (x.Status == "Active" || x.Status == "Completed"))
            .Select(x => (Guid?)x.ProgramId).SingleOrDefaultAsync(ct);
        var now = DateTime.UtcNow;
        var candidates = await db.AcademicPolicyRevisions.AsNoTracking().Where(x => x.InstitutionId == institutionId && x.Status == "Published" &&
            x.EffectiveFromUtc <= now && x.EffectiveUntilUtc > now && (x.ProgramId == null || x.ProgramId == program)).ToListAsync(ct);
        var selected = candidates.OrderByDescending(x => x.ProgramId.HasValue).FirstOrDefault();
        return selected is null ? (null, new(true, true, null, false)) :
            (selected.Id, JsonSerializer.Deserialize<AcademicPolicyRules>(selected.RulesJson) ?? throw new InvalidOperationException("Invalid stored policy."));
    }

    public async Task<PolicyDecision> Evaluate(Guid institutionId, Guid studentId, Guid sectionId, bool prerequisites, CancellationToken ct)
    {
        var section = await db.CourseSections.AsNoTracking().SingleOrDefaultAsync(x => x.Id == sectionId && x.InstitutionId == institutionId, ct) ?? throw ApiFault.NotFound();
        var (id, rules) = await Active(institutionId, studentId, ct);
        var now = DateTime.UtcNow;
        var hasHold = await db.StudentAcademicHolds.AsNoTracking().AnyAsync(x => x.InstitutionId == institutionId && x.StudentId == studentId && x.ReleasedAtUtc == null, ct);
        var exceptions = (await db.ApprovedAcademicExceptions.AsNoTracking().Where(x => x.InstitutionId == institutionId && x.StudentId == studentId &&
            x.SectionId == sectionId && x.PolicyId == id && x.Status == "Approved" && x.ExpiresAtUtc > now).Select(x => x.Rule).ToListAsync(ct)).ToHashSet();
        var plan = await db.StudentCurriculumAssignments.AsNoTracking().Where(x => x.InstitutionId == institutionId && x.StudentId == studentId)
            .Select(x => (Guid?)x.PlanId).SingleOrDefaultAsync(ct);
        var courseCredits = await db.CurriculumCourses.AsNoTracking().Where(x => x.InstitutionId == institutionId && x.PlanId == plan)
            .Select(x => new { x.CourseId, x.CreditHours }).ToDictionaryAsync(x => x.CourseId, x => x.CreditHours, ct);
        var enrolledCourses = await (from e in db.StudentEnrollments.AsNoTracking()
            join s in db.CourseSections.AsNoTracking() on e.SectionId equals s.Id
            where e.InstitutionId == institutionId && e.StudentId == studentId && e.Status == "Active" && s.TermId == section.TermId && s.Id != sectionId
            select s.CourseId).ToListAsync(ct);
        var reasons = AcademicPolicyRulesValidator.Evaluate(rules, prerequisites, hasHold,
            enrolledCourses.Sum(c => courseCredits.GetValueOrDefault(c)), courseCredits.GetValueOrDefault(section.CourseId), exceptions).ToList();
        if (rules.MaximumTermCredits.HasValue && (plan is null || !courseCredits.ContainsKey(section.CourseId) || enrolledCourses.Any(c => !courseCredits.ContainsKey(c))))
            reasons.Add("CreditMappingUnavailable"); // Never treat unknown credits as zero under a credit limit.
        if(await db.StudentProgramMemberships.AnyAsync(x=>x.InstitutionId==institutionId&&x.StudentId==studentId&&x.Status!="Active",ct))reasons.Add("ProgramInactive");
        if (!await db.AcademicTerms.AnyAsync(x => x.Id == section.TermId && x.InstitutionId == institutionId && x.Status == "Active", ct)) reasons.Add("TermInactive");
        reasons.AddRange(await Academix.Api.Services.Finance.FinanceDebt.Restrictions(db,institutionId,studentId,"Registration",DateTime.UtcNow,ct));
        return new(id, reasons.Count == 0, enrolledCourses.Sum(c => courseCredits.GetValueOrDefault(c)), courseCredits.GetValueOrDefault(section.CourseId), reasons);
    }
}
