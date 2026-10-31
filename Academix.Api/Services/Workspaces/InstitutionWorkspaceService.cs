using System.Security.Claims;
using Academix.Api.Data;
using Academix.Api.Domain.Identity;
using Academix.Api.DTOs.Workspaces;
using Academix.Api.Middleware;
using Academix.Api.Services.Identity;
using Microsoft.EntityFrameworkCore;

namespace Academix.Api.Services.Workspaces;

public sealed class InstitutionWorkspaceService(AcademixDbContext db, IdentitySecurity security)
{
    private async Task Guard(ClaimsPrincipal principal, Guid institutionId, CancellationToken ct)
    {
        await security.RequireAdminAsync(principal, institutionId, ct);
        if (!await db.Institutions.AsNoTracking().AnyAsync(x => x.Id == institutionId, ct))
            throw ApiFault.NotFound();
    }

    public async Task<InstitutionOverview> Overview(ClaimsPrincipal principal, Guid institutionId, CancellationToken ct)
    {
        await Guard(principal, institutionId, ct);
        var institution = await db.Institutions.AsNoTracking()
            .Where(x => x.Id == institutionId)
            .Select(x => new { x.Name, x.IsActive }).SingleAsync(ct);
        var users = db.Users.AsNoTracking().Where(x => x.InstitutionId == institutionId && x.IsActive);
        var roles = from ur in db.UserRoles.AsNoTracking()
                    join r in db.Roles.AsNoTracking() on ur.RoleId equals r.Id
                    select new { ur.UserId, r.Name };
        var students = users.Where(u => roles.Any(x => x.UserId == u.Id && x.Name == AcademixRoles.Student));
        var teaching = users.Where(u => roles.Any(x => x.UserId == u.Id &&
            (x.Name == AcademixRoles.Instructor || x.Name == AcademixRoles.TeachingAssistant)));
        return new InstitutionOverview(institutionId, institution.Name, institution.IsActive,
            await db.Faculties.AsNoTracking().CountAsync(x => x.InstitutionId == institutionId, ct),
            await db.Departments.AsNoTracking().CountAsync(x => x.InstitutionId == institutionId, ct),
            await db.AcademicPrograms.AsNoTracking().CountAsync(x => x.InstitutionId == institutionId, ct),
            await db.Courses.AsNoTracking().CountAsync(x => x.InstitutionId == institutionId, ct),
            await db.CourseSections.AsNoTracking().CountAsync(x => x.InstitutionId == institutionId, ct),
            await students.CountAsync(ct), await teaching.CountAsync(ct),
            await db.StudentEnrollments.AsNoTracking().CountAsync(x =>
                x.InstitutionId == institutionId && x.Status == "Active", ct),
            await db.Exams.AsNoTracking().CountAsync(x =>
                x.InstitutionId == institutionId && x.Status == "Published", ct));
    }

    public async Task<PageResult<DirectoryUser>> Users(ClaimsPrincipal principal,
        Guid institutionId, int page, CancellationToken ct)
    {
        var offset = WorkspacePolicy.Offset(page);
        await Guard(principal, institutionId, ct);
        var baseQuery = db.Users.AsNoTracking().Where(x => x.InstitutionId == institutionId);
        var total = await baseQuery.CountAsync(ct);
        var listed = await baseQuery.OrderBy(x => x.Email).ThenBy(x => x.Id)
            .Skip(offset).Take(WorkspacePolicy.PageSize)
            .Select(x => new { x.Id, x.Email, x.IsActive, x.CreatedAtUtc }).ToListAsync(ct);
        var ids = listed.Select(x => x.Id).ToArray();
        var userRoles = await (from assignment in db.UserRoles.AsNoTracking()
                               join role in db.Roles.AsNoTracking() on assignment.RoleId equals role.Id
                               where ids.Contains(assignment.UserId)
                               select new { assignment.UserId, role.Name }).ToListAsync(ct);
        var roleMap = userRoles.GroupBy(x => x.UserId).ToDictionary(x => x.Key,
            x => (IReadOnlyList<string>)x.Select(y => y.Name ?? "").OrderBy(y => y).ToArray());
        var rows = listed.Select(x => new DirectoryUser(x.Id, x.Email, x.IsActive, x.CreatedAtUtc,
            roleMap.TryGetValue(x.Id, out var roles) ? roles : Array.Empty<string>())).ToArray();
        return new PageResult<DirectoryUser>(page, WorkspacePolicy.PageSize, total, rows);
    }

    public async Task<InstitutionCoursePerformance> CoursePerformance(ClaimsPrincipal principal,
        Guid institutionId, Guid courseId, CancellationToken ct)
    {
        await Guard(principal, institutionId, ct);
        var course = await db.Courses.AsNoTracking().Where(x => x.Id == courseId && x.InstitutionId == institutionId)
            .Select(x => new { x.Code, x.Title }).SingleOrDefaultAsync(ct) ?? throw ApiFault.NotFound();
        var sections = db.CourseSections.AsNoTracking().Where(x => x.InstitutionId == institutionId && x.CourseId == courseId)
            .Select(x => x.Id);
        return new InstitutionCoursePerformance(courseId, course.Code, course.Title,
            await sections.CountAsync(ct),
            await db.StudentEnrollments.AsNoTracking().CountAsync(x => x.InstitutionId == institutionId &&
                sections.Contains(x.SectionId) && x.Status == "Active", ct),
            await db.ExamAttempts.AsNoTracking().CountAsync(x => x.InstitutionId == institutionId &&
                x.CourseId == courseId && sections.Contains(x.SectionId) && x.Status == "Submitted", ct),
            await db.ExamGradeReports.AsNoTracking().CountAsync(x => x.InstitutionId == institutionId &&
                x.CourseId == courseId && sections.Contains(x.SectionId) && x.Status == "Published", ct));
    }
}
