using System.Security.Claims;
using Academix.Api.Data;
using Academix.Api.Domain.Identity;
using Academix.Api.DTOs.Workspaces;
using Academix.Api.Middleware;
using Academix.Api.Services.Identity;
using Microsoft.EntityFrameworkCore;

namespace Academix.Api.Services.Workspaces;

public sealed class StudentWorkspaceService(AcademixDbContext db, IdentitySecurity security)
{
    private async Task<(Guid UserId, Guid InstitutionId, string Email)> Student(ClaimsPrincipal principal, CancellationToken ct)
    {
        var (user, roles) = await security.CurrentAsync(principal, ct);
        if (!roles.Contains(AcademixRoles.Student) || user.InstitutionId is not Guid institutionId)
            throw ApiFault.Forbidden();
        return (user.Id, institutionId, user.Email ?? "");
    }

    private async Task<(Guid UserId, Guid InstitutionId)> EnrolledIn(ClaimsPrincipal principal,
        Guid sectionId, CancellationToken ct)
    {
        var (userId, institutionId, _) = await Student(principal, ct);
        if (!await db.StudentEnrollments.AsNoTracking().AnyAsync(x =>
                x.StudentId == userId && x.InstitutionId == institutionId &&
                x.SectionId == sectionId && x.Status == "Active", ct))
            throw ApiFault.NotFound();
        return (userId, institutionId);
    }

    public async Task<StudentOverview> Overview(ClaimsPrincipal principal, CancellationToken ct)
    {
        var (userId, institutionId, email) = await Student(principal, ct);
        var sections = db.StudentEnrollments.AsNoTracking().Where(e =>
            e.StudentId == userId && e.InstitutionId == institutionId && e.Status == "Active")
            .Select(e => e.SectionId);
        var lessons = from lesson in db.LearningLessons.AsNoTracking()
                      join unit in db.LearningUnits.AsNoTracking() on lesson.UnitId equals unit.Id
                      where lesson.InstitutionId == institutionId && unit.InstitutionId == institutionId &&
                            lesson.SectionId == unit.SectionId && lesson.IsPublished && unit.IsPublished &&
                            sections.Contains(lesson.SectionId)
                      select lesson.Id;
        var now = DateTime.UtcNow;
        var assignments = db.LearningAssignments.AsNoTracking().Where(x =>
            x.InstitutionId == institutionId && sections.Contains(x.SectionId) && x.IsPublished);
        return new StudentOverview(userId, email, institutionId,
            await sections.CountAsync(ct),
            await lessons.CountAsync(ct),
            await db.LessonCompletions.AsNoTracking().CountAsync(x => x.StudentId == userId &&
                x.InstitutionId == institutionId && lessons.Contains(x.LessonId), ct),
            await assignments.CountAsync(x => x.DueAtUtc >= now, ct),
            await db.LearningSubmissions.AsNoTracking().CountAsync(x => x.StudentId == userId &&
                x.InstitutionId == institutionId && sections.Contains(x.SectionId) &&
                (x.Status == "Submitted" || x.Status == "Graded"), ct),
            await db.ExamGradeReports.AsNoTracking().CountAsync(x => x.StudentId == userId &&
                x.InstitutionId == institutionId && x.Status == "Published", ct));
    }

    public async Task<PageResult<StudentCourse>> Courses(ClaimsPrincipal principal, int page, CancellationToken ct)
    {
        var offset = WorkspacePolicy.Offset(page);
        var (userId, institutionId, _) = await Student(principal, ct);
        var query = from enrollment in db.StudentEnrollments.AsNoTracking()
                    join section in db.CourseSections.AsNoTracking() on enrollment.SectionId equals section.Id
                    join course in db.Courses.AsNoTracking() on section.CourseId equals course.Id
                    join term in db.AcademicTerms.AsNoTracking() on section.TermId equals term.Id
                    where enrollment.StudentId == userId && enrollment.InstitutionId == institutionId &&
                          section.InstitutionId == institutionId && course.InstitutionId == institutionId &&
                          term.InstitutionId == institutionId && enrollment.Status == "Active"
                    select new { Enrollment = enrollment, Section = section, Course = course, Term = term };
        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(x => x.Course.Code).ThenBy(x => x.Section.Code)
            .ThenBy(x => x.Section.Id).Skip(offset).Take(WorkspacePolicy.PageSize)
            .Select(x => new StudentCourse(x.Section.Id, x.Course.Id, x.Course.Code, x.Course.Title,
                x.Section.Code, x.Term.Id, x.Term.Name, x.Section.Status, x.Enrollment.EnrolledAtUtc))
            .ToListAsync(ct);
        return new PageResult<StudentCourse>(page, WorkspacePolicy.PageSize, total, items);
    }

