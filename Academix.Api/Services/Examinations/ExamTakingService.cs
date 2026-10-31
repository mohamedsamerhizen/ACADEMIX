using Academix.Api.Services.Infrastructure;
using System.Security.Claims;
using System.Text.Json;
using Academix.Api.Data;
using Academix.Api.Domain.Examinations;
using Academix.Api.Domain.Identity;
using Academix.Api.DTOs.Examinations;
using Academix.Api.Middleware;
using Academix.Api.Services.Identity;
using Microsoft.EntityFrameworkCore;

namespace Academix.Api.Services.Examinations;

public sealed class ExamTakingService(AcademixDbContext db, IdentitySecurity access, RetryingServiceWork retry)
{
    private async Task<ApplicationUser> Student(ClaimsPrincipal principal, Guid institutionId,
        Guid sectionId, CancellationToken ct)
    {
        var (actor, roles) = await access.CurrentAsync(principal, ct);
        if (actor.InstitutionId != institutionId) throw ApiFault.NotFound();
        if (!roles.Contains(AcademixRoles.Student)) throw ApiFault.Forbidden();
        if (!await db.StudentEnrollments.AnyAsync(e => e.StudentId == actor.Id &&
            e.SectionId == sectionId && e.InstitutionId == institutionId &&
            e.Status == "Active", ct)) throw ApiFault.Forbidden();
        return actor;
    }

    private async Task<ExamDefinition> Published(Guid examId, CancellationToken ct)
        => await db.Exams.AsNoTracking().SingleOrDefaultAsync(x => x.Id == examId &&
            x.Status == "Published", ct) ?? throw ApiFault.NotFound();

