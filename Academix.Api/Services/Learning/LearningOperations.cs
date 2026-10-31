using System.Security.Claims;
using Academix.Api.Data;
using Academix.Api.Services.Infrastructure;
using Academix.Api.Domain.Academic;
using Academix.Api.Domain.Identity;
using Academix.Api.Domain.Learning;
using Academix.Api.DTOs.Learning;
using Academix.Api.Middleware;
using Academix.Api.Services.Identity;
using Academix.Api.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace Academix.Api.Services.Learning;

public sealed class LearningOperations(AcademixDbContext db, IdentitySecurity access, IPrivateFileStore files, RetryingServiceWork retry, LearningArtifactCleanup artifactCleanup)
{
    private static string Valid(string? value, int length, string name)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > length) throw ApiFault.Bad($"Invalid {name}.");
        return value.Trim();
    }
    private static void Position(int value)
    {
        if (value < 1 || value > 10000) throw ApiFault.Bad("Position must be 1-10000.");
    }
    private async Task<CourseSection> Section(Guid id, CancellationToken ct)
        => await db.CourseSections.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw ApiFault.NotFound();

    private async Task Staff(ClaimsPrincipal principal, CourseSection section, CancellationToken ct)
    {
        _ = await access.RequireSectionStaffAsync(principal, section.InstitutionId, section.Id, ct);
    }
    private async Task LockAssignment(Guid assignmentId, CancellationToken ct)
    {
        var transaction = db.Database.CurrentTransaction ?? throw new InvalidOperationException("Expected active transaction.");
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.Transaction = transaction.GetDbTransaction();
        command.CommandText = "SELECT \"Id\" FROM lms_assignments WHERE \"Id\" = @id FOR UPDATE";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "id";
        parameter.Value = assignmentId;
        command.Parameters.Add(parameter);
        var found = await command.ExecuteScalarAsync(ct);
        if (found is null) throw ApiFault.NotFound();
    }

    private async Task<(Guid UserId, bool IsStaff)> Viewer(ClaimsPrincipal principal, CourseSection section, CancellationToken ct)
    {
        var (user, roles) = await access.CurrentAsync(principal, ct);
        if (roles.Contains(AcademixRoles.SuperAdministrator) ||
            (roles.Contains(AcademixRoles.InstitutionAdministrator) && user.InstitutionId == section.InstitutionId) ||
            await access.IsCurrentSectionTeacherAsync(user, roles, section.InstitutionId, section.Id, ct))
            return (user.Id, true);
        if (user.InstitutionId == section.InstitutionId && section.Status == "Open" &&
            roles.Contains(AcademixRoles.Student) &&
            await db.StudentEnrollments.AnyAsync(x => x.SectionId == section.Id && x.StudentId == user.Id && x.Status == "Active", ct))
            return (user.Id, false);
        throw ApiFault.Forbidden();
    }
    private async Task<Guid> Student(ClaimsPrincipal principal, CourseSection section, CancellationToken ct)
    {
        var (user, roles) = await access.CurrentAsync(principal, ct);
        if (user.InstitutionId != section.InstitutionId || section.Status != "Open" || !roles.Contains(AcademixRoles.Student) ||
            !await db.StudentEnrollments.AnyAsync(x => x.SectionId == section.Id && x.StudentId == user.Id && x.Status == "Active", ct))
            throw ApiFault.Forbidden();
        return user.Id;
    }

    public async Task<object> AddUnit(ClaimsPrincipal principal, Guid sectionId, UnitCreate request, CancellationToken ct)
    {
        var sec = await Section(sectionId, ct); await Staff(principal, sec, ct);
        Position(request.Position);
        var unit = new LearningUnit { InstitutionId = sec.InstitutionId, SectionId = sectionId, Title = Valid(request.Title, 200, "unit title"), Position = request.Position };
        db.LearningUnits.Add(unit); await db.SaveChangesAsync(ct); return unit;
    }
    public async Task<object> PublishUnit(ClaimsPrincipal principal, Guid unitId, UnitPublish request, CancellationToken ct)
    {
        var unit = await db.LearningUnits.SingleOrDefaultAsync(x => x.Id == unitId, ct) ?? throw ApiFault.NotFound();
        await Staff(principal, await Section(unit.SectionId, ct), ct);
        if (unit.Version != request.ExpectedVersion) throw ApiFault.Conflict("Unit modified by another request.");
        unit.IsPublished = request.IsPublished; unit.Version++; await db.SaveChangesAsync(ct); return unit;
    }
    public async Task<object> AddLesson(ClaimsPrincipal principal, Guid sectionId, LessonCreate request, CancellationToken ct)
    {
        var sec = await Section(sectionId, ct); await Staff(principal, sec, ct); Position(request.Position);
        if (!await db.LearningUnits.AnyAsync(x => x.Id == request.UnitId && x.SectionId == sectionId, ct)) throw ApiFault.NotFound();
        var lesson = new LearningLesson { InstitutionId = sec.InstitutionId, SectionId = sectionId, UnitId = request.UnitId,
            Title = Valid(request.Title, 200, "lesson title"), Body = Valid(request.Body, 20000, "lesson body"), Position = request.Position };
        db.LearningLessons.Add(lesson); await db.SaveChangesAsync(ct); return lesson;
    }
    public async Task<object> PublishLesson(ClaimsPrincipal principal, Guid lessonId, LessonPublish request, CancellationToken ct)
    {
        var lesson = await db.LearningLessons.SingleOrDefaultAsync(x => x.Id == lessonId, ct) ?? throw ApiFault.NotFound();
        await Staff(principal, await Section(lesson.SectionId, ct), ct);
        if (lesson.Version != request.ExpectedVersion) throw ApiFault.Conflict("Lesson modified by another request.");
        lesson.IsPublished = request.IsPublished; lesson.Version++; await db.SaveChangesAsync(ct); return lesson;
    }
    public async Task<object> Catalog(ClaimsPrincipal principal, Guid sectionId, CancellationToken ct)
    {
        var sec = await Section(sectionId, ct); var (_, staff) = await Viewer(principal, sec, ct);
        var units = await db.LearningUnits.AsNoTracking().Where(x => x.SectionId == sectionId && (staff || x.IsPublished))
            .OrderBy(x => x.Position).ToListAsync(ct);
        var lessons = await db.LearningLessons.AsNoTracking().Where(x => x.SectionId == sectionId && (staff || x.IsPublished))
            .OrderBy(x => x.Position).ToListAsync(ct);
        var visibleUnitIds = units.Select(x => x.Id).ToHashSet();
        return new { units, lessons = lessons.Where(x => visibleUnitIds.Contains(x.UnitId)).ToList() };
    }
    public async Task<object> Announce(ClaimsPrincipal principal, Guid sectionId, AnnouncementCreate request, CancellationToken ct)
    {
        var sec = await Section(sectionId, ct); await Staff(principal, sec, ct);
        var item = new LearningAnnouncement { InstitutionId = sec.InstitutionId, SectionId = sectionId,
            Title = Valid(request.Title, 200, "announcement title"), Body = Valid(request.Body, 10000, "announcement body") };
        db.LearningAnnouncements.Add(item); await db.SaveChangesAsync(ct); return item;
    }
    public async Task<object> Announcements(ClaimsPrincipal principal, Guid sectionId, CancellationToken ct)
    {
        var sec = await Section(sectionId, ct); await Viewer(principal, sec, ct);
        return await db.LearningAnnouncements.AsNoTracking().Where(x => x.SectionId == sectionId)
            .OrderByDescending(x => x.PublishedAtUtc).Take(100).ToListAsync(ct);
    }
    public async Task<object> AddAssignment(ClaimsPrincipal principal, Guid sectionId, AssignmentCreate request, CancellationToken ct)
    {
        var sec = await Section(sectionId, ct); await Staff(principal, sec, ct);
        if (request.DueAtUtc.Kind != DateTimeKind.Utc || request.DueAtUtc <= DateTime.UtcNow || request.MaxScore <= 0 || request.MaxScore > 10000)
            throw ApiFault.Bad("Due date must be future UTC and max score must be 0-10000.");
        var item = new LearningAssignment { InstitutionId = sec.InstitutionId, SectionId = sectionId,
            Title = Valid(request.Title, 200, "assignment title"), Instructions = Valid(request.Instructions, 20000, "instructions"),
            DueAtUtc = request.DueAtUtc, MaxScore = request.MaxScore };
        db.LearningAssignments.Add(item); await db.SaveChangesAsync(ct); return item;
    }
    public async Task<object> PublishAssignment(ClaimsPrincipal principal, Guid id, AssignmentPublish request, CancellationToken ct)
    {
        var item = await db.LearningAssignments.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw ApiFault.NotFound();
        await Staff(principal, await Section(item.SectionId, ct), ct);
        if (item.Version != request.ExpectedVersion) throw ApiFault.Conflict("Assignment version changed.");
        item.IsPublished = request.IsPublished; item.Version++; await db.SaveChangesAsync(ct); return item;
    }
    public async Task<object> Assignments(ClaimsPrincipal principal, Guid sectionId, CancellationToken ct)
    {
        var sec = await Section(sectionId, ct); var (_, staff) = await Viewer(principal, sec, ct);
        return await db.LearningAssignments.AsNoTracking().Where(x => x.SectionId == sectionId && (staff || x.IsPublished))
            .OrderBy(x => x.DueAtUtc).ToListAsync(ct);
    }
    public Task<object> SaveDraft(ClaimsPrincipal principal, Guid assignmentId, SubmissionDraft request, CancellationToken ct)
        => retry.ExecuteAsync<LearningOperations, object>(service => service.SaveDraftCore(principal, assignmentId, request, ct), ct);

    private async Task<object> SaveDraftCore(ClaimsPrincipal principal, Guid assignmentId, SubmissionDraft request, CancellationToken ct)
    {
        var assignment = await db.LearningAssignments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == assignmentId, ct) ?? throw ApiFault.NotFound();
        var studentId = await Student(principal, await Section(assignment.SectionId, ct), ct);
        var content = request.Content?.Trim() ?? "";
        if (content.Length > 20000 || (content.Length == 0 && request.AttachmentId is null))
            throw ApiFault.Bad("Submission needs text or an attachment (text max 20000 characters).");
        if (request.AttachmentId is Guid attachment &&
            !await db.LearningFiles.AnyAsync(x => x.Id == attachment && x.SectionId == assignment.SectionId &&
                x.UploadedBy == studentId && x.IsStudentUpload, ct))
            throw ApiFault.Bad("Attachment must belong to this student and section.");
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        // The deadline and publication state must be checked AFTER the row lock.
        // A request waiting for this lock cannot rely on an earlier timestamp/snapshot.
        await LockAssignment(assignmentId, ct);
        var currentAssignment = await db.LearningAssignments.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == assignmentId, ct) ?? throw ApiFault.NotFound();
        if (!currentAssignment.IsPublished || currentAssignment.DueAtUtc <= DateTime.UtcNow)
            throw ApiFault.Conflict("Assignment is closed.");
        var submission = await db.LearningSubmissions.SingleOrDefaultAsync(x => x.AssignmentId == assignmentId && x.StudentId == studentId, ct);
        if (submission is null)
        {
            submission = new LearningSubmission { InstitutionId = assignment.InstitutionId, SectionId = assignment.SectionId,
                AssignmentId = assignmentId, StudentId = studentId, Content = content, AttachmentId = request.AttachmentId };
            db.LearningSubmissions.Add(submission);
        }
        else
        {
            if (submission.Status != "Draft") throw ApiFault.Conflict("Submitted work cannot be changed.");
            submission.Content = content; submission.AttachmentId = request.AttachmentId; submission.Revision++;
        }
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return submission;
    }
    public Task<object> Submit(ClaimsPrincipal principal, Guid assignmentId, CancellationToken ct)
        => retry.ExecuteAsync<LearningOperations, object>(service => service.SubmitCore(principal, assignmentId, ct), ct);

    private async Task<object> SubmitCore(ClaimsPrincipal principal, Guid assignmentId, CancellationToken ct)
    {
        var assignment = await db.LearningAssignments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == assignmentId, ct) ?? throw ApiFault.NotFound();
        var studentId = await Student(principal, await Section(assignment.SectionId, ct), ct);
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await LockAssignment(assignmentId, ct);
        var submission = await db.LearningSubmissions.SingleOrDefaultAsync(x => x.AssignmentId == assignmentId && x.StudentId == studentId, ct)
            ?? throw ApiFault.Conflict("Save a draft before submitting.");
        if (submission.Status is "Submitted" or "Graded") return submission; // Idempotent retry.
        // A pending submission must use the publication state and deadline observed
        // after acquiring the assignment lock, not the pre-transaction snapshot.
        var currentAssignment = await db.LearningAssignments.AsNoTracking()
            .SingleOrDefaultAsync(x => x.Id == assignmentId, ct) ?? throw ApiFault.NotFound();
        if (!currentAssignment.IsPublished || currentAssignment.DueAtUtc <= DateTime.UtcNow)
            throw ApiFault.Conflict("Submission deadline passed.");
        submission.Status = "Submitted"; submission.SubmittedAtUtc = DateTime.UtcNow;
        await db.SaveChangesAsync(ct); await transaction.CommitAsync(ct); return submission;
    }
    public Task<object> Grade(ClaimsPrincipal principal, Guid submissionId, GradeSubmission request, CancellationToken ct)
        =>retry.ExecuteAsync<LearningOperations,object>(s=>s.GradeCore(principal,submissionId,request,ct),ct);
    private async Task<object> GradeCore(ClaimsPrincipal principal, Guid submissionId, GradeSubmission request, CancellationToken ct)
    {
        var initial=await db.LearningSubmissions.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==submissionId,ct)??throw ApiFault.NotFound();
        await using var tx=await db.Database.BeginTransactionAsync(ct);
        await Academix.Api.Services.Academic.RegistrationCoordination.Lock(db,initial.InstitutionId,ct);
        var submission = await db.LearningSubmissions.SingleOrDefaultAsync(x => x.Id == submissionId, ct) ?? throw ApiFault.NotFound();
        var section = await Section(submission.SectionId, ct);
        await Staff(principal, section, ct);
        var assignment = await db.LearningAssignments.AsNoTracking().SingleAsync(x => x.Id == submission.AssignmentId, ct);
        if (submission.Status is not ("Submitted" or "Graded")) throw ApiFault.Conflict("Draft cannot be graded.");
        if (request.Score < 0 || request.Score > assignment.MaxScore) throw ApiFault.Bad("Score exceeds assignment maximum.");
        var (actor, _) = await access.CurrentAsync(principal, ct);
        submission.Score = request.Score; submission.Feedback = request.Feedback?.Trim() is { Length: > 5000 } ? throw ApiFault.Bad("Feedback too long.") : request.Feedback?.Trim();
        submission.Status = "Graded"; submission.GradedAtUtc = DateTime.UtcNow; submission.GradedBy = actor.Id;
        await db.SaveChangesAsync(ct);
        await Academix.Api.Services.Infrastructure.AssessmentFreshness.Changed(db,submission.InstitutionId,submission.SectionId,"Assignment",submission.AssignmentId,[submission.StudentId],actor.Id,ct);
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct); return submission;
    }
    public async Task<object> MySubmission(ClaimsPrincipal principal, Guid assignmentId, CancellationToken ct)
    {
        var assignment = await db.LearningAssignments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == assignmentId, ct) ?? throw ApiFault.NotFound();
        var student = await Student(principal, await Section(assignment.SectionId, ct), ct);
        return (object?)await db.LearningSubmissions.AsNoTracking().SingleOrDefaultAsync(x => x.AssignmentId == assignmentId && x.StudentId == student, ct)
            ?? new { status = "NotStarted" };
    }
    public async Task<object> Roster(ClaimsPrincipal principal, Guid assignmentId, CancellationToken ct)
    {
        var assignment = await db.LearningAssignments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == assignmentId, ct) ?? throw ApiFault.NotFound();
        await Staff(principal, await Section(assignment.SectionId, ct), ct);
        return await db.LearningSubmissions.AsNoTracking().Where(x => x.AssignmentId == assignmentId)
            .OrderBy(x => x.StudentId).Take(500).ToListAsync(ct);
    }
    public async Task<object> Complete(ClaimsPrincipal principal, Guid lessonId, CancellationToken ct)
    {
        var lesson = await db.LearningLessons.AsNoTracking().SingleOrDefaultAsync(x => x.Id == lessonId, ct) ?? throw ApiFault.NotFound();
        var student = await Student(principal, await Section(lesson.SectionId, ct), ct);
        if (!lesson.IsPublished || !await db.LearningUnits.AnyAsync(x => x.Id == lesson.UnitId && x.IsPublished, ct)) throw ApiFault.NotFound();
        var existing = await db.LessonCompletions.SingleOrDefaultAsync(x => x.LessonId == lessonId && x.StudentId == student, ct);
        if (existing is not null) return existing;
        var item = new LessonCompletion { InstitutionId = lesson.InstitutionId, SectionId = lesson.SectionId, LessonId = lessonId, StudentId = student };
        db.LessonCompletions.Add(item);
        try { await db.SaveChangesAsync(ct); } catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
        { db.Entry(item).State = EntityState.Detached; } // Duplicate completion from a concurrent retry.
        return (object?)await db.LessonCompletions.AsNoTracking().SingleOrDefaultAsync(x => x.LessonId == lessonId && x.StudentId == student, ct) ?? item;
    }
    public async Task<object> Progress(ClaimsPrincipal principal, Guid sectionId, CancellationToken ct)
    {
        var section = await Section(sectionId, ct);
        var student = await Student(principal, section, ct);
        var visible = await (from l in db.LearningLessons.AsNoTracking() join u in db.LearningUnits on l.UnitId equals u.Id
            where l.SectionId == sectionId && l.IsPublished && u.IsPublished select l.Id).ToListAsync(ct);
        var completed = await db.LessonCompletions.CountAsync(x => x.SectionId == sectionId && x.StudentId == student && visible.Contains(x.LessonId), ct);
        return new { completed, total = visible.Count, percent = visible.Count == 0 ? 0 : Math.Round((decimal)completed * 100 / visible.Count, 2) };
    }
    public Task<object> Upload(ClaimsPrincipal principal, Guid sectionId, IFormFile file, CancellationToken ct)
    {
        var fileId=Guid.NewGuid();
        return retry.ExecuteAsync<LearningOperations,object>(s=>s.UploadCore(principal,sectionId,fileId,file,ct),ct);
    }
    private async Task<object> UploadCore(ClaimsPrincipal principal, Guid sectionId,Guid fileId,IFormFile file,CancellationToken ct)
    {
        if(file is null)throw ApiFault.Bad("A file is required.");
        var sec = await Section(sectionId, ct);
        await using var tx=await db.Database.BeginTransactionAsync(ct);
        // Permit the independent intent's foreign-key KEY SHARE while retaining
        // serialization with institution FOR UPDATE writers and cleanup.
        await db.Institutions.FromSqlInterpolated($"SELECT * FROM institutions WHERE \"Id\"={sec.InstitutionId} FOR NO KEY UPDATE").SingleAsync(ct);
        var (actorId, staffUpload) = await Viewer(principal, sec, ct);
        var previous=await db.LearningFiles.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==fileId&&x.InstitutionId==sec.InstitutionId&&x.SectionId==sectionId&&x.UploadedBy==actorId,ct);
        if(previous is not null){await tx.CommitAsync(ct);return new{previous.Id,previous.SectionId,previous.FileName,previous.ContentType,previous.Size};}
        if (file.Length < 1 || file.Length > 20 * 1024 * 1024) throw ApiFault.Bad("Files must be 1 byte to 20 MB.");
        var allowed = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "application/pdf", "image/png", "image/jpeg", "text/plain" };
        if (!allowed.Contains(file.ContentType)) throw ApiFault.Bad("File content type is not allowed.");
        // A multipart Content-Type header is user-controlled. Validate the file bytes
        // before storing the object; this does not replace malware scanning.
        await using (var inspection = file.OpenReadStream())
            await LearningFileContentPolicy.ValidateAsync(inspection, file.ContentType, ct);
        var name = Path.GetFileName(file.FileName);
        if (string.IsNullOrWhiteSpace(name) || name.Length > 200 || name.Any(char.IsControl)) throw ApiFault.Bad("Invalid file name.");
        var item = new LearningFile { Id=fileId, InstitutionId = sec.InstitutionId, SectionId = sectionId,
            UploadedBy = actorId, IsStudentUpload = !staffUpload, FileName = name, ContentType = file.ContentType, Size = file.Length };
        item.ObjectKey=LearningArtifactCleanup.Key(sec.InstitutionId,sectionId,item.Id);
        await artifactCleanup.Schedule(sec.InstitutionId,sectionId,item.Id,ct);
        await using (var input = file.OpenReadStream()) await files.PutAsync(item.ObjectKey, input, item.Size, item.ContentType, ct);
        db.LearningFiles.Add(item);await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);
        return new { item.Id, item.SectionId, item.FileName, item.ContentType, item.Size };
    }
    public async Task<(byte[] Bytes, string Mime, string Name)> Download(ClaimsPrincipal principal, Guid fileId, CancellationToken ct)
    {
        var file = await db.LearningFiles.AsNoTracking().SingleOrDefaultAsync(x => x.Id == fileId, ct) ?? throw ApiFault.NotFound();
        var (viewer, staff) = await Viewer(principal, await Section(file.SectionId, ct), ct);
        if (file.IsStudentUpload && !staff && file.UploadedBy != viewer) throw ApiFault.Forbidden();
        return (await files.GetAsync(file.ObjectKey, ct), file.ContentType, file.FileName);
    }
}
