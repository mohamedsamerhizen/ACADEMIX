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

public sealed class ExamGradingService(AcademixDbContext db, IdentitySecurity access, RetryingServiceWork retry)
{
    private async Task<ApplicationUser> Staff(ClaimsPrincipal principal, Guid sectionId,
        bool publish, CancellationToken ct)
    {
        var section = await db.CourseSections.AsNoTracking().SingleOrDefaultAsync(x => x.Id == sectionId, ct)
            ?? throw ApiFault.NotFound();
        var (actor, roles) = await access.CurrentAsync(principal, ct);
        if (roles.Contains(AcademixRoles.SuperAdministrator)) return actor;
        if (actor.InstitutionId != section.InstitutionId) throw ApiFault.NotFound();
        if (roles.Contains(AcademixRoles.InstitutionAdministrator)) return actor;
        string[] kinds = publish
            ? (roles.Contains(AcademixRoles.Instructor) ? new[] { AcademixRoles.Instructor } : Array.Empty<string>())
            : TeachingRolePolicy.ActiveAssignmentKinds(roles);
        if (kinds.Length == 0 || !await db.TeachingAssignments.AnyAsync(t =>
            t.UserId == actor.Id && t.SectionId == sectionId &&
            t.InstitutionId == section.InstitutionId && kinds.Contains(t.Kind), ct))
            throw ApiFault.Forbidden();
        return actor;
    }