    public async Task<StudentCourseProgress> Progress(ClaimsPrincipal principal, Guid sectionId, CancellationToken ct)
    {
        var (userId, institutionId) = await EnrolledIn(principal, sectionId, ct);
        var publishedLessons = from lesson in db.LearningLessons.AsNoTracking()
                               join unit in db.LearningUnits.AsNoTracking() on lesson.UnitId equals unit.Id
                               where lesson.SectionId == sectionId && lesson.InstitutionId == institutionId &&
                                     unit.SectionId == sectionId && unit.InstitutionId == institutionId &&
                                     lesson.IsPublished && unit.IsPublished
                               select lesson.Id;
        var count = await publishedLessons.CountAsync(ct);
        var completed = await db.LessonCompletions.AsNoTracking().CountAsync(x =>
            x.StudentId == userId && x.InstitutionId == institutionId &&
            x.SectionId == sectionId && publishedLessons.Contains(x.LessonId), ct);
        var assignments = await (from task in db.LearningAssignments.AsNoTracking()
                                 where task.SectionId == sectionId && task.InstitutionId == institutionId && task.IsPublished
                                 join submission in db.LearningSubmissions.AsNoTracking()
                                    .Where(s => s.StudentId == userId && s.InstitutionId == institutionId && s.SectionId == sectionId)
                                    on task.Id equals submission.AssignmentId into submissions
                                 from submission in submissions.DefaultIfEmpty()
                                 orderby task.DueAtUtc, task.Id
                                 select new StudentAssignment(task.Id, task.Title, task.DueAtUtc, task.MaxScore,
                                     submission == null ? null : submission.Status,
                                     submission == null ? null : submission.Score)).ToListAsync(ct);
        var results = await (from grade in db.ExamGradeReports.AsNoTracking()
                             join exam in db.Exams.AsNoTracking() on grade.ExamId equals exam.Id
                             where grade.StudentId == userId && grade.InstitutionId == institutionId &&
                                   grade.SectionId == sectionId && exam.InstitutionId == institutionId &&
                                   exam.SectionId == sectionId && grade.Status == "Published"
                             orderby grade.PublishedAtUtc descending, grade.Id
                             select new StudentExamResult(exam.Id, exam.Title, grade.AttemptId,
                                 grade.EarnedPoints, grade.MaximumPoints, grade.PublishedAtUtc)).ToListAsync(ct);
        return new StudentCourseProgress(sectionId, count, completed, assignments, results);
    }

    public async Task<IReadOnlyList<StudentCalendarItem>> Calendar(ClaimsPrincipal principal, int days, CancellationToken ct)
    {
        var (userId, institutionId, _) = await Student(principal, ct);
        var now = DateTime.UtcNow;
        var end = WorkspacePolicy.CalendarEnd(now, days);
        var sections = db.StudentEnrollments.AsNoTracking().Where(x =>
            x.StudentId == userId && x.InstitutionId == institutionId && x.Status == "Active")
            .Select(x => x.SectionId);
        var assignments = await db.LearningAssignments.AsNoTracking()
            .Where(x => x.InstitutionId == institutionId && sections.Contains(x.SectionId) &&
                x.IsPublished && x.DueAtUtc >= now && x.DueAtUtc < end)
            .OrderBy(x => x.DueAtUtc).ThenBy(x => x.Id).Take(100)
            .Select(x => new StudentCalendarItem("AssignmentDue", x.Id, x.SectionId, x.Title, x.DueAtUtc))
            .ToListAsync(ct);
        var exams = await db.Exams.AsNoTracking()
            .Where(x => x.InstitutionId == institutionId && sections.Contains(x.SectionId) &&
                x.Status == "Published" && x.OpensAtUtc >= now && x.OpensAtUtc < end)
            .OrderBy(x => x.OpensAtUtc).ThenBy(x => x.Id).Take(100)
            .Select(x => new StudentCalendarItem("ExamOpens", x.Id, x.SectionId, x.Title, x.OpensAtUtc))
            .ToListAsync(ct);
        return assignments.Concat(exams).OrderBy(x => x.AtUtc).ThenBy(x => x.Id).Take(100).ToArray();
    }
}
