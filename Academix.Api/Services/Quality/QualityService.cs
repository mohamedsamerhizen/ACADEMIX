using System.Security.Claims;
using System.Text.Json;
using Academix.Api.Data;
using Academix.Api.Domain.Quality;
using Academix.Api.DTOs.Quality;
using Academix.Api.Middleware;
using Academix.Api.Services.Analytics;
using Academix.Api.Services.Identity;
using Academix.Api.Services.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Academix.Api.Services.Quality;

/// <summary>
/// Institution-admin-only academic quality workflows. Course-level grade outcomes are
/// explicitly labelled as a PROXY, never as direct assessment of learning outcomes.
/// </summary>
public sealed class QualityService(AcademixDbContext db, IdentitySecurity security, QualityEvidenceService evidenceService, RetryingServiceWork retry)
{
    private async Task<Guid> Admin(ClaimsPrincipal principal, Guid institutionId, CancellationToken ct)
    {
        if (institutionId == Guid.Empty) throw ApiFault.Bad("Institution is required.");
        var user = await security.RequireAdminAsync(principal, institutionId, ct);
        if (!await db.Institutions.AsNoTracking().AnyAsync(i => i.Id == institutionId && i.IsActive, ct))
            throw ApiFault.NotFound();
        return user.Id;
    }

    private async Task Program(Guid institutionId, Guid programId, CancellationToken ct)
    {
        if (!await db.AcademicPrograms.AsNoTracking().AnyAsync(p => p.Id == programId && p.InstitutionId == institutionId, ct))
            throw ApiFault.NotFound();
    }

    public async Task<IReadOnlyList<ProgramQualityOutcome>> Outcomes(ClaimsPrincipal principal,
        Guid institutionId, Guid programId, CancellationToken ct)
    {
        await Admin(principal, institutionId, ct);
        await Program(institutionId, programId, ct);
        return await db.QualityOutcomes.AsNoTracking().Where(o => o.InstitutionId == institutionId && o.ProgramId == programId)
            .OrderBy(o => o.Code).ToListAsync(ct);
    }

    public Task<ProgramQualityOutcome> SaveOutcome(ClaimsPrincipal principal, Guid institutionId,
        Guid programId, Guid? outcomeId, QualityOutcomeInput input, CancellationToken ct)
        =>retry.ExecuteAsync<QualityService,ProgramQualityOutcome>(s=>s.SaveOutcomeCore(principal,institutionId,programId,outcomeId,input,ct),ct);

