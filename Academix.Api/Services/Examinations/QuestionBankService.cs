using System.Security.Claims;
using System.Text.Json;
using Academix.Api.Data;
using Academix.Api.Domain.Courses;
using Academix.Api.Domain.Examinations;
using Academix.Api.Domain.Identity;
using Academix.Api.DTOs.Examinations;
using Academix.Api.Middleware;
using Academix.Api.Services.Identity;
using Microsoft.EntityFrameworkCore;

namespace Academix.Api.Services.Examinations;

// Instructor/TA can manage the bank of a course only while assigned to that
// course in their own institution. Answer keys are intentionally STAFF ONLY.
public sealed class QuestionBankService(AcademixDbContext db, IdentitySecurity access)
{
    private async Task<Course> RequireCourseStaff(ClaimsPrincipal principal, Guid courseId, CancellationToken ct)
    {
        var course = await db.Courses.AsNoTracking().SingleOrDefaultAsync(x => x.Id == courseId, ct)
            ?? throw ApiFault.NotFound();
        var (actor, roles) = await access.CurrentAsync(principal, ct);
        if (roles.Contains(AcademixRoles.SuperAdministrator)) return course;
        if (actor.InstitutionId != course.InstitutionId) throw ApiFault.NotFound();
        if (roles.Contains(AcademixRoles.InstitutionAdministrator)) return course;
        var kinds = TeachingRolePolicy.ActiveAssignmentKinds(roles);
        if (kinds.Length == 0 || !await db.TeachingAssignments.AnyAsync(t =>
            t.UserId == actor.Id && t.InstitutionId == course.InstitutionId && kinds.Contains(t.Kind) &&
            db.CourseSections.Any(s => s.Id == t.SectionId && s.InstitutionId == course.InstitutionId && s.CourseId == courseId), ct))
            throw ApiFault.Forbidden();
        return course;
    }

    private static BankQuestionSummary Summary(BankQuestion question) => new(
        question.Id, question.CourseId, question.Kind, question.Difficulty,
        question.CurrentRevision, question.Version, question.IsArchived, question.UpdatedAtUtc);

    private static QuestionRevisionResponse Revision(BankQuestionRevision item) => new(
        item.Id, item.QuestionId, item.RevisionNumber,
        JsonSerializer.Deserialize<QuestionBody>(item.BodyJson)!,
        JsonSerializer.Deserialize<QuestionAnswerKey>(item.AnswerKeyJson)!,
        item.CreatedBy, item.CreatedAtUtc);

    private async Task<BankQuestion> RequireQuestion(ClaimsPrincipal principal, Guid id, CancellationToken ct)
    {
        var question = await db.BankQuestions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw ApiFault.NotFound();
        await RequireCourseStaff(principal, question.CourseId, ct);
        return question;
    }

    public async Task<BankQuestionDetail> Create(ClaimsPrincipal principal, Guid courseId,
        QuestionDraftRequest draft, CancellationToken ct)
    {
        var course = await RequireCourseStaff(principal, courseId, ct);
        if (!course.IsActive) throw ApiFault.Conflict("Cannot add questions to an inactive course.");
        var (body, answers) = QuestionValidation.Validate(draft);
        var (actor, _) = await access.CurrentAsync(principal, ct);
        var now = DateTime.UtcNow;
        var question = new BankQuestion
        {
            InstitutionId = course.InstitutionId, CourseId = course.Id, CreatedBy = actor.Id,
            Kind = body.Kind, Difficulty = body.Difficulty, CreatedAtUtc = now, UpdatedAtUtc = now
        };
        var revision = new BankQuestionRevision
        {
            QuestionId = question.Id, InstitutionId = course.InstitutionId, CourseId = course.Id,
            RevisionNumber = 1, BodyJson = JsonSerializer.Serialize(body),
            AnswerKeyJson = JsonSerializer.Serialize(answers), CreatedBy = actor.Id, CreatedAtUtc = now
        };
        db.BankQuestions.Add(question);
        db.BankQuestionRevisions.Add(revision);
        access.Audit(actor.Id, course.InstitutionId, "question.created", question.Id.ToString());
        await db.SaveChangesAsync(ct);
        return new BankQuestionDetail(Summary(question), Revision(revision));
    }

