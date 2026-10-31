using System.Security.Claims;
using System.Text.Json;
using Academix.Api.Data;
using Academix.Api.Domain.Academic;
using Academix.Api.Domain.Examinations;
using Academix.Api.Domain.Identity;
using Academix.Api.DTOs.Examinations;
using Academix.Api.Middleware;
using Academix.Api.Services.Identity;
using Microsoft.EntityFrameworkCore;

namespace Academix.Api.Services.Examinations;

public sealed class ExamBuilderService(AcademixDbContext db, IdentitySecurity access)
{
    private async Task<CourseSection> RequireStaff(ClaimsPrincipal principal, Guid sectionId,
        bool publish, CancellationToken ct)
    {
        var section = await db.CourseSections.AsNoTracking().SingleOrDefaultAsync(s => s.Id == sectionId, ct)
            ?? throw ApiFault.NotFound();
        var (actor, roles) = await access.CurrentAsync(principal, ct);
        if (roles.Contains(AcademixRoles.SuperAdministrator)) return section;
        if (actor.InstitutionId != section.InstitutionId) throw ApiFault.NotFound();
        if (roles.Contains(AcademixRoles.InstitutionAdministrator)) return section;
        string[] requiredKinds = publish
            ? (roles.Contains(AcademixRoles.Instructor) ? [AcademixRoles.Instructor] : [])
            : TeachingRolePolicy.ActiveAssignmentKinds(roles);
        if (requiredKinds.Length == 0 || !await db.TeachingAssignments.AnyAsync(t =>
            t.SectionId == sectionId && t.InstitutionId == section.InstitutionId &&
            t.UserId == actor.Id && requiredKinds.Contains(t.Kind), ct))
            throw ApiFault.Forbidden();
        return section;
    }

    private static ExamSummary Summary(ExamDefinition exam) => new(exam.Id, exam.SectionId, exam.CourseId,
        exam.Title, exam.Status, exam.Version, exam.DurationMinutes, exam.MaxAttempts,
        exam.OpensAtUtc, exam.ClosesAtUtc, exam.ShuffleQuestions, exam.PublishedAtUtc);

    private async Task<ExamDefinition> RequireExam(ClaimsPrincipal principal, Guid id, bool publish,
        CancellationToken ct)
    {
        var exam = await db.Exams.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw ApiFault.NotFound();
        var section = await RequireStaff(principal, exam.SectionId, publish, ct);
        if (exam.InstitutionId != section.InstitutionId || exam.CourseId != section.CourseId)
            throw ApiFault.NotFound();
        return exam;
    }

    private static void RequireDraft(ExamDefinition exam, long expected)
    {
        if (exam.Status != "Draft") throw ApiFault.Conflict("Published exams are immutable.");
        if (exam.Version != expected) throw ApiFault.Conflict("The exam was modified by another request.");
    }

    private async Task<ExamDefinition> LockExam(Guid id, CancellationToken ct)
        => (await db.Exams.FromSqlInterpolated(
            $"SELECT * FROM exam_definitions WHERE \"Id\" = {id} FOR UPDATE").ToListAsync(ct))
            .SingleOrDefault() ?? throw ApiFault.NotFound();

    private async Task<ExamDetail> Detail(ExamDefinition exam, CancellationToken ct)
    {
        var items = await db.ExamDraftQuestions.AsNoTracking().Where(q => q.ExamId == exam.Id)
            .OrderBy(q => q.Position).Select(q => new ExamDraftQuestionView(q.QuestionId,
                q.RevisionNumber, q.Position, q.Points)).ToListAsync(ct);
        var pools = await db.ExamQuestionPools.AsNoTracking().Where(p => p.ExamId == exam.Id)
            .OrderBy(p => p.Position).Select(p => new ExamPoolView(p.Position, p.Kind,
                p.Difficulty, p.Quantity, p.Points)).ToListAsync(ct);
        var total = items.Sum(x => x.Points) + pools.Sum(p => p.Quantity * p.Points);
        return new ExamDetail(Summary(exam), exam.Description, items, total, pools,
            items.Count + pools.Sum(p => p.Quantity));
    }

    public async Task<ExamDetail> Create(ClaimsPrincipal principal, Guid sectionId,
        ExamDraftRequest request, CancellationToken ct)
    {
        var section = await RequireStaff(principal, sectionId, false, ct);
        var (title, description) = ExamValidation.ValidateDraft(request);
        var (actor, _) = await access.CurrentAsync(principal, ct);
        var exam = new ExamDefinition
        {
            InstitutionId = section.InstitutionId, CourseId = section.CourseId, SectionId = section.Id,
            CreatedBy = actor.Id, Title = title, Description = description,
            DurationMinutes = request.DurationMinutes, MaxAttempts = request.MaxAttempts,
            OpensAtUtc = request.OpensAtUtc, ClosesAtUtc = request.ClosesAtUtc,
            ShuffleQuestions = request.ShuffleQuestions
        };
        db.Exams.Add(exam);
        access.Audit(actor.Id, exam.InstitutionId, "exam.draft.created", exam.Id.ToString());
        await db.SaveChangesAsync(ct);
        return await Detail(exam, ct);
    }