    private async Task<(ApplicationUser Actor, ExamAttempt Attempt)> Owner(ClaimsPrincipal principal,
        Guid attemptId, CancellationToken ct)
    {
        var attempt = await db.ExamAttempts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == attemptId, ct)
            ?? throw ApiFault.NotFound();
        var (actor, roles) = await access.CurrentAsync(principal, ct);
        if (actor.Id != attempt.StudentId || actor.InstitutionId != attempt.InstitutionId ||
            !roles.Contains(AcademixRoles.Student)) throw ApiFault.NotFound();
        return (actor, attempt);
    }

    private async Task<ExamGradeReport> StaffReport(ClaimsPrincipal principal, Guid attemptId,
        bool publish, CancellationToken ct)
    {
        var report = await db.ExamGradeReports.AsNoTracking().SingleOrDefaultAsync(x => x.AttemptId == attemptId, ct)
            ?? throw ApiFault.NotFound();
        await Staff(principal, report.SectionId, publish, ct);
        return report;
    }

    private async Task<ExamGradeReport> LockReport(Guid reportId, CancellationToken ct)
        => (await db.ExamGradeReports.FromSqlInterpolated(
            $"SELECT * FROM exam_grade_reports WHERE \"Id\" = {reportId} FOR UPDATE")
            .ToListAsync(ct)).SingleOrDefault() ?? throw ApiFault.NotFound();

    private static ExamGradeSummary Summary(ExamGradeReport report, int pending)
        => new(report.Id, report.AttemptId, report.ExamId, report.Status, report.EarnedPoints,
            report.MaximumPoints, pending, report.Version, report.PublishedAtUtc);

    private async Task<ExamStaffGradeView> StaffDetail(ExamGradeReport report, CancellationToken ct)
    {
        var lines = await db.ExamGradeItems.AsNoTracking().Where(x => x.ReportId == report.Id).ToListAsync(ct);
        var ids = lines.Select(x => x.PublishedQuestionId).ToArray();
        var questions = await db.ExamPublishedQuestions.AsNoTracking().Where(x => ids.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, ct);
        var answers = await db.ExamAttemptAnswers.AsNoTracking().Where(x => x.AttemptId == report.AttemptId)
            .ToDictionaryAsync(x => x.PublishedQuestionId, ct);
        var views = lines.OrderBy(x => questions[x.PublishedQuestionId].Position).Select(x =>
            new ExamGradeItemView(x.PublishedQuestionId, questions[x.PublishedQuestionId].Position,
                JsonSerializer.Deserialize<QuestionBody>(questions[x.PublishedQuestionId].BodyJson)!.Kind,
                x.MaximumPoints, x.EarnedPoints, x.State, x.Feedback,
                answers.TryGetValue(x.PublishedQuestionId, out var response) ? response.AnswerJson : null)).ToList();
        return new ExamStaffGradeView(Summary(report, lines.Count(x => x.EarnedPoints is null)), views);
    }

    public async Task<IReadOnlyList<ExamReviewQueueItem>> Queue(ClaimsPrincipal principal, Guid sectionId,
        int page, CancellationToken ct)
    {
        await Staff(principal, sectionId, false, ct);
        if (page is < 1 or > 10000) throw ApiFault.Bad("Page out of range.");
        var attempts = await db.ExamAttempts.AsNoTracking().Where(x =>
            x.SectionId == sectionId && (x.Status == "Submitted" || x.Status == "Expired"))
            .OrderByDescending(x => x.SubmittedAtUtc).ThenBy(x => x.Id)
            .Skip((page - 1) * 50).Take(50).ToListAsync(ct);
        var ids = attempts.Select(x => x.Id).ToArray();
        var reports = await db.ExamGradeReports.AsNoTracking().Where(x => ids.Contains(x.AttemptId))
            .ToDictionaryAsync(x => x.AttemptId, ct);
        var reportIds = reports.Values.Select(x => x.Id).ToArray();
        var pendingCounts = await db.ExamGradeItems.AsNoTracking().Where(x =>
            reportIds.Contains(x.ReportId) && x.EarnedPoints == null)
            .GroupBy(x => x.ReportId).Select(x => new { Id = x.Key, Count = x.Count() })
            .ToDictionaryAsync(x => x.Id, x => x.Count, ct);
        return attempts.Select(a => new ExamReviewQueueItem(a.Id, a.StudentId, a.Status,
            reports.ContainsKey(a.Id), reports.TryGetValue(a.Id, out var r) ? r.Status : null,
            reports.TryGetValue(a.Id, out var grade) ? pendingCounts.GetValueOrDefault(grade.Id) : null)).ToList();
    }

    public Task<ExamStaffGradeView> Initialize(ClaimsPrincipal principal, Guid attemptId,
        CancellationToken ct)
        => retry.ExecuteAsync<ExamGradingService, ExamStaffGradeView>(service => service.InitializeCore(principal, attemptId, ct), ct);

    private async Task<ExamStaffGradeView> InitializeCore(ClaimsPrincipal principal, Guid attemptId,
        CancellationToken ct)
    {
        var preliminary = await db.ExamAttempts.AsNoTracking().SingleOrDefaultAsync(x => x.Id == attemptId, ct)
            ?? throw ApiFault.NotFound();
        var actor = await Staff(principal, preliminary.SectionId, false, ct);
        var strategy = db.Database.CreateExecutionStrategy();
        var reportId = await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            // Serialize grade initialization with Phase 4 submission using the attempt row lock.
            var attempt = (await db.ExamAttempts.FromSqlInterpolated(
                $"SELECT * FROM exam_attempts WHERE \"Id\" = {attemptId} FOR UPDATE").ToListAsync(ct))
                .SingleOrDefault() ?? throw ApiFault.NotFound();
            if (!ExamAttemptPolicy.CanGrade(attempt.Status)) throw ApiFault.Conflict("Only finalized attempts may be graded.");
            var existing = await db.ExamGradeReports.AsNoTracking().SingleOrDefaultAsync(x =>
                x.AttemptId == attemptId, ct);
            if (existing is not null)
            {
                await tx.CommitAsync(ct);
                return existing.Id;
            }
            var included = await db.ExamAttemptQuestions.AsNoTracking().Where(x => x.AttemptId == attemptId)
                .ToListAsync(ct);
            if (included.Count == 0) throw ApiFault.Conflict("Attempt has no published questions.");
            var ids = included.Select(x => x.PublishedQuestionId).ToArray();
            var questions = await db.ExamPublishedQuestions.AsNoTracking().Where(x => ids.Contains(x.Id))
                .ToDictionaryAsync(x => x.Id, ct);
            var answers = await db.ExamAttemptAnswers.AsNoTracking().Where(x => x.AttemptId == attemptId)
                .ToDictionaryAsync(x => x.PublishedQuestionId, ct);
            var report = new ExamGradeReport
            {
                AttemptId = attempt.Id, ExamId = attempt.ExamId, InstitutionId = attempt.InstitutionId,
                CourseId = attempt.CourseId, SectionId = attempt.SectionId, StudentId = attempt.StudentId,
                MaximumPoints = included.Sum(x => questions[x.PublishedQuestionId].Points)
            };
            db.ExamGradeReports.Add(report);
            foreach (var includedQuestion in included)
            {
                var q = questions[includedQuestion.PublishedQuestionId];
                var body = JsonSerializer.Deserialize<QuestionBody>(q.BodyJson)
                    ?? throw new InvalidOperationException("Invalid stored question snapshot.");
                var key = JsonSerializer.Deserialize<QuestionAnswerKey>(q.AnswerKeyJson)
                    ?? throw new InvalidOperationException("Invalid stored answer key.");
                var answer = answers.TryGetValue(q.Id, out var saved) ? saved.AnswerJson : null;
                var score = ExamGradingPolicy.AutoScore(body, key, answer, q.Points);
                var item = new ExamGradeItem
                {
                    ReportId = report.Id, AttemptId = attempt.Id,
                    PublishedQuestionId = q.Id, MaximumPoints = q.Points,
                    EarnedPoints = score, State = score is null ? "PendingManual" : "Auto"
                };
                db.ExamGradeItems.Add(item);
                report.EarnedPoints += score ?? 0;
            }
            db.ExamGradeAudits.Add(new ExamGradeAudit
            {
                ReportId = report.Id, ActorId = actor.Id, Action = "Initialized",
                Reason = "Auto-graded submitted attempt using immutable published question keys."
            });
            access.Audit(actor.Id, report.InstitutionId, "exam.grade.initialized", report.Id.ToString());
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return report.Id;
        });
        var created = await db.ExamGradeReports.AsNoTracking().SingleAsync(x => x.Id == reportId, ct);
        return await StaffDetail(created, ct);
    }

    public async Task<ExamStaffGradeView> GetStaff(ClaimsPrincipal principal, Guid attemptId,
        CancellationToken ct)
        => await StaffDetail(await StaffReport(principal, attemptId, false, ct), ct);

    public Task<ExamStaffGradeView> Grade(ClaimsPrincipal principal, Guid attemptId,
        Guid questionId, ExamManualGradeRequest request, CancellationToken ct)
        => retry.ExecuteAsync<ExamGradingService, ExamStaffGradeView>(service => service.GradeCore(principal, attemptId, questionId, request, ct), ct);

    private async Task<ExamStaffGradeView> GradeCore(ClaimsPrincipal principal, Guid attemptId,
        Guid questionId, ExamManualGradeRequest request, CancellationToken ct)
    {
        if (request is null) throw ApiFault.Bad("Grade request is required.");
        var visible = await StaffReport(principal, attemptId, false, ct);
        var actor = await Staff(principal, visible.SectionId, visible.Status == "Published", ct);
        var reason = request.Reason?.Trim() ?? "";
        var feedback = request.Feedback?.Trim() ?? "";
        if (reason.Length is < 5 or > 2000 || feedback.Length > 4000)
            throw ApiFault.Bad("Reason must be 5-2000 characters; feedback at most 4000.");
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await Academix.Api.Services.Academic.RegistrationCoordination.Lock(db,visible.InstitutionId,ct);
            var report = await LockReport(visible.Id, ct);
            // Re-check publication permissions after the row lock to prevent TA/publication races.
            await Staff(principal, report.SectionId, report.Status == "Published", ct);
            if (report.Version != request.ExpectedVersion) throw ApiFault.Conflict("Grade version changed.");
            var line = await db.ExamGradeItems.SingleOrDefaultAsync(x => x.ReportId == report.Id &&
                x.PublishedQuestionId == questionId, ct) ?? throw ApiFault.NotFound();
            var newScore = ExamGradingPolicy.ValidateScore(request.EarnedPoints, line.MaximumPoints);
            var previous = line.EarnedPoints;
            if(previous!=newScore)report.ScoreVersion++;
            line.EarnedPoints = newScore;
            line.State = "Manual";
            line.Feedback = feedback;
            line.GradedBy = actor.Id;
            line.GradedAtUtc = DateTime.UtcNow;
            report.EarnedPoints = await db.ExamGradeItems.Where(x => x.ReportId == report.Id && x.Id != line.Id)
                .SumAsync(x => x.EarnedPoints ?? 0, ct) + newScore;
            report.Version++;
            db.ExamGradeAudits.Add(new ExamGradeAudit
            {
                ReportId = report.Id, PublishedQuestionId = questionId, ActorId = actor.Id,
                Action = "ManualGrade", PreviousPoints = previous, NewPoints = newScore,
                Reason = reason
            });
            access.Audit(actor.Id, report.InstitutionId, "exam.grade.changed", report.Id.ToString());
            await db.SaveChangesAsync(ct);
            if(report.Status=="Published"&&previous!=newScore)await AssessmentFreshness.Changed(db,report.InstitutionId,report.SectionId,"Exam",report.ExamId,[report.StudentId],actor.Id,ct);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        return await GetStaff(principal, attemptId, ct);
    }

    public async Task<IReadOnlyList<ExamGradeAuditView>> History(ClaimsPrincipal principal,
        Guid attemptId, CancellationToken ct)
    {
        var report = await StaffReport(principal, attemptId, false, ct);
        var events = await db.ExamGradeAudits.AsNoTracking().Where(x => x.ReportId == report.Id)
            .OrderBy(x => x.AtUtc).ThenBy(x => x.Id).ToListAsync(ct);
        return events.Select(x => new ExamGradeAuditView(x.Id, x.PublishedQuestionId, x.ActorId,
            x.Action, x.PreviousPoints, x.NewPoints, x.Reason, x.AtUtc)).ToList();
    }

    public Task<ExamGradeSummary> Publish(ClaimsPrincipal principal, Guid attemptId,
        ExamPublishGradesRequest request, CancellationToken ct)
        => retry.ExecuteAsync<ExamGradingService, ExamGradeSummary>(service => service.PublishCore(principal, attemptId, request, ct), ct);

    private async Task<ExamGradeSummary> PublishCore(ClaimsPrincipal principal, Guid attemptId,
        ExamPublishGradesRequest request, CancellationToken ct)
    {
        if (request is null) throw ApiFault.Bad("Request is required.");
        var visible = await StaffReport(principal, attemptId, true, ct);
        var actor = await Staff(principal, visible.SectionId, true, ct);
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await Academix.Api.Services.Academic.RegistrationCoordination.Lock(db,visible.InstitutionId,ct);
            await Staff(principal,visible.SectionId,true,ct);
            var report = await LockReport(visible.Id, ct);
            if (report.Status == "Published" &&
                (report.Version == request.ExpectedVersion || report.Version - 1 == request.ExpectedVersion))
            {
                await tx.CommitAsync(ct);
                return Summary(report,0);
            }
            if (report.Version != request.ExpectedVersion) throw ApiFault.Conflict("Grade version changed.");
            var pending = await db.ExamGradeItems.AnyAsync(x => x.ReportId == report.Id && x.EarnedPoints == null, ct);
            if (pending) throw ApiFault.Conflict("Manual grading must be completed before publication.");
            report.Status = "Published";
            report.PublishedAtUtc = DateTime.UtcNow;
            report.Version++;
            db.ExamGradeAudits.Add(new ExamGradeAudit
            {
                ReportId = report.Id, ActorId = actor.Id, Action = "Published",
                Reason = "Grade report released to student."
            });
            access.Audit(actor.Id, report.InstitutionId, "exam.grade.published", report.Id.ToString());
            await db.SaveChangesAsync(ct);
            if(report.Status=="Published")await AssessmentFreshness.Changed(db,report.InstitutionId,report.SectionId,"Exam",report.ExamId,[report.StudentId],actor.Id,ct);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        var latest = await db.ExamGradeReports.AsNoTracking().SingleAsync(x => x.Id == visible.Id, ct);
        var pendingCount = await db.ExamGradeItems.CountAsync(x => x.ReportId == visible.Id && x.EarnedPoints == null, ct);
        return Summary(latest, pendingCount);
    }

    public async Task<IReadOnlyList<ExamGradeSummary>> MyResults(ClaimsPrincipal principal, Guid examId,
        CancellationToken ct)
    {
        var exam = await db.Exams.AsNoTracking().SingleOrDefaultAsync(x => x.Id == examId && x.Status == "Published", ct)
            ?? throw ApiFault.NotFound();
        var (actor, roles) = await access.CurrentAsync(principal, ct);
        if (actor.InstitutionId != exam.InstitutionId || !roles.Contains(AcademixRoles.Student))
            throw ApiFault.NotFound();
        var list = await db.ExamGradeReports.AsNoTracking().Where(x =>
            x.ExamId == examId && x.StudentId == actor.Id && x.Status == "Published")
            .OrderBy(x => x.PublishedAtUtc).ToListAsync(ct);
        return list.Select(x => Summary(x, 0)).ToList();
    }

    public async Task<ExamStudentResult> Result(ClaimsPrincipal principal, Guid attemptId,
        CancellationToken ct)
    {
        var (_, attempt) = await Owner(principal, attemptId, ct);
        var report = await db.ExamGradeReports.AsNoTracking().SingleOrDefaultAsync(x =>
            x.AttemptId == attempt.Id && x.Status == "Published", ct) ?? throw ApiFault.NotFound();
        var items = await db.ExamGradeItems.AsNoTracking().Where(x => x.ReportId == report.Id)
            .ToListAsync(ct);
        var ids = items.Select(x => x.PublishedQuestionId).ToArray();
        var published = await db.ExamPublishedQuestions.AsNoTracking().Where(x => ids.Contains(x.Id))
            .ToDictionaryAsync(x => x.Id, ct);
        var views = items.OrderBy(x => published[x.PublishedQuestionId].Position).Select(x =>
            new ExamStudentGradeItemView(x.PublishedQuestionId, published[x.PublishedQuestionId].Position,
                JsonSerializer.Deserialize<QuestionBody>(published[x.PublishedQuestionId].BodyJson)!.Kind,
                x.MaximumPoints, x.EarnedPoints ?? 0m, x.State, x.Feedback)).ToList();
        var appeal = await db.ExamGradeAppeals.AsNoTracking().SingleOrDefaultAsync(x => x.ReportId == report.Id, ct);
        return new ExamStudentResult(Summary(report, 0), views, appeal is null ? null : ToAppeal(appeal));
    }

    private static ExamAppealView ToAppeal(ExamGradeAppeal a)
        => new(a.Id, a.ReportId, a.Status, a.Message, a.Resolution, a.CreatedAtUtc, a.ResolvedAtUtc);

    public Task<ExamAppealView> Appeal(ClaimsPrincipal principal, Guid attemptId,
        ExamAppealRequest request, CancellationToken ct)
        => retry.ExecuteAsync<ExamGradingService, ExamAppealView>(service => service.AppealCore(principal, attemptId, request, ct), ct);

    private async Task<ExamAppealView> AppealCore(ClaimsPrincipal principal, Guid attemptId,
        ExamAppealRequest request, CancellationToken ct)
    {
        if (request is null) throw ApiFault.Bad("Appeal request is required.");
        var (actor, _) = await Owner(principal, attemptId, ct);
        var message = request.Message?.Trim() ?? "";
        if (message.Length is < 10 or > 2000) throw ApiFault.Bad("Appeal must be 10-2000 characters.");
        var strategy = db.Database.CreateExecutionStrategy();
        var id = await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var published = await db.ExamGradeReports.AsNoTracking().SingleOrDefaultAsync(x =>
                x.AttemptId == attemptId && x.StudentId == actor.Id && x.Status == "Published", ct)
                ?? throw ApiFault.NotFound();
            var report = await LockReport(published.Id, ct);
            if (report.Status != "Published" || report.PublishedAtUtc is null ||
                DateTime.UtcNow > report.PublishedAtUtc.Value.AddDays(14))
                throw ApiFault.Conflict("Appeal window is closed.");
            if (await db.ExamGradeAppeals.AnyAsync(x => x.ReportId == report.Id, ct))
                throw ApiFault.Conflict("This result already has an appeal.");
            var a = new ExamGradeAppeal { ReportId = report.Id, StudentId = actor.Id, Message = message };
            db.ExamGradeAppeals.Add(a);
            access.Audit(actor.Id, report.InstitutionId, "exam.grade.appealed", report.Id.ToString());
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return a.Id;
        });
        return ToAppeal(await db.ExamGradeAppeals.AsNoTracking().SingleAsync(x => x.Id == id, ct));
    }

    public async Task<IReadOnlyList<ExamAppealView>> Appeals(ClaimsPrincipal principal, Guid examId,
        CancellationToken ct)
    {
        var exam = await db.Exams.AsNoTracking().SingleOrDefaultAsync(x => x.Id == examId, ct)
            ?? throw ApiFault.NotFound();
        await Staff(principal, exam.SectionId, false, ct);
        var ids = await db.ExamGradeReports.AsNoTracking().Where(x => x.ExamId == exam.Id)
            .Select(x => x.Id).ToListAsync(ct);
        var list = await db.ExamGradeAppeals.AsNoTracking().Where(x => ids.Contains(x.ReportId))
            .OrderByDescending(x => x.CreatedAtUtc).Take(200).ToListAsync(ct);
        return list.Select(ToAppeal).ToList();
    }

    public Task<ExamAppealView> Resolve(ClaimsPrincipal principal, Guid appealId,
        ExamResolveAppealRequest request, CancellationToken ct)
        => retry.ExecuteAsync<ExamGradingService, ExamAppealView>(service => service.ResolveCore(principal, appealId, request, ct), ct);

    private async Task<ExamAppealView> ResolveCore(ClaimsPrincipal principal, Guid appealId,
        ExamResolveAppealRequest request, CancellationToken ct)
    {
        if (request is null) throw ApiFault.Bad("Resolution request is required.");
        var visible = await db.ExamGradeAppeals.AsNoTracking().SingleOrDefaultAsync(x => x.Id == appealId, ct)
            ?? throw ApiFault.NotFound();
        var reportVisible = await db.ExamGradeReports.AsNoTracking().SingleAsync(x => x.Id == visible.ReportId, ct);
        var actor = await Staff(principal, reportVisible.SectionId, true, ct);
        var resolution = request.Resolution?.Trim() ?? "";
        if (resolution.Length is < 10 or > 2000) throw ApiFault.Bad("Resolution must be 10-2000 characters.");
        if (request.PublishedQuestionId.HasValue != request.CorrectedPoints.HasValue ||
            !request.Accept && request.PublishedQuestionId.HasValue)
            throw ApiFault.Bad("Corrected question and score must both be present, and require acceptance.");
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await Academix.Api.Services.Academic.RegistrationCoordination.Lock(db,reportVisible.InstitutionId,ct);
            await Staff(principal,reportVisible.SectionId,true,ct);
            var report = await LockReport(reportVisible.Id, ct);
            if (report.Version != request.ExpectedVersion) throw ApiFault.Conflict("Grade version changed.");
            if (report.Status != "Published") throw ApiFault.Conflict("Result has not been published.");
            var appeal = await db.ExamGradeAppeals.SingleAsync(x => x.Id == appealId && x.ReportId == report.Id, ct);
            if (appeal.Status != "Open") throw ApiFault.Conflict("Appeal already resolved.");
            var scoreChanged=false;
            if (request.PublishedQuestionId is Guid questionId && request.CorrectedPoints is decimal corrected)
            {
                var item = await db.ExamGradeItems.SingleOrDefaultAsync(x =>
                    x.ReportId == report.Id && x.PublishedQuestionId == questionId, ct)
                    ?? throw ApiFault.NotFound();
                corrected = ExamGradingPolicy.ValidateScore(corrected, item.MaximumPoints);
                var previous = item.EarnedPoints;
                scoreChanged=previous!=corrected;if(scoreChanged)report.ScoreVersion++;
                item.EarnedPoints = corrected;
                item.State = "Manual";
                item.GradedBy = actor.Id;
                item.GradedAtUtc = DateTime.UtcNow;
                item.Feedback = resolution;
                report.EarnedPoints = await db.ExamGradeItems.Where(x => x.ReportId == report.Id && x.Id != item.Id)
                    .SumAsync(x => x.EarnedPoints ?? 0, ct) + corrected;
                db.ExamGradeAudits.Add(new ExamGradeAudit
                {
                    ReportId = report.Id, PublishedQuestionId = questionId, ActorId = actor.Id,
                    Action = "AppealCorrection", PreviousPoints = previous,
                    NewPoints = corrected, Reason = resolution
                });
            }
            appeal.Status = request.Accept ? "Accepted" : "Rejected";
            appeal.Resolution = resolution;
            appeal.ResolvedBy = actor.Id;
            appeal.ResolvedAtUtc = DateTime.UtcNow;
            report.Version++;
            db.ExamGradeAudits.Add(new ExamGradeAudit
            {
                ReportId = report.Id, ActorId = actor.Id,
                Action = appeal.Status == "Accepted" ? "AppealAccepted" : "AppealRejected",
                Reason = resolution
            });
            access.Audit(actor.Id, report.InstitutionId, "exam.appeal.resolved", appeal.Id.ToString());
            await db.SaveChangesAsync(ct);
            if(scoreChanged)await AssessmentFreshness.Changed(db,report.InstitutionId,report.SectionId,"Exam",report.ExamId,[report.StudentId],actor.Id,ct);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }
        return ToAppeal(await db.ExamGradeAppeals.AsNoTracking().SingleAsync(x => x.Id == appealId, ct));
    }
}