    public async Task<IReadOnlyList<BankQuestionSummary>> List(ClaimsPrincipal principal,
        Guid courseId, int page, bool includeArchived, CancellationToken ct)
    {
        await RequireCourseStaff(principal, courseId, ct);
        if (page < 1 || page > 10000) throw ApiFault.Bad("Page out of range.");
        var items = await db.BankQuestions.AsNoTracking().Where(x => x.CourseId == courseId &&
            (includeArchived || !x.IsArchived)).OrderByDescending(x => x.UpdatedAtUtc).ThenBy(x => x.Id)
            .Skip((page - 1) * 50).Take(50).ToListAsync(ct);
        return items.Select(Summary).ToList();
    }

    public async Task<BankQuestionDetail> Get(ClaimsPrincipal principal, Guid id, CancellationToken ct)
    {
        var question = await RequireQuestion(principal, id, ct);
        var revision = await db.BankQuestionRevisions.AsNoTracking()
            .SingleAsync(x => x.QuestionId == id && x.RevisionNumber == question.CurrentRevision, ct);
        return new BankQuestionDetail(Summary(question), Revision(revision));
    }

    public async Task<QuestionRevisionResponse> GetRevision(ClaimsPrincipal principal,
        Guid id, int revisionNumber, CancellationToken ct)
    {
        await RequireQuestion(principal, id, ct);
        var revision = await db.BankQuestionRevisions.AsNoTracking()
            .SingleOrDefaultAsync(x => x.QuestionId == id && x.RevisionNumber == revisionNumber, ct)
            ?? throw ApiFault.NotFound();
        return Revision(revision);
    }

    public async Task<BankQuestionDetail> Edit(ClaimsPrincipal principal, Guid id,
        QuestionEditRequest request, CancellationToken ct)
    {
        var existing = await RequireQuestion(principal, id, ct);
        var (body, answers) = QuestionValidation.Validate(request.Draft);
        var (actor, _) = await access.CurrentAsync(principal, ct);
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            // Serializes writers regardless of which revision they initially read.
            var question = (await db.BankQuestions.FromSqlInterpolated(
                $"SELECT * FROM exam_bank_questions WHERE \"Id\" = {id} FOR UPDATE").ToListAsync(ct))
                .SingleOrDefault() ?? throw ApiFault.NotFound();
            if (question.CourseId != existing.CourseId || question.InstitutionId != existing.InstitutionId)
                throw ApiFault.Conflict("Question scope changed.");
            if (question.Version != request.ExpectedVersion)
                throw ApiFault.Conflict("Question was modified by another request.");
            if (question.IsArchived) throw ApiFault.Conflict("Archived question cannot be edited.");
            var revision = new BankQuestionRevision
            {
                QuestionId = id, InstitutionId = question.InstitutionId, CourseId = question.CourseId,
                RevisionNumber = checked(question.CurrentRevision + 1),
                BodyJson = JsonSerializer.Serialize(body), AnswerKeyJson = JsonSerializer.Serialize(answers),
                CreatedBy = actor.Id, CreatedAtUtc = DateTime.UtcNow
            };
            question.CurrentRevision = revision.RevisionNumber;
            question.Kind = body.Kind;
            question.Difficulty = body.Difficulty;
            question.Version++;
            question.UpdatedAtUtc = revision.CreatedAtUtc;
            db.BankQuestionRevisions.Add(revision);
            access.Audit(actor.Id, question.InstitutionId, "question.revised", $"{id}:{revision.RevisionNumber}");
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            return new BankQuestionDetail(Summary(question), Revision(revision));
        });
    }

    public async Task<BankQuestionSummary> Archive(ClaimsPrincipal principal, Guid id,
        QuestionArchiveRequest request, CancellationToken ct)
    {
        await RequireQuestion(principal, id, ct);
        var (actor, _) = await access.CurrentAsync(principal, ct);
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            var question = (await db.BankQuestions.FromSqlInterpolated(
                $"SELECT * FROM exam_bank_questions WHERE \"Id\" = {id} FOR UPDATE").ToListAsync(ct))
                .SingleOrDefault() ?? throw ApiFault.NotFound();
            if (question.Version != request.ExpectedVersion) throw ApiFault.Conflict("Question was modified by another request.");
            if (question.IsArchived != request.IsArchived)
            {
                question.IsArchived = request.IsArchived;
                question.Version++;
                question.UpdatedAtUtc = DateTime.UtcNow;
                access.Audit(actor.Id, question.InstitutionId,
                    request.IsArchived ? "question.archived" : "question.restored", id.ToString());
                await db.SaveChangesAsync(ct);
            }
            await transaction.CommitAsync(ct);
            return Summary(question);
        });
    }
}