    public async Task<IReadOnlyList<ExamSummary>> List(ClaimsPrincipal principal, Guid sectionId,
        int page, CancellationToken ct)
    {
        var section = await RequireStaff(principal, sectionId, false, ct);
        if (page is < 1 or > 10000) throw ApiFault.Bad("Page out of range.");
        var exams = await db.Exams.AsNoTracking().Where(x => x.SectionId == sectionId &&
            x.InstitutionId == section.InstitutionId).OrderByDescending(x => x.CreatedAtUtc)
            .ThenBy(x => x.Id).Skip((page - 1) * 50).Take(50).ToListAsync(ct);
        return exams.Select(Summary).ToList();
    }

    public async Task<ExamDetail> Get(ClaimsPrincipal principal, Guid id, CancellationToken ct)
        => await Detail(await RequireExam(principal, id, false, ct), ct);

    public async Task<ExamDetail> Edit(ClaimsPrincipal principal, Guid id, ExamEditRequest request,
        CancellationToken ct)
    {
        await RequireExam(principal, id, false, ct);
        var (title, description) = ExamValidation.ValidateDraft(request.Draft);
        var (actor, _) = await access.CurrentAsync(principal, ct);
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var exam = await LockExam(id, ct);
            await RequireExam(principal, id, false, ct);
            RequireDraft(exam, request.ExpectedVersion);
            exam.Title = title;
            exam.Description = description;
            exam.DurationMinutes = request.Draft.DurationMinutes;
            exam.MaxAttempts = request.Draft.MaxAttempts;
            exam.OpensAtUtc = request.Draft.OpensAtUtc;
            exam.ClosesAtUtc = request.Draft.ClosesAtUtc;
            exam.ShuffleQuestions = request.Draft.ShuffleQuestions;
            exam.Version++;
            exam.UpdatedAtUtc = DateTime.UtcNow;
            access.Audit(actor.Id, exam.InstitutionId, "exam.draft.updated", exam.Id.ToString());
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return await Detail(exam, ct);
        });
    }

    public async Task<ExamDetail> ReplaceQuestions(ClaimsPrincipal principal, Guid id,
        ExamReplaceQuestionsRequest request, CancellationToken ct)
    {
        var context = await RequireExam(principal, id, false, ct);
        var selected = ExamValidation.ValidateQuestions(request, allowEmpty: true);
        var (actor, _) = await access.CurrentAsync(principal, ct);
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var exam = await LockExam(id, ct);
            await RequireExam(principal, id, false, ct);
            RequireDraft(exam, request.ExpectedVersion);
            if (exam.CourseId != context.CourseId || exam.InstitutionId != context.InstitutionId)
                throw ApiFault.Conflict("Exam scope changed.");
            var activePools = await LoadPools(id, ct);
            PoolAssembly.ValidateCombined(selected.Count, selected.Sum(q => q.Points), activePools);
            if (selected.Count == 0 && activePools.Count == 0)
                throw ApiFault.Bad("The exam must contain at least one manual question or question pool.");
            var ids = selected.Select(x => x.QuestionId).ToArray();
            var questions = await db.BankQuestions.AsNoTracking().Where(q => ids.Contains(q.Id) &&
                q.CourseId == exam.CourseId && q.InstitutionId == exam.InstitutionId && !q.IsArchived)
                .ToListAsync(ct);
            if (questions.Count != selected.Count) throw ApiFault.Bad("Questions must exist in the same course and be active.");
            var revisionNumbers = selected.Select(x => x.RevisionNumber).Distinct().ToArray();
            var revisions = await db.BankQuestionRevisions.AsNoTracking()
                .Where(r => ids.Contains(r.QuestionId) && revisionNumbers.Contains(r.RevisionNumber) &&
                    r.CourseId == exam.CourseId && r.InstitutionId == exam.InstitutionId)
                .Select(r => new { r.QuestionId, r.RevisionNumber }).ToListAsync(ct);
            var allowed = revisions.Select(r => (r.QuestionId, r.RevisionNumber)).ToHashSet();
            if (selected.Any(q => !allowed.Contains((q.QuestionId, q.RevisionNumber))))
                throw ApiFault.Bad("A selected question revision does not exist.");
            await db.ExamDraftQuestions.Where(q => q.ExamId == id).ExecuteDeleteAsync(ct);
            db.ExamDraftQuestions.AddRange(selected.Select((q, i) => new ExamDraftQuestion
            {
                ExamId = id, InstitutionId = exam.InstitutionId, CourseId = exam.CourseId,
                QuestionId = q.QuestionId, RevisionNumber = q.RevisionNumber,
                Position = i + 1, Points = q.Points
            }));
            exam.Version++;
            exam.UpdatedAtUtc = DateTime.UtcNow;
            access.Audit(actor.Id, exam.InstitutionId, "exam.draft.questions.replaced", id.ToString());
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return await Detail(exam, ct);
        });
    }

    private async Task<List<ExamPoolView>> LoadPools(Guid examId, CancellationToken ct)
        => await db.ExamQuestionPools.AsNoTracking().Where(p => p.ExamId == examId)
            .OrderBy(p => p.Position).Select(p => new ExamPoolView(p.Position, p.Kind,
                p.Difficulty, p.Quantity, p.Points)).ToListAsync(ct);

    private async Task<List<PoolAssembly.Candidate>> CandidateBank(ExamDefinition exam,
        IReadOnlyList<ExamPoolView> pools, CancellationToken ct)
    {
        if (pools.Count == 0) return [];
        var kinds = pools.Select(p => p.Kind).Distinct().ToArray();
        var questions = await db.BankQuestions.AsNoTracking()
            .Where(q => q.InstitutionId == exam.InstitutionId && q.CourseId == exam.CourseId &&
                !q.IsArchived && kinds.Contains(q.Kind))
            .Select(q => new { q.Id, q.CurrentRevision, q.Kind, q.Difficulty }).ToListAsync(ct);
        return questions.Select(q => new PoolAssembly.Candidate(q.Id, q.CurrentRevision,
            q.Kind, q.Difficulty)).ToList();
    }

    public async Task<ExamDetail> ReplacePools(ClaimsPrincipal principal, Guid id,
        ExamReplacePoolsRequest request, CancellationToken ct)
    {
        await RequireExam(principal, id, false, ct);
        var pools = PoolAssembly.Validate(request);
        var (actor, _) = await access.CurrentAsync(principal, ct);
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var exam = await LockExam(id, ct);
            await RequireExam(principal, id, false, ct);
            RequireDraft(exam, request.ExpectedVersion);
            var manual = await db.ExamDraftQuestions.AsNoTracking().Where(q => q.ExamId == id)
                .Select(q => q.Points).ToListAsync(ct);
            PoolAssembly.ValidateCombined(manual.Count, manual.Sum(), pools);
            if (manual.Count == 0 && pools.Count == 0)
                throw ApiFault.Bad("The exam must contain at least one manual question or question pool.");
            await db.ExamQuestionPools.Where(p => p.ExamId == id).ExecuteDeleteAsync(ct);
            db.ExamQuestionPools.AddRange(pools.Select(pool => new ExamQuestionPool
            {
                ExamId = id, InstitutionId = exam.InstitutionId, CourseId = exam.CourseId,
                Position = pool.Position, Kind = pool.Kind, Difficulty = pool.Difficulty,
                Quantity = pool.Quantity, Points = pool.Points
            }));
            exam.Version++;
            exam.UpdatedAtUtc = DateTime.UtcNow;
            access.Audit(actor.Id, exam.InstitutionId, "exam.draft.pools.replaced", id.ToString());
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return await Detail(exam, ct);
        });
    }

    public async Task<ExamAssemblyCheck> CheckAssembly(ClaimsPrincipal principal, Guid id,
        CancellationToken ct)
    {
        var exam = await RequireExam(principal, id, false, ct);
        var manual = await db.ExamDraftQuestions.AsNoTracking().Where(q => q.ExamId == id)
            .Select(q => new { q.QuestionId, q.Points }).ToListAsync(ct);
        var pools = await LoadPools(id, ct);
        var total = manual.Sum(q => q.Points) + pools.Sum(p => p.Quantity * p.Points);
        var count = manual.Count + pools.Sum(p => p.Quantity);
        if (exam.Status != "Draft")
            return new ExamAssemblyCheck(false, manual.Count, count - manual.Count, count, total,
                "Exam is already published.");
        if (count == 0 || count > 200 || total > 10000m)
            return new ExamAssemblyCheck(false, manual.Count, count - manual.Count, count, total,
                "Exam must contain 1-200 questions and at most 10000 total points.");
        if (exam.ClosesAtUtc <= DateTime.UtcNow)
            return new ExamAssemblyCheck(false, manual.Count, count - manual.Count, count, total,
                "Exam window has already closed.");
        try
        {
            var available = await CandidateBank(exam, pools, ct);
            PoolAssembly.Allocate(pools, available, manual.Select(q => q.QuestionId));
            return new ExamAssemblyCheck(true, manual.Count, count - manual.Count, count, total, null);
        }
        catch (ApiFault ex) when (ex.StatusCode == 409)
        {
            return new ExamAssemblyCheck(false, manual.Count, count - manual.Count, count, total, ex.Message);
        }
    }

    public async Task<ExamPublishedPreview> Publish(ClaimsPrincipal principal, Guid id,
        ExamPublishRequest request, CancellationToken ct)
    {
        await RequireExam(principal, id, true, ct);
        var (actor, _) = await access.CurrentAsync(principal, ct);
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var exam = await LockExam(id, ct);
            await RequireExam(principal, id, true, ct);
            RequireDraft(exam, request.ExpectedVersion);
            if (exam.ClosesAtUtc <= DateTime.UtcNow)
                throw ApiFault.Conflict("Cannot publish an exam whose window has already closed.");
            var items = await db.ExamDraftQuestions.AsNoTracking().Where(q => q.ExamId == id)
                .OrderBy(q => q.Position).ToListAsync(ct);
            var pools = await LoadPools(id, ct);
            if (items.Count == 0 && pools.Count == 0)
                throw ApiFault.Conflict("Add at least one question or pool before publishing.");
            // Publishing is serialized by the exam row lock; re-evaluate readiness INSIDE
            // the transaction so concurrent draft changes cannot invalidate assembly.
            PoolAssembly.ValidateCombined(items.Count, items.Sum(q => q.Points), pools);
            var candidates = await CandidateBank(exam, pools, ct);
            var allocations = PoolAssembly.Allocate(pools, candidates, items.Select(q => q.QuestionId));
            var selected = items.Select(q => new
            {
                q.QuestionId, q.RevisionNumber, q.Points
            }).ToList();
            var poolByPosition = pools.ToDictionary(p => p.Position);
            selected.AddRange(allocations.Select(a => new
            {
                a.QuestionId, a.RevisionNumber,
                Points = poolByPosition[a.PoolPosition].Points
            }));
            var questionIds = selected.Select(q => q.QuestionId).ToArray();
            var requestedRevisions = selected.Select(q => q.RevisionNumber).Distinct().ToArray();
            var revisions = await db.BankQuestionRevisions.AsNoTracking()
                .Where(r => questionIds.Contains(r.QuestionId) && requestedRevisions.Contains(r.RevisionNumber) &&
                    r.InstitutionId == exam.InstitutionId && r.CourseId == exam.CourseId).ToListAsync(ct);
            var byId = revisions.ToDictionary(x => (x.QuestionId, x.RevisionNumber));
            if (selected.Any(q => !byId.ContainsKey((q.QuestionId, q.RevisionNumber))))
                throw ApiFault.Conflict("One or more selected question revisions are missing.");
            db.ExamPublishedQuestions.AddRange(selected.Select((q, i) =>
            {
                var revision = byId[(q.QuestionId, q.RevisionNumber)];
                return new ExamPublishedQuestion
                {
                    ExamId = id, InstitutionId = exam.InstitutionId, CourseId = exam.CourseId,
                    QuestionId = q.QuestionId, RevisionNumber = q.RevisionNumber,
                    Position = i + 1, Points = q.Points,
                    BodyJson = revision.BodyJson, AnswerKeyJson = revision.AnswerKeyJson
                };
            }));
            exam.Status = "Published";
            exam.PublishedAtUtc = DateTime.UtcNow;
            exam.UpdatedAtUtc = exam.PublishedAtUtc.Value;
            exam.Version++;
            access.Audit(actor.Id, exam.InstitutionId, "exam.published", exam.Id.ToString());
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return await PublishedPreview(exam, ct);
        });
    }

    private async Task<ExamPublishedPreview> PublishedPreview(ExamDefinition exam, CancellationToken ct)
    {
        if (exam.Status != "Published") throw ApiFault.Conflict("Exam is not published.");
        var items = await db.ExamPublishedQuestions.AsNoTracking().Where(q => q.ExamId == exam.Id)
            .OrderBy(q => q.Position).ToListAsync(ct);
        var visible = items.Select(q =>
        {
            var body = JsonSerializer.Deserialize<QuestionBody>(q.BodyJson)
                ?? throw new InvalidOperationException("Stored question snapshot is invalid.");
            return new ExamPublishedQuestionView(q.Position, q.QuestionId, q.RevisionNumber,
                q.Points, body.Kind, body.Stem, body.Options);
        }).ToList();
        return new ExamPublishedPreview(Summary(exam), visible, visible.Sum(q => q.Points));
    }

    public async Task<ExamPublishedPreview> GetPublishedPreview(ClaimsPrincipal principal,
        Guid id, CancellationToken ct)
        => await PublishedPreview(await RequireExam(principal, id, false, ct), ct);
}
