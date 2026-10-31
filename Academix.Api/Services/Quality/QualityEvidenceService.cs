using System.Data;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Academix.Api.Data;
using Academix.Api.Domain.Quality;
using Academix.Api.DTOs.Quality;
using Academix.Api.Middleware;
using Academix.Api.Services.Identity;
using Academix.Api.Services.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Academix.Api.Services.Quality;

/// <summary>
/// Criterion-level evidence, accreditation evidence bundles, and program quality trends.
/// Direct observations describe graded team rubric criteria: NOT student-level PLO attainment.
/// Neither evidence bundles nor hashes constitute external accreditation.
/// </summary>
public sealed class QualityEvidenceService(AcademixDbContext db, IdentitySecurity security, RetryingServiceWork retry)
{
    private async Task<Guid> Admin(ClaimsPrincipal principal, Guid institutionId, CancellationToken ct)
    {
        if (institutionId == Guid.Empty) throw ApiFault.Bad("Institution is required.");
        var actor = await security.RequireAdminAsync(principal, institutionId, ct);
        if (!await db.Institutions.AsNoTracking().AnyAsync(i => i.Id == institutionId && i.IsActive, ct))
            throw ApiFault.NotFound();
        return actor.Id;
    }

    private async Task<ProgramQualityReview> Review(Guid institutionId, Guid reviewId, CancellationToken ct)
        => await db.QualityReviews.SingleOrDefaultAsync(x => x.InstitutionId == institutionId && x.Id == reviewId, ct)
           ?? throw ApiFault.NotFound();

    private async Task<QualityAccreditationPack> Pack(Guid institutionId, Guid packId, CancellationToken ct)
        => await db.QualityAccreditationPacks.SingleOrDefaultAsync(x => x.InstitutionId == institutionId && x.Id == packId, ct)
           ?? throw ApiFault.NotFound();

    public async Task<IReadOnlyList<QualityCriterionAlignment>> Criteria(ClaimsPrincipal principal,
        Guid institutionId, Guid outcomeId, CancellationToken ct)
    {
        await Admin(principal, institutionId, ct);
        if (!await db.QualityOutcomes.AsNoTracking().AnyAsync(x => x.InstitutionId == institutionId && x.Id == outcomeId, ct))
            throw ApiFault.NotFound();
        return await db.QualityCriterionAlignments.AsNoTracking().Where(x => x.InstitutionId == institutionId && x.OutcomeId == outcomeId)
            .OrderBy(x => x.CreatedAtUtc).ThenBy(x => x.Id).ToArrayAsync(ct);
    }

    public Task<QualityCriterionAlignment> AttachCriterion(ClaimsPrincipal principal, Guid institutionId,
        Guid outcomeId, QualityCriterionMapInput input, CancellationToken ct)
        =>retry.ExecuteAsync<QualityEvidenceService,QualityCriterionAlignment>(s=>s.AttachCriterionCore(principal,institutionId,outcomeId,input,ct),ct);

