using System.Security.Claims;
using Academix.Api.Data;
using Academix.Api.Domain.Identity;
using Academix.Api.DTOs.Workspaces;
using Academix.Api.Middleware;
using Academix.Api.Services.Identity;
using Microsoft.EntityFrameworkCore;

namespace Academix.Api.Services.Workspaces;

public sealed class InstructorWorkspaceService(AcademixDbContext db, IdentitySecurity security)
{
    public async Task<PageResult<TeachingSection>> Sections(ClaimsPrincipal principal, int page, CancellationToken ct)
    {
        var offset = WorkspacePolicy.Offset(page);
        var (user, roles) = await security.CurrentAsync(principal, ct);
        if (user.InstitutionId is not Guid institutionId) throw ApiFault.Forbidden();
        var kinds = TeachingRolePolicy.ActiveAssignmentKinds(roles);
        if (kinds.Length == 0) throw ApiFault.Forbidden();
        var query = from assignment in db.TeachingAssignments.AsNoTracking()
                    join section in db.CourseSections.AsNoTracking() on assignment.SectionId equals section.Id
                    join course in db.Courses.AsNoTracking() on section.CourseId equals course.Id
                    where assignment.UserId == user.Id && assignment.InstitutionId == institutionId &&
                          section.InstitutionId == institutionId && course.InstitutionId == institutionId &&
                          kinds.Contains(assignment.Kind)
                    select new { Assignment = assignment, Section = section, Course = course };
        var total = await query.CountAsync(ct);
        var rows = await query.OrderBy(x => x.Course.Code).ThenBy(x => x.Section.Code)
            .ThenBy(x => x.Section.Id).Skip(offset).Take(WorkspacePolicy.PageSize)
            .Select(x => new TeachingSection(x.Section.Id, x.Section.Code, x.Course.Id, x.Course.Code,
                x.Course.Title, x.Assignment.Kind, x.Section.Status, x.Section.Capacity,
                db.StudentEnrollments.Count(e => e.InstitutionId == institutionId &&
                    e.SectionId == x.Section.Id && e.Status == "Active")))
            .ToListAsync(ct);
        return new PageResult<TeachingSection>(page, WorkspacePolicy.PageSize, total, rows);
    }

    private async Task<Guid> Scope(ClaimsPrincipal principal, Guid sectionId, CancellationToken ct)
    {
        var section = await db.CourseSections.AsNoTracking()
            .Where(x => x.Id == sectionId).Select(x => new { x.InstitutionId }).FirstOrDefaultAsync(ct)
            ?? throw ApiFault.NotFound();
        await security.RequireSectionStaffAsync(principal, section.InstitutionId, sectionId, ct);
        return section.InstitutionId;
    }

    public async Task<PageResult<SectionStudent>> Roster(ClaimsPrincipal principal,
        Guid sectionId, int page, CancellationToken ct)
    {
        var offset = WorkspacePolicy.Offset(page);
        var institutionId = await Scope(principal, sectionId, ct);
        var publishedLessons = from lesson in db.LearningLessons.AsNoTracking()
                               join unit in db.LearningUnits.AsNoTracking() on lesson.UnitId equals unit.Id
                               where lesson.SectionId == sectionId && lesson.InstitutionId == institutionId &&
                                     unit.SectionId == sectionId && unit.InstitutionId == institutionId &&
                                     unit.IsPublished && lesson.IsPublished
                               select lesson.Id;
        var query = from enrollment in db.StudentEnrollments.AsNoTracking()
                    join student in db.Users.AsNoTracking() on enrollment.StudentId equals student.Id
                    where enrollment.InstitutionId == institutionId && enrollment.SectionId == sectionId &&
                          enrollment.Status == "Active" && student.InstitutionId == institutionId
                    select new { Enrollment = enrollment, Student = student };
        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(x => x.Student.Email).ThenBy(x => x.Student.Id)
            .Skip(offset).Take(WorkspacePolicy.PageSize)
            .Select(x => new SectionStudent(x.Student.Id, x.Student.Email, x.Enrollment.EnrolledAtUtc,
                db.LessonCompletions.Count(c => c.StudentId == x.Student.Id && c.SectionId == sectionId &&
                    c.InstitutionId == institutionId && publishedLessons.Contains(c.LessonId)),
                db.LearningSubmissions.Count(s => s.StudentId == x.Student.Id &&
                    s.SectionId == sectionId && s.InstitutionId == institutionId &&
                    (s.Status == "Submitted" || s.Status == "Graded")),
                db.ExamGradeReports.Count(g => g.StudentId == x.Student.Id &&
                    g.SectionId == sectionId && g.InstitutionId == institutionId &&
                    g.Status == "Published")))
            .ToListAsync(ct);
        return new PageResult<SectionStudent>(page, WorkspacePolicy.PageSize, total, items);
    }

    public async Task<SectionPerformance> Performance(ClaimsPrincipal principal, Guid sectionId, CancellationToken ct)
    {
        var institutionId = await Scope(principal, sectionId, ct);
        var enrolled = db.StudentEnrollments.AsNoTracking().Where(x => x.SectionId == sectionId &&
            x.InstitutionId == institutionId && x.Status == "Active");
        var students = enrolled.Select(x => x.StudentId);
        var lessons = from lesson in db.LearningLessons.AsNoTracking()
                      join unit in db.LearningUnits.AsNoTracking() on lesson.UnitId equals unit.Id
                      where lesson.SectionId == sectionId && lesson.InstitutionId == institutionId &&
                            unit.SectionId == sectionId && unit.InstitutionId == institutionId &&
                            lesson.IsPublished && unit.IsPublished
                      select lesson.Id;
        var grades = db.ExamGradeReports.AsNoTracking().Where(x => x.SectionId == sectionId &&
            x.InstitutionId == institutionId && x.Status == "Published" && students.Contains(x.StudentId));
        var average = await grades.AverageAsync(x => x.MaximumPoints > 0m
            ? (decimal?)(x.EarnedPoints * 100m / x.MaximumPoints) : null, ct);
        return new SectionPerformance(sectionId,
            await enrolled.CountAsync(ct),
            await lessons.CountAsync(ct),
            await db.LessonCompletions.AsNoTracking().CountAsync(x => x.SectionId == sectionId &&
                x.InstitutionId == institutionId && students.Contains(x.StudentId) && lessons.Contains(x.LessonId), ct),
            await db.LearningAssignments.AsNoTracking().CountAsync(x => x.SectionId == sectionId &&
                x.InstitutionId == institutionId && x.IsPublished, ct),
            await db.LearningSubmissions.AsNoTracking().CountAsync(x => x.SectionId == sectionId &&
                x.InstitutionId == institutionId && students.Contains(x.StudentId) &&
                (x.Status == "Submitted" || x.Status == "Graded"), ct),
            await db.Exams.AsNoTracking().CountAsync(x => x.SectionId == sectionId &&
                x.InstitutionId == institutionId && x.Status == "Published", ct),
            await db.ExamAttempts.AsNoTracking().CountAsync(x => x.SectionId == sectionId &&
                x.InstitutionId == institutionId && students.Contains(x.StudentId) && x.Status == "Submitted", ct),
            await grades.CountAsync(ct), average);
    }
}
