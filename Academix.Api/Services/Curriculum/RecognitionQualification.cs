using Academix.Api.Data;
using Academix.Api.Domain.Registrar;
using Academix.Api.DTOs.Curriculum;
using Academix.Api.Services.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Academix.Api.Services.Curriculum;

public sealed record QualifiedRecognition(RecognizedCourseCredit Credit, string Source);

/// <summary>Current qualification of immutable transition history, shared by all credit consumers.</summary>
public static class RecognitionQualification
{
    public static async Task<HashSet<Guid>> LocalSources(AcademixDbContext db,Guid institution,Guid student,CancellationToken ct)
    {
        var attempts=await db.StudentTranscriptEntries.AsNoTracking().Where(x=>x.InstitutionId==institution&&x.StudentId==student)
            .Select(x=>new TranscriptAttempt(x.CourseId,x.TermId,x.CourseCode,x.Percentage,x.Passed)).ToListAsync(ct);
        return CurriculumPolicy.BestAttempts(attempts).Where(x=>x.Passed).Select(x=>x.CourseId).ToHashSet();
    }

    public static async Task<IReadOnlyList<QualifiedRecognition>> Load(AcademixDbContext db,
        Guid institution, Guid student, Guid plan, CancellationToken ct)
    {
        var history = await db.Set<RecognizedCourseCredit>().AsNoTracking()
            .Where(x => x.InstitutionId == institution && x.StudentId == student).ToListAsync(ct);
        var current = history.Where(x => x.PlanId == plan).ToArray();
        if (current.Length == 0) return [];
        // A current transcript is required before a local achievement can qualify recognition.
        await AcademicFreshness.Ensure(db, institution, student, ct);
        var transitionIds = history.Select(x => x.TransitionId).Distinct().ToArray();
        var transitions = await db.Set<CurriculumTransition>().AsNoTracking().Where(x =>
            x.InstitutionId == institution && x.StudentId == student && transitionIds.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, ct);
        var priorPlans = transitions.Values.Select(x => x.FromPlanId).Distinct().ToArray();
        var programs = await db.CurriculumPlans.AsNoTracking().Where(x =>
            x.InstitutionId == institution && priorPlans.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, x => x.ProgramId, ct);
        var passed = await LocalSources(db,institution,student,ct);
        var transfers = await (from line in db.TransferCreditLines.AsNoTracking()
            join petition in db.TransferCreditPetitions.AsNoTracking() on line.PetitionId equals petition.Id
            where line.InstitutionId == institution && petition.InstitutionId == institution &&
                  petition.StudentId == student && petition.Status == "Approved"
            select new { line.TargetCourseId, petition.ProgramId, petition.OriginInstitution, line.OriginCourseCode })
            .ToListAsync(ct);
        var result = new List<QualifiedRecognition>();
        foreach (var credit in current)
        {
            var ancestor = credit;
            var visited = new HashSet<Guid>();
            while (visited.Add(ancestor.Id))
            {
                if (!transitions.TryGetValue(ancestor.TransitionId, out var transition) ||
                    transition.Status != "Applied" || transition.ToPlanId != ancestor.PlanId) break;
                // SourceCourseId is flattened by transition application. Follow the prior
                // recognition to its originating program, preserving external provenance.
                var parents = history.Where(x => x.PlanId == transition.FromPlanId &&
                    x.SourceCourseId == credit.SourceCourseId).ToArray();
                if (parents.Length == 1) { ancestor = parents[0]; continue; }
                if (parents.Length != 0 || !programs.TryGetValue(transition.FromPlanId, out var program)) break;
                string? source = passed.Contains(credit.SourceCourseId) ? "course:" + credit.SourceCourseId : null;
                source ??= transfers.Where(x => x.ProgramId == program && x.TargetCourseId == credit.SourceCourseId)
                    .Select(x => "transfer:" + x.OriginInstitution.Trim().ToUpperInvariant() + ":" + x.OriginCourseCode.Trim().ToUpperInvariant())
                    .Order(StringComparer.Ordinal).FirstOrDefault();
                if (source is not null) result.Add(new(credit, source));
                break;
            }
        }
        return result;
    }
}