    private async Task<QualityCriterionAlignment> AttachCriterionCore(ClaimsPrincipal principal, Guid institutionId,
        Guid outcomeId, QualityCriterionMapInput input, CancellationToken ct)
    {
        await using var tx=await db.Database.BeginTransactionAsync(ct);
        await Academix.Api.Services.Academic.RegistrationCoordination.Lock(db,institutionId,ct);
        await Admin(principal, institutionId, ct);
        var threshold = QualityRules.Target(input.ProficiencyThresholdPercent);
        var outcome = await db.QualityOutcomes.AsNoTracking().SingleOrDefaultAsync(x => x.Id == outcomeId &&
            x.InstitutionId == institutionId && x.IsActive, ct) ?? throw ApiFault.NotFound();
        var section = await db.CourseSections.AsNoTracking().SingleOrDefaultAsync(x => x.Id == input.SectionId &&
            x.InstitutionId == institutionId, ct) ?? throw ApiFault.Bad("Section not found in institution.");
        var criterion = await db.RubricCriteria.AsNoTracking().SingleOrDefaultAsync(x => x.Id == input.CriterionId &&
            x.InstitutionId == institutionId && x.SectionId == input.SectionId && x.MaxPoints > 0, ct)
            ?? throw ApiFault.Bad("Criterion must be from this section and have positive maximum points.");
        if (!await db.ProgramCourses.AsNoTracking().AnyAsync(x => x.InstitutionId == institutionId &&
            x.ProgramId == outcome.ProgramId && x.CourseId == section.CourseId, ct))
            throw ApiFault.Bad("Section course does not belong to the program.");
        if (!await db.QualityCourseAlignments.AsNoTracking().AnyAsync(x => x.InstitutionId == institutionId &&
            x.ProgramId == outcome.ProgramId && x.OutcomeId == outcomeId && x.CourseId == section.CourseId && x.IsActive, ct))
            throw ApiFault.Conflict("Map the program outcome to the course before mapping a rubric criterion.");
        var existing = await db.QualityCriterionAlignments.SingleOrDefaultAsync(x => x.InstitutionId == institutionId &&
            x.OutcomeId == outcomeId && x.CriterionId == criterion.Id, ct);
        if (existing is not null)
        {
            if (!existing.IsActive || existing.ProficiencyThresholdPercent != threshold)
                throw ApiFault.Conflict("Criterion mapping already exists with different settings; existing history cannot be silently rewritten.");
            return existing;
        }
        var map = new QualityCriterionAlignment
        {
            InstitutionId = institutionId, ProgramId = outcome.ProgramId, OutcomeId = outcomeId,
            SectionId = input.SectionId, CriterionId = criterion.Id, ProficiencyThresholdPercent = threshold
        };
        db.QualityCriterionAlignments.Add(map);
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);
        return map;
    }

    /// <summary>Authentic, graded, published group-project rubric evidence. Team observations only.</summary>
    public async Task<IReadOnlyList<QualityDirectEvidence>> LiveDirect(ProgramQualityReview review, CancellationToken ct)
    {
        var outcomes = await db.QualityOutcomes.AsNoTracking().Where(x => x.InstitutionId == review.InstitutionId &&
            x.ProgramId == review.ProgramId && x.IsActive).OrderBy(x => x.Code).ToListAsync(ct);
        var result = new List<QualityDirectEvidence>(outcomes.Count);
        foreach (var outcome in outcomes)
        {
            var mappings = await (from map in db.QualityCriterionAlignments.AsNoTracking()
                join section in db.CourseSections.AsNoTracking() on map.SectionId equals section.Id
                where map.InstitutionId == review.InstitutionId && map.ProgramId == review.ProgramId &&
                      map.OutcomeId == outcome.Id && map.IsActive && section.InstitutionId == review.InstitutionId &&
                      section.TermId == review.TermId
                select new { map.CriterionId, map.SectionId, map.ProficiencyThresholdPercent }).ToListAsync(ct);
            var observations = new List<bool>();
            foreach (var mapping in mappings)
            {
                var scores = await (from score in db.RubricCriterionScores.AsNoTracking()
                    join criterion in db.RubricCriteria.AsNoTracking() on score.CriterionId equals criterion.Id
                    join submission in db.GroupProjectSubmissions.AsNoTracking() on score.SubmissionId equals submission.Id
                    join project in db.GroupProjects.AsNoTracking() on submission.ProjectId equals project.Id
                    where score.CriterionId == mapping.CriterionId && criterion.InstitutionId == review.InstitutionId &&
                          criterion.SectionId == mapping.SectionId && criterion.MaxPoints > 0m &&
                          submission.InstitutionId == review.InstitutionId && submission.SectionId == mapping.SectionId &&
                          submission.Status == "Graded" && submission.GradedAtUtc != null &&
                          project.InstitutionId == review.InstitutionId && project.SectionId == mapping.SectionId &&
                          project.IsPublished && project.RubricId == criterion.RubricId &&
                          db.ProjectTeamMembers.AsNoTracking().Any(member => member.InstitutionId == review.InstitutionId &&
                              member.TeamId == submission.TeamId && member.SectionId == mapping.SectionId &&
                              db.StudentProgramMemberships.AsNoTracking().Any(m => m.InstitutionId == review.InstitutionId &&
                                  m.ProgramId == review.ProgramId && m.StudentId == member.StudentId))
                    select new { score.Points, criterion.MaxPoints }).ToListAsync(ct);
                observations.AddRange(scores.Select(x => QualityRules.CriterionIsProficient(
                    x.Points, x.MaxPoints, mapping.ProficiencyThresholdPercent)));
            }
            result.Add(QualityRules.DirectEvidence(outcome.Id, outcome.Code, outcome.TargetPassingRate,
                mappings.Count, observations.Count, observations.Count(x => x)));
        }
        return result;
    }

    public async Task<IReadOnlyList<QualityDirectEvidence>> DirectEvidence(ClaimsPrincipal principal,
        Guid institutionId, Guid reviewId, CancellationToken ct)
    {
        await Admin(principal, institutionId, ct);
        var review = await Review(institutionId, reviewId, ct);
        return review.Status == "Published"
            ? JsonSerializer.Deserialize<List<QualityDirectEvidence>>(review.DirectSnapshotJson) ?? []
            : await LiveDirect(review, ct);
    }

    public async Task<QualityAccreditationPack> CreatePack(ClaimsPrincipal principal, Guid institutionId,
        Guid reviewId, QualityPackInput input, CancellationToken ct)
    {
        var actor = await Admin(principal, institutionId, ct);
        var review = await Review(institutionId, reviewId, ct);
        if (review.Status != "Published") throw ApiFault.Conflict("Publish the academic quality review before creating an evidence pack.");
        var title = QualityRules.PackLabel(input.Title, 200);
        var pack = new QualityAccreditationPack { InstitutionId = institutionId, ReviewId = review.Id,
            Title = title, CreatedBy = actor };
        db.QualityAccreditationPacks.Add(pack);
        await db.SaveChangesAsync(ct);
        return pack;
    }

    public async Task<IReadOnlyList<QualityAccreditationPack>> Packs(ClaimsPrincipal principal,
        Guid institutionId, Guid reviewId, CancellationToken ct)
    {
        await Admin(principal, institutionId, ct);
        await Review(institutionId, reviewId, ct);
        return await db.QualityAccreditationPacks.AsNoTracking().Where(x => x.InstitutionId == institutionId && x.ReviewId == reviewId)
            .OrderByDescending(x => x.CreatedAtUtc).Take(100).ToArrayAsync(ct);
    }

    public async Task<IReadOnlyList<QualityPackItem>> Items(ClaimsPrincipal principal, Guid institutionId,
        Guid packId, CancellationToken ct)
    {
        await Admin(principal, institutionId, ct);
        await Pack(institutionId, packId, ct);
        return await db.QualityPackItems.AsNoTracking().Where(x => x.InstitutionId == institutionId && x.PackId == packId)
            .OrderBy(x => x.EvidenceKind).ThenBy(x => x.SourceId).ToArrayAsync(ct);
    }

    private async Task<string> VerifiedSourceJson(ProgramQualityReview review, string kind, Guid sourceId, CancellationToken ct)
    {
        if (kind == "ReviewProxy" && sourceId == review.Id && review.Status == "Published")
            return JsonSerializer.Serialize(new { Kind = kind, review.Id, review.ProgramId, review.TermId,
                review.PublishedAtUtc, review.SnapshotJson });
        if (kind == "ReviewDirect" && sourceId == review.Id && review.Status == "Published")
            return JsonSerializer.Serialize(new { Kind = kind, review.Id, review.ProgramId, review.TermId,
                review.PublishedAtUtc, review.DirectSnapshotJson });
        if (kind == "ImprovementAction")
        {
            var action = await db.QualityImprovementActions.AsNoTracking().SingleOrDefaultAsync(x =>
                x.InstitutionId == review.InstitutionId && x.ReviewId == review.Id && x.Id == sourceId, ct);
            if (action is not null) return JsonSerializer.Serialize(new
            {
                Kind = kind, action.Id, action.OutcomeId, action.Description, action.Status,
                action.DueOn, action.ResolutionNote, action.CreatedAtUtc, action.CompletedAtUtc
            });
        }
        if (kind == "RubricCriterion")
        {
            var match = await (from map in db.QualityCriterionAlignments.AsNoTracking()
                join criterion in db.RubricCriteria.AsNoTracking() on map.CriterionId equals criterion.Id
                join section in db.CourseSections.AsNoTracking() on map.SectionId equals section.Id
                where map.InstitutionId == review.InstitutionId && map.ProgramId == review.ProgramId &&
                      map.CriterionId == sourceId && section.InstitutionId == review.InstitutionId &&
                      section.TermId == review.TermId && map.IsActive
                select new { criterion.Id, criterion.Name, criterion.MaxPoints, map.OutcomeId,
                    map.ProficiencyThresholdPercent, map.SectionId }).FirstOrDefaultAsync(ct);
            if (match is not null) return JsonSerializer.Serialize(new { Kind = kind, match });
        }
        throw ApiFault.Bad("Evidence source is not a verified source belonging to the review and institution.");
    }

    public Task<QualityPackItem> AttachItem(ClaimsPrincipal principal, Guid institutionId,
        Guid packId, QualityPackItemInput input, CancellationToken ct)
        => retry.ExecuteAsync<QualityEvidenceService,QualityPackItem>(s=>s.AttachItemCore(principal, institutionId, packId, input, ct),ct);

    private async Task<QualityPackItem> AttachItemCore(ClaimsPrincipal principal, Guid institutionId,
        Guid packId, QualityPackItemInput input, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await Academix.Api.Services.Academic.RegistrationCoordination.Lock(db,institutionId,ct);
        var actor = await Admin(principal, institutionId, ct);
        var pack = await Pack(institutionId, packId, ct);
        if (pack.Status != "Draft") throw ApiFault.Conflict("Sealed evidence packs are immutable.");
        var kind = input.EvidenceKind?.Trim() ?? "";
        if (kind is not ("ReviewProxy" or "ReviewDirect" or "ImprovementAction" or "RubricCriterion"))
            throw ApiFault.Bad("Unsupported evidence source kind.");
        var existing = await db.QualityPackItems.AsNoTracking().SingleOrDefaultAsync(x => x.InstitutionId == institutionId &&
            x.PackId == packId && x.EvidenceKind == kind && x.SourceId == input.SourceId, ct);
        if (existing is not null) { await tx.CommitAsync(ct); return existing; }
        var source = await VerifiedSourceJson(await Review(institutionId, pack.ReviewId, ct), kind, input.SourceId, ct);
        var item = new QualityPackItem { InstitutionId = institutionId, PackId = pack.Id, SourceId = input.SourceId,
            EvidenceKind = kind, Label = QualityRules.PackLabel(input.Label, 200), EvidenceJson = source,
            EvidenceSha256 = Digest(source), AddedBy = actor };
        db.QualityPackItems.Add(item);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return item;
    }

    public Task<QualityAccreditationPack> SealPack(ClaimsPrincipal principal, Guid institutionId,
        Guid packId, CancellationToken ct)
        => retry.ExecuteAsync<QualityEvidenceService,QualityAccreditationPack>(s=>s.SealPackCore(principal, institutionId, packId, ct),ct);

    private async Task<QualityAccreditationPack> SealPackCore(ClaimsPrincipal principal, Guid institutionId,
        Guid packId, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await Academix.Api.Services.Academic.RegistrationCoordination.Lock(db,institutionId,ct);
        var actor = await Admin(principal, institutionId, ct);
        var pack = await Pack(institutionId, packId, ct);
        if (pack.Status == "Sealed") { await tx.CommitAsync(ct); return pack; }
        var review = await Review(institutionId, pack.ReviewId, ct);
        if (review.Status != "Published") throw ApiFault.Conflict("Review must be published.");
        var items = await db.QualityPackItems.AsNoTracking().Where(x => x.InstitutionId == institutionId && x.PackId == pack.Id)
            .OrderBy(x => x.EvidenceKind).ThenBy(x => x.SourceId).Select(x => new
            { x.Id, x.EvidenceKind, x.SourceId, x.Label, x.EvidenceJson, x.EvidenceSha256 }).ToListAsync(ct);
        if (items.Count == 0 || !items.Any(x => x.EvidenceKind is "ReviewProxy" or "ReviewDirect"))
            throw ApiFault.Conflict("An evidence pack needs at least one published review snapshot before sealing.");
        pack.ManifestJson = JsonSerializer.Serialize(new { pack.Id, pack.InstitutionId,
            pack.ReviewId, review.ProgramId, review.TermId, pack.Title, Evidence = items });
        pack.ManifestSha256 = Digest(pack.ManifestJson);
        pack.Status = "Sealed";
        pack.SealedBy = actor;
        pack.SealedAtUtc = DateTime.UtcNow;
        pack.Version++;
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return pack;
    }

    public async Task<QualityAccreditationPack> GetPack(ClaimsPrincipal principal, Guid institutionId,
        Guid packId, CancellationToken ct)
    {
        await Admin(principal, institutionId, ct);
        return await Pack(institutionId, packId, ct);
    }

    public async Task<IReadOnlyList<QualityActionEvent>> ActionHistory(ClaimsPrincipal principal,
        Guid institutionId, Guid actionId, CancellationToken ct)
    {
        await Admin(principal, institutionId, ct);
        if (!await db.QualityImprovementActions.AsNoTracking().AnyAsync(x => x.InstitutionId == institutionId && x.Id == actionId, ct))
            throw ApiFault.NotFound();
        return await db.QualityActionEvents.AsNoTracking().Where(x => x.InstitutionId == institutionId && x.ActionId == actionId)
            .OrderBy(x => x.AtUtc).ThenBy(x => x.Id).ToArrayAsync(ct);
    }

    public async Task<IReadOnlyList<QualityProgramTrend>> ProgramTrends(ClaimsPrincipal principal, Guid institutionId,
        Guid programId, CancellationToken ct)
    {
        await Admin(principal, institutionId, ct);
        if (!await db.AcademicPrograms.AsNoTracking().AnyAsync(x => x.Id == programId && x.InstitutionId == institutionId, ct))
            throw ApiFault.NotFound();
        var reviews = await db.QualityReviews.AsNoTracking().Where(x => x.InstitutionId == institutionId &&
            x.ProgramId == programId && x.Status == "Published").OrderBy(x => x.PublishedAtUtc).Take(120).ToListAsync(ct);
        var result = new List<QualityProgramTrend>(reviews.Count);
        foreach (var review in reviews)
        {
            var proxy = JsonSerializer.Deserialize<List<QualityOutcomeEvidence>>(review.SnapshotJson) ?? [];
            var direct = JsonSerializer.Deserialize<List<QualityDirectEvidence>>(review.DirectSnapshotJson) ?? [];
            var actions = await db.QualityImprovementActions.AsNoTracking().Where(x => x.InstitutionId == institutionId &&
                x.ReviewId == review.Id).Select(x => new { x.Status, x.DueOn }).ToArrayAsync(ct);
            result.Add(new QualityProgramTrend(review.ProgramId, review.TermId, review.Id,
                review.PublishedAtUtc ?? review.CreatedAtUtc,
                proxy.Count(x => x.MappedCourses > 0), proxy.Count(x => x.AssessmentStatus == "ProxyTargetMet"),
                direct.Count(x => x.MappedCriteria > 0), direct.Count(x => x.AssessmentStatus == "DirectCriterionTargetMet"),
                direct.Sum(x => x.GradedCriterionObservations), actions.Count(x => x.Status != "Completed"),
                actions.Count(x => x.Status != "Completed" && x.DueOn.HasValue && x.DueOn.Value < DateOnly.FromDateTime(DateTime.UtcNow))));
        }
        return result;
    }

    public static string Digest(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