    private async Task<ExamAttempt> Owned(ClaimsPrincipal principal, Guid attemptId, CancellationToken ct)
    {
        var attempt = await db.ExamAttempts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == attemptId, ct)
            ?? throw ApiFault.NotFound();
        var actor = await Student(principal, attempt.InstitutionId, attempt.SectionId, ct);
        if (actor.Id != attempt.StudentId) throw ApiFault.NotFound();
        return attempt;
    }

    private static string Status(ExamAttempt a, DateTime now)
        => ExamAttemptPolicy.IsExpired(a.Status, a.DeadlineUtc, now) ? "Expired" : a.Status;

    private static ExamAttemptSummary Summary(ExamAttempt a, DateTime now)
        => new(a.Id, a.ExamId, a.AttemptNumber, Status(a, now), a.StartedAtUtc,
            a.DeadlineUtc, a.SubmittedAtUtc, now);

    private async Task ReconcileExpiry(Guid attemptId, CancellationToken ct)
    {
        await new ExamExpirationService(db).FinalizeOne(attemptId, ct);
    }

    private async Task<ExamAttemptDetail> Detail(Guid attemptId, CancellationToken ct)
    {
        await ReconcileExpiry(attemptId, ct);
        var a = await db.ExamAttempts.AsNoTracking().SingleAsync(x => x.Id == attemptId, ct);
        var exam = await db.Exams.AsNoTracking().SingleAsync(x => x.Id == a.ExamId, ct);
        var order = await db.ExamAttemptQuestions.AsNoTracking().Where(x => x.AttemptId == a.Id)
            .OrderBy(x => x.Position).ToListAsync(ct);
        var ids = order.Select(x => x.PublishedQuestionId).ToArray();
        var published = await db.ExamPublishedQuestions.AsNoTracking().Where(x =>
            x.ExamId == a.ExamId && ids.Contains(x.Id)).ToDictionaryAsync(x => x.Id, ct);
        var questions = order.Select(x =>
        {
            var q = published[x.PublishedQuestionId];
            var body = JsonSerializer.Deserialize<QuestionBody>(q.BodyJson)
                ?? throw new InvalidOperationException("Stored question snapshot is invalid.");
            return new StudentExamQuestion(q.Id, x.Position, q.Points,
                body.Kind, body.Stem, body.Options);
        }).ToList();
        var saved = await db.ExamAttemptAnswers.AsNoTracking().Where(x => x.AttemptId == a.Id)
            .OrderBy(x => x.SavedAtUtc).ToListAsync(ct);
        return new ExamAttemptDetail(Summary(a, DateTime.UtcNow), exam.Title, questions,
            saved.Select(x => new SavedExamAnswer(x.PublishedQuestionId,
                JsonSerializer.Deserialize<JsonElement>(x.AnswerJson), x.ClientSequence,
                x.SavedAtUtc)).ToList());
    }

    public async Task<IReadOnlyList<AvailableExamView>> Available(ClaimsPrincipal principal,
        Guid sectionId, CancellationToken ct)
    {
        var section = await db.CourseSections.AsNoTracking().SingleOrDefaultAsync(s => s.Id == sectionId, ct)
            ?? throw ApiFault.NotFound();
        var student = await Student(principal, section.InstitutionId, sectionId, ct);
        var exams = await db.Exams.AsNoTracking().Where(e => e.SectionId == sectionId &&
            e.InstitutionId == section.InstitutionId && e.Status == "Published")
            .OrderBy(e => e.OpensAtUtc).Take(200).ToListAsync(ct);
        var ids = exams.Select(e => e.Id).ToArray();
        var mine = await db.ExamAttempts.AsNoTracking().Where(a =>
            a.StudentId == student.Id && ids.Contains(a.ExamId)).ToListAsync(ct);
        var byExam = mine.ToLookup(a => a.ExamId);
        var now = DateTime.UtcNow;
        return exams.Select(e =>
        {
            var own = byExam[e.Id].ToArray();
            var resume = own.Any(a => a.Status == "InProgress" && a.DeadlineUtc > now);
            var canStart = resume || own.Length < e.MaxAttempts &&
                ExamAttemptPolicy.CanStart(now, e.OpensAtUtc, e.ClosesAtUtc);
            return new AvailableExamView(e.Id, e.SectionId, e.Title, e.OpensAtUtc,
                e.ClosesAtUtc, e.DurationMinutes, e.MaxAttempts, own.Length, canStart, resume);
        }).ToList();
    }

    private async Task<ExamDefinition> LockExam(Guid examId, CancellationToken ct)
        => (await db.Exams.FromSqlInterpolated(
            $"SELECT * FROM exam_definitions WHERE \"Id\" = {examId} FOR UPDATE")
            .ToListAsync(ct)).SingleOrDefault() ?? throw ApiFault.NotFound();

    private async Task<ExamAttempt> LockAttempt(Guid attemptId, CancellationToken ct)
        => (await db.ExamAttempts.FromSqlInterpolated(
            $"SELECT * FROM exam_attempts WHERE \"Id\" = {attemptId} FOR UPDATE")
            .ToListAsync(ct)).SingleOrDefault() ?? throw ApiFault.NotFound();

    public Task<ExamAttemptDetail> Start(ClaimsPrincipal principal, Guid examId, CancellationToken ct)
        => retry.ExecuteAsync<ExamTakingService, ExamAttemptDetail>(service => service.StartCore(principal, examId, ct), ct);

    private async Task<ExamAttemptDetail> StartCore(ClaimsPrincipal principal, Guid examId, CancellationToken ct)
    {
        var visible = await Published(examId, ct);
        await Student(principal, visible.InstitutionId, visible.SectionId, ct);
        var strategy = db.Database.CreateExecutionStrategy();
        var selected = await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var exam = await LockExam(examId, ct);
            if (exam.Status != "Published") throw ApiFault.NotFound();
            var actor = await Student(principal, exam.InstitutionId, exam.SectionId, ct);
            var now = DateTime.UtcNow;
            var attempts = await db.ExamAttempts.Where(x => x.ExamId == examId &&
                x.StudentId == actor.Id).OrderBy(x => x.AttemptNumber).ToListAsync(ct);
            foreach (var old in attempts.Where(x => ExamAttemptPolicy.IsExpired(x.Status, x.DeadlineUtc, now) &&
                x.Status == "InProgress"))
            {
                access.Audit(null, old.InstitutionId, "exam.attempt.timeout", old.Id.ToString());
                old.Status = "Expired";
                old.Version++;
                old.LastActivityAtUtc = now;
            }
            // A repeated start resumes the same sitting, even after the exam's opening window closes.
            var active = attempts.FirstOrDefault(x => x.Status == "InProgress");
            if (active is not null)
            {
                await db.SaveChangesAsync(ct);
                await tx.CommitAsync(ct);
                return active.Id;
            }
            if (!ExamAttemptPolicy.CanStart(now, exam.OpensAtUtc, exam.ClosesAtUtc))
                throw ApiFault.Conflict("Exam is not open for new attempts.");
            if (attempts.Count >= exam.MaxAttempts)
                throw ApiFault.Conflict("Maximum number of attempts reached.");
            var questions = await db.ExamPublishedQuestions.AsNoTracking().Where(x => x.ExamId == exam.Id)
                .OrderBy(x => x.Position).ToListAsync(ct);
            if (questions.Count == 0) throw ApiFault.Conflict("Published exam has no questions.");
            var deadline = ExamAttemptPolicy.Deadline(now, exam.DurationMinutes, exam.ClosesAtUtc);
            var attempt = new ExamAttempt
            {
                ExamId = exam.Id, InstitutionId = exam.InstitutionId, CourseId = exam.CourseId,
                SectionId = exam.SectionId, StudentId = actor.Id,
                AttemptNumber = attempts.Count + 1, StartedAtUtc = now,
                DeadlineUtc = deadline, LastActivityAtUtc = now
            };
            db.ExamAttempts.Add(attempt);
            var order = ExamAttemptPolicy.OrderQuestions(questions, exam.ShuffleQuestions);
            db.ExamAttemptQuestions.AddRange(order.Select((q, i) => new ExamAttemptQuestion
            {
                AttemptId = attempt.Id, ExamId = exam.Id,
                InstitutionId = exam.InstitutionId, CourseId = exam.CourseId,
                PublishedQuestionId = q.Id, Position = i + 1
            }));
            access.Audit(actor.Id, exam.InstitutionId, "exam.attempt.started", attempt.Id.ToString());
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return attempt.Id;
        });
        return await Detail(selected, ct);
    }

    public async Task<IReadOnlyList<ExamAttemptSummary>> MyAttempts(ClaimsPrincipal principal,
        Guid examId, CancellationToken ct)
    {
        var exam = await Published(examId, ct);
        var actor = await Student(principal, exam.InstitutionId, exam.SectionId, ct);
        var attempts = await db.ExamAttempts.AsNoTracking().Where(x =>
            x.ExamId == examId && x.StudentId == actor.Id)
            .OrderBy(x => x.AttemptNumber).ToListAsync(ct);
        var now = DateTime.UtcNow;
        return attempts.Select(a => Summary(a, now)).ToList();
    }

    public async Task<ExamAttemptDetail> Resume(ClaimsPrincipal principal, Guid attemptId,
        CancellationToken ct)
    {
        await Owned(principal, attemptId, ct);
        return await Detail(attemptId, ct);
    }

    public Task<SavedExamAnswer> SaveAnswer(ClaimsPrincipal principal, Guid attemptId,
        Guid publishedQuestionId, SaveExamAnswerRequest request, CancellationToken ct)
        => retry.ExecuteAsync<ExamTakingService, SavedExamAnswer>(service => service.SaveAnswerCore(principal, attemptId, publishedQuestionId, request, ct), ct);

    private async Task<SavedExamAnswer> SaveAnswerCore(ClaimsPrincipal principal, Guid attemptId,
        Guid publishedQuestionId, SaveExamAnswerRequest request, CancellationToken ct)
    {
        await Owned(principal, attemptId, ct);
        // Serialize ALL writes and final submission through the attempt row lock.
        var strategy = db.Database.CreateExecutionStrategy();
        var result = await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var attempt = await LockAttempt(attemptId, ct);
            var actor = await Student(principal, attempt.InstitutionId, attempt.SectionId, ct);
            if (attempt.StudentId != actor.Id) throw ApiFault.NotFound();
            var now = DateTime.UtcNow; // Must be read AFTER acquiring the lock.
            if (ExamAttemptPolicy.IsExpired(attempt.Status, attempt.DeadlineUtc, now))
            {
                if (attempt.Status == "InProgress")
                {
                    access.Audit(null, attempt.InstitutionId, "exam.attempt.timeout", attempt.Id.ToString());
                    attempt.Status = "Expired";
                    attempt.Version++;
                    attempt.LastActivityAtUtc = now;
                    await db.SaveChangesAsync(ct);
                }
                await tx.CommitAsync(ct);
                return (SavedExamAnswer?)null;
            }
            if (attempt.Status != "InProgress") throw ApiFault.Conflict("Attempt is no longer editable.");
            var included = await db.ExamAttemptQuestions.AnyAsync(q =>
                q.AttemptId == attemptId && q.PublishedQuestionId == publishedQuestionId, ct);
            if (!included) throw ApiFault.NotFound();
            var question = await db.ExamPublishedQuestions.AsNoTracking().SingleAsync(q =>
                q.Id == publishedQuestionId && q.ExamId == attempt.ExamId, ct);
            var body = JsonSerializer.Deserialize<QuestionBody>(question.BodyJson)
                ?? throw new InvalidOperationException("Stored question snapshot is invalid.");
            var normalized = ExamAttemptPolicy.NormalizeAnswer(body.Kind, body.Options, request);
            var answer = await db.ExamAttemptAnswers.SingleOrDefaultAsync(x =>
                x.AttemptId == attemptId && x.PublishedQuestionId == publishedQuestionId, ct);
            if (answer is not null)
            {
                if (request.ClientSequence < answer.ClientSequence ||
                    request.ClientSequence == answer.ClientSequence &&
                    !ExamAttemptPolicy.SameAnswer(answer.ClientSequence, answer.AnswerJson,
                        request.ClientSequence, normalized))
                    throw ApiFault.Conflict("Stale or conflicting answer sequence.");
                if (request.ClientSequence > answer.ClientSequence)
                {
                    answer.AnswerJson = normalized;
                    answer.ClientSequence = request.ClientSequence;
                    answer.SavedAtUtc = now;
                }
            }
            else
            {
                answer = new ExamAttemptAnswer
                {
                    AttemptId = attempt.Id, ExamId = attempt.ExamId,
                    InstitutionId = attempt.InstitutionId, CourseId = attempt.CourseId,
                    PublishedQuestionId = publishedQuestionId,
                    AnswerJson = normalized, ClientSequence = request.ClientSequence,
                    SavedAtUtc = now
                };
                db.ExamAttemptAnswers.Add(answer);
            }
            attempt.LastActivityAtUtc = now;
            attempt.Version++;
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return new SavedExamAnswer(answer.PublishedQuestionId,
                JsonSerializer.Deserialize<JsonElement>(answer.AnswerJson),
                answer.ClientSequence, answer.SavedAtUtc);
        });
        return result ?? throw ApiFault.Conflict("Attempt time has expired.");
    }

    public Task<ExamSubmissionReceipt> Submit(ClaimsPrincipal principal, Guid attemptId,
        CancellationToken ct)
        => retry.ExecuteAsync<ExamTakingService, ExamSubmissionReceipt>(service => service.SubmitCore(principal, attemptId, ct), ct);

    private async Task<ExamSubmissionReceipt> SubmitCore(ClaimsPrincipal principal, Guid attemptId,
        CancellationToken ct)
    {
        await Owned(principal, attemptId, ct);
        var strategy = db.Database.CreateExecutionStrategy();
        var receipt = await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var attempt = await LockAttempt(attemptId, ct);
            var actor = await Student(principal, attempt.InstitutionId, attempt.SectionId, ct);
            if (actor.Id != attempt.StudentId) throw ApiFault.NotFound();
            var now = DateTime.UtcNow;
            // Return the same microsecond timestamp that PostgreSQL persists for replay.
            now = new DateTime(now.Ticks / 10 * 10, DateTimeKind.Utc);
            // A successful submit is idempotent even if the exam later closes.
            if (attempt.Status == "Submitted")
            {
                var count = await db.ExamAttemptAnswers.CountAsync(x => x.AttemptId == attemptId &&
                    x.AnswerJson != "null", ct);
                await tx.CommitAsync(ct);
                return new ExamSubmissionReceipt(attempt.Id, attempt.ExamId,
                    attempt.AttemptNumber, "Submitted", attempt.SubmittedAtUtc!.Value, count);
            }
            if (ExamAttemptPolicy.IsExpired(attempt.Status, attempt.DeadlineUtc, now))
            {
                if (attempt.Status == "InProgress")
                {
                    access.Audit(null, attempt.InstitutionId, "exam.attempt.timeout", attempt.Id.ToString());
                    attempt.Status = "Expired";
                    attempt.LastActivityAtUtc = now;
                    attempt.Version++;
                    await db.SaveChangesAsync(ct);
                }
                await tx.CommitAsync(ct);
                return (ExamSubmissionReceipt?)null;
            }
            if (attempt.Status != "InProgress") throw ApiFault.Conflict("Attempt is not in progress.");
            attempt.Status = "Submitted";
            attempt.SubmittedAtUtc = now;
            attempt.LastActivityAtUtc = now;
            attempt.Version++;
            access.Audit(actor.Id, attempt.InstitutionId, "exam.attempt.submitted", attempt.Id.ToString());
            await db.SaveChangesAsync(ct);
            var answered = await db.ExamAttemptAnswers.CountAsync(x => x.AttemptId == attemptId &&
                x.AnswerJson != "null", ct);
            await tx.CommitAsync(ct);
            return new ExamSubmissionReceipt(attempt.Id, attempt.ExamId, attempt.AttemptNumber,
                attempt.Status, now, answered);
        });
        return receipt ?? throw ApiFault.Conflict("Attempt time has expired.");
    }
}