    private async Task<ProgramQualityOutcome> SaveOutcomeCore(ClaimsPrincipal principal, Guid institutionId,
        Guid programId, Guid? outcomeId, QualityOutcomeInput input, CancellationToken ct)
    {
        await using var tx=await db.Database.BeginTransactionAsync(ct);
        await Academix.Api.Services.Academic.RegistrationCoordination.Lock(db,institutionId,ct);
        await Admin(principal, institutionId, ct);
        await Program(institutionId, programId, ct);
        var code = QualityRules.Code(input.Code);
        var description = QualityRules.Description(input.Description);
        var target = QualityRules.Target(input.TargetPassingRate);
        var outcome = outcomeId.HasValue ? await db.QualityOutcomes.SingleOrDefaultAsync(o =>
            o.Id == outcomeId.Value && o.InstitutionId == institutionId && o.ProgramId == programId, ct)
            ?? throw ApiFault.NotFound() : new ProgramQualityOutcome { InstitutionId = institutionId, ProgramId = programId };
        if (await db.QualityOutcomes.AsNoTracking().AnyAsync(o => o.InstitutionId == institutionId &&
            o.ProgramId == programId && o.Code == code && o.Id != outcome.Id, ct))
            throw ApiFault.Conflict("Outcome code already exists in the program.");
        outcome.Code = code;
        outcome.Description = description;
        outcome.TargetPassingRate = target;
        if (outcomeId.HasValue) outcome.Version++;
        else db.QualityOutcomes.Add(outcome);
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);
        return outcome;
    }

    public async Task<IReadOnlyList<ProgramOutcomeCourseMap>> CourseMaps(ClaimsPrincipal principal,
        Guid institutionId, Guid outcomeId, CancellationToken ct)
    {
        await Admin(principal, institutionId, ct);
        await Outcome(institutionId, outcomeId, ct);
        return await db.QualityCourseAlignments.AsNoTracking().Where(x => x.InstitutionId == institutionId && x.OutcomeId == outcomeId)
            .OrderBy(x => x.CourseId).ToListAsync(ct);
    }

    private async Task<ProgramQualityOutcome> Outcome(Guid institutionId, Guid outcomeId, CancellationToken ct)
        => await db.QualityOutcomes.SingleOrDefaultAsync(x => x.InstitutionId == institutionId && x.Id == outcomeId, ct)
            ?? throw ApiFault.NotFound();

    public Task<ProgramOutcomeCourseMap> MapCourse(ClaimsPrincipal principal,
        Guid institutionId, Guid outcomeId, QualityCourseMapInput input, CancellationToken ct)
        =>retry.ExecuteAsync<QualityService,ProgramOutcomeCourseMap>(s=>s.MapCourseCore(principal,institutionId,outcomeId,input,ct),ct);

    private async Task<ProgramOutcomeCourseMap> MapCourseCore(ClaimsPrincipal principal,
        Guid institutionId, Guid outcomeId, QualityCourseMapInput input, CancellationToken ct)
    {
        await using var tx=await db.Database.BeginTransactionAsync(ct);
        await Academix.Api.Services.Academic.RegistrationCoordination.Lock(db,institutionId,ct);
        await Admin(principal, institutionId, ct);
        var outcome = await Outcome(institutionId, outcomeId, ct);
        if (!outcome.IsActive) throw ApiFault.Conflict("Inactive outcomes cannot receive mappings.");
        if (!await db.ProgramCourses.AsNoTracking().AnyAsync(x => x.InstitutionId == institutionId &&
            x.ProgramId == outcome.ProgramId && x.CourseId == input.CourseId, ct))
            throw ApiFault.Bad("Course must belong to the same academic program.");
        var mapping = await db.QualityCourseAlignments.SingleOrDefaultAsync(x => x.InstitutionId == institutionId &&
            x.OutcomeId == outcomeId && x.CourseId == input.CourseId, ct);
        if (mapping is not null) return mapping;
        mapping = new ProgramOutcomeCourseMap
        {
            InstitutionId = institutionId, ProgramId = outcome.ProgramId, OutcomeId = outcomeId, CourseId = input.CourseId
        };
        db.QualityCourseAlignments.Add(mapping);
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);
        return mapping;
    }

    public async Task<ProgramQualityReview> StartReview(ClaimsPrincipal principal, Guid institutionId,
        Guid programId, Guid termId, CancellationToken ct)
    {
        var userId = await Admin(principal, institutionId, ct);
        await Program(institutionId, programId, ct);
        if (!await db.AcademicTerms.AsNoTracking().AnyAsync(x => x.Id == termId && x.InstitutionId == institutionId, ct))
            throw ApiFault.Bad("Term is not in this institution.");
        var existing = await db.QualityReviews.AsNoTracking().SingleOrDefaultAsync(x => x.InstitutionId == institutionId &&
            x.ProgramId == programId && x.TermId == termId, ct);
        if (existing is not null) return existing;
        var review = new ProgramQualityReview
        {
            InstitutionId = institutionId, ProgramId = programId, TermId = termId, CreatedBy = userId
        };
        db.QualityReviews.Add(review);
        await db.SaveChangesAsync(ct);
        return review;
    }

    private async Task<ProgramQualityReview> Review(Guid institutionId, Guid reviewId, CancellationToken ct)
        => await db.QualityReviews.SingleOrDefaultAsync(x => x.InstitutionId == institutionId && x.Id == reviewId, ct)
           ?? throw ApiFault.NotFound();

    private async Task<IReadOnlyList<QualityOutcomeEvidence>> LiveEvidence(ProgramQualityReview review, CancellationToken ct)
    {
        var outcomes = await db.QualityOutcomes.AsNoTracking().Where(x => x.InstitutionId == review.InstitutionId &&
            x.ProgramId == review.ProgramId && x.IsActive).OrderBy(x => x.Code).ToListAsync(ct);
        var evidence = new List<QualityOutcomeEvidence>(outcomes.Count);
        foreach (var outcome in outcomes)
        {
            var courseIds = await db.QualityCourseAlignments.AsNoTracking().Where(x =>
                x.InstitutionId == review.InstitutionId && x.OutcomeId == outcome.Id && x.IsActive)
                .Select(x => x.CourseId).ToListAsync(ct);
            if (courseIds.Count == 0)
            {
                evidence.Add(QualityRules.Evidence(outcome.Id, outcome.Code, outcome.TargetPassingRate, 0, 0, 0));
                continue;
            }
            var sections=await db.CourseSections.Where(x=>x.InstitutionId==review.InstitutionId&&x.TermId==review.TermId&&courseIds.Contains(x.CourseId)).Select(x=>x.Id).ToArrayAsync(ct);
            var cohort=await db.StudentProgramMemberships.Where(x=>x.InstitutionId==review.InstitutionId&&x.ProgramId==review.ProgramId).Select(x=>x.StudentId).ToArrayAsync(ct);
            foreach(var section in sections)await AssessmentFreshness.Ensure(db,review.InstitutionId,null,ct,section,cohort);
            // GradebookResults are published results. Each row is one grade record, not a unique student.
            // Restrict to students who have a program membership; course enrollment alone does not establish program membership.
            var results = await (from g in db.GradebookResults.AsNoTracking()
                join section in db.CourseSections.AsNoTracking() on g.SectionId equals section.Id
                where g.InstitutionId == review.InstitutionId && section.InstitutionId == review.InstitutionId &&
                      section.TermId == review.TermId && courseIds.Contains(section.CourseId) &&
                      db.StudentProgramMemberships.AsNoTracking().Any(m =>
                          m.InstitutionId == review.InstitutionId && m.ProgramId == review.ProgramId && m.StudentId == g.StudentId)
                select g.Passed).ToListAsync(ct);
            evidence.Add(QualityRules.Evidence(outcome.Id, outcome.Code, outcome.TargetPassingRate,
                courseIds.Count, results.Count, results.Count(p => p)));
        }
        return evidence;
    }

    public async Task<QualityReviewPreview> Preview(ClaimsPrincipal principal,
        Guid institutionId, Guid reviewId, CancellationToken ct)
    {
        await Admin(principal, institutionId, ct);
        var review = await Review(institutionId, reviewId, ct);
        var frozen = review.Status == "Published";
        var evidence = frozen
            ? JsonSerializer.Deserialize<List<QualityOutcomeEvidence>>(review.SnapshotJson) ?? []
            : await LiveEvidence(review, ct);
        return new QualityReviewPreview(review.Id, review.ProgramId, review.TermId, review.Status,
            evidence, frozen ? review.PublishedAtUtc ?? review.CreatedAtUtc : DateTime.UtcNow);
    }

    public Task<QualityReviewPreview> Publish(ClaimsPrincipal principal,Guid institutionId,Guid reviewId,CancellationToken ct)
        =>retry.ExecuteAsync<QualityService,QualityReviewPreview>(s=>s.PublishCore(principal,institutionId,reviewId,ct),ct);
    private async Task<QualityReviewPreview> PublishCore(ClaimsPrincipal principal,
        Guid institutionId, Guid reviewId, CancellationToken ct)
    {
        await using var tx=await db.Database.BeginTransactionAsync(ct);
        await Academix.Api.Services.Academic.RegistrationCoordination.Lock(db,institutionId,ct);
        await Admin(principal, institutionId, ct);
        var review = await Review(institutionId, reviewId, ct);
        if (review.Status == "Published") { var prior=await Preview(principal,institutionId,reviewId,ct);await tx.CommitAsync(ct);return prior; }
        var evidence = await LiveEvidence(review, ct);
        if (evidence.Count == 0) throw ApiFault.Conflict("Define at least one active outcome before publishing.");
        var direct = await evidenceService.LiveDirect(review, ct);
        review.SnapshotJson = JsonSerializer.Serialize(evidence);
        review.DirectSnapshotJson = JsonSerializer.Serialize(direct);
        review.Status = "Published";
        review.PublishedAtUtc = DateTime.UtcNow;
        review.Version++;
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);
        return new QualityReviewPreview(review.Id, review.ProgramId, review.TermId, review.Status,
            evidence, review.PublishedAtUtc.Value);
    }

    public async Task<QualityImprovementAction> AddAction(ClaimsPrincipal principal,
        Guid institutionId, Guid reviewId, QualityActionInput input, CancellationToken ct)
    {
        var actor = await Admin(principal, institutionId, ct);
        var review = await Review(institutionId, reviewId, ct);
        var description = QualityRules.Description(input.Description);
        if (input.OutcomeId is Guid outcomeId &&
            !await db.QualityOutcomes.AsNoTracking().AnyAsync(x => x.Id == outcomeId &&
                x.InstitutionId == institutionId && x.ProgramId == review.ProgramId, ct))
            throw ApiFault.Bad("Outcome does not belong to the reviewed program.");
        var action = new QualityImprovementAction
        {
            InstitutionId = institutionId, ReviewId = reviewId, OutcomeId = input.OutcomeId,
            Description = description, DueOn = input.DueOn, CreatedBy = actor
        };
        db.QualityImprovementActions.Add(action);
        db.QualityActionEvents.Add(new QualityActionEvent { InstitutionId = institutionId, ActionId = action.Id,
            ActorId = actor, FromStatus = "", ToStatus = "Open", Note = "Created" });
        await db.SaveChangesAsync(ct);
        return action;
    }

    public async Task<QualityImprovementAction> DecideAction(ClaimsPrincipal principal,
        Guid institutionId, Guid actionId, QualityActionDecision input, CancellationToken ct)
    {
        var actor = await Admin(principal, institutionId, ct);
        var action = await db.QualityImprovementActions.SingleOrDefaultAsync(x => x.InstitutionId == institutionId && x.Id == actionId, ct)
            ?? throw ApiFault.NotFound();
        var previousStatus = action.Status;
        action.Status = QualityRules.NextActionStatus(action.Status, input.Status?.Trim() ?? "");
        if (action.Status == "Completed")
        {
            action.ResolutionNote = QualityRules.Description(input.ResolutionNote);
            action.CompletedBy = actor;
            action.CompletedAtUtc = DateTime.UtcNow;
        }
        action.Version++;
        db.QualityActionEvents.Add(new QualityActionEvent { InstitutionId = institutionId, ActionId = action.Id,
            ActorId = actor, FromStatus = previousStatus, ToStatus = action.Status,
            Note = action.Status == "Completed" ? action.ResolutionNote : "Status change" });
        await db.SaveChangesAsync(ct);
        return action;
    }

    public async Task<IReadOnlyList<QualityImprovementAction>> Actions(ClaimsPrincipal principal,
        Guid institutionId, Guid reviewId, CancellationToken ct)
    {
        await Admin(principal, institutionId, ct);
        await Review(institutionId, reviewId, ct);
        return await db.QualityImprovementActions.AsNoTracking().Where(x => x.InstitutionId == institutionId && x.ReviewId == reviewId)
            .OrderBy(x => x.CreatedAtUtc).ThenBy(x => x.Id).ToListAsync(ct);
    }

    public async Task<QualityDashboard> Dashboard(ClaimsPrincipal principal, Guid institutionId, CancellationToken ct)
    {
        await Admin(principal, institutionId, ct);
        return new QualityDashboard(institutionId,
            await db.QualityOutcomes.AsNoTracking().CountAsync(x => x.InstitutionId == institutionId, ct),
            await db.QualityCourseAlignments.AsNoTracking().CountAsync(x => x.InstitutionId == institutionId, ct),
            await db.QualityReviews.AsNoTracking().CountAsync(x => x.InstitutionId == institutionId, ct),
            await db.QualityReviews.AsNoTracking().CountAsync(x => x.InstitutionId == institutionId && x.Status == "Published", ct),
            await db.QualityImprovementActions.AsNoTracking().CountAsync(x => x.InstitutionId == institutionId && x.Status != "Completed", ct),
            DateTime.UtcNow);
    }

    public async Task<string> ExportReview(ClaimsPrincipal principal, Guid institutionId, Guid reviewId, CancellationToken ct)
    {
        var preview = await Preview(principal, institutionId, reviewId, ct);
        var lines = new List<string>
        {
            AnalyticsPolicy.CsvRow("OutcomeCode", "TargetPercent", "MappedCourses", "GradeRecords", "PassingGradeRecords", "PassingPercent", "AssessmentStatus", "EvidenceKind")
        };
        lines.AddRange(preview.Evidence.Select(x => AnalyticsPolicy.CsvRow(x.Code, x.TargetPassingRate,
            x.MappedCourses, x.PublishedGradeRecords, x.PassingGradeRecords, x.PassingRate,
            x.AssessmentStatus, x.EvidenceKind)));
        return string.Join("\r\n", lines) + "\r\n";
    }
}
