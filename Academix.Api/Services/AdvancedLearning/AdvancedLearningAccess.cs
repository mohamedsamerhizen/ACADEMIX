using System.Security.Claims;
using Academix.Api.Data;
using Academix.Api.Domain.Academic;
using Academix.Api.Domain.Identity;
using Academix.Api.Middleware;
using Academix.Api.Services.Identity;
using Microsoft.EntityFrameworkCore;

namespace Academix.Api.Services.AdvancedLearning;

public sealed class AdvancedLearningAccess(AcademixDbContext db, IdentitySecurity security)
{
    public async Task<CourseSection> Section(Guid id, CancellationToken ct)
        => await db.CourseSections.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw ApiFault.NotFound();

    public async Task<Guid> Staff(ClaimsPrincipal principal, CourseSection section, CancellationToken ct)
        => (await security.RequireSectionStaffAsync(principal, section.InstitutionId, section.Id, ct)).Id;

    public async Task<(Guid Id, bool Staff)> Viewer(ClaimsPrincipal principal, CourseSection section, CancellationToken ct)
    {
        var (user, roles) = await security.CurrentAsync(principal, ct);
        if (roles.Contains(AcademixRoles.SuperAdministrator) ||
            roles.Contains(AcademixRoles.InstitutionAdministrator) && user.InstitutionId == section.InstitutionId ||
            await security.IsCurrentSectionTeacherAsync(user, roles, section.InstitutionId, section.Id, ct))
            return (user.Id, true);
        if (section.Status == "Open" && user.InstitutionId == section.InstitutionId &&
            roles.Contains(AcademixRoles.Student) &&
            await db.StudentEnrollments.AnyAsync(x => x.SectionId == section.Id && x.StudentId == user.Id && x.Status == "Active", ct))
            return (user.Id, false);
        throw ApiFault.Forbidden();
    }

    public async Task<Guid> Student(ClaimsPrincipal principal, CourseSection section, CancellationToken ct)
    {
        var (user, roles) = await security.CurrentAsync(principal, ct);
        if (section.Status != "Open" || user.InstitutionId != section.InstitutionId ||
            !roles.Contains(AcademixRoles.Student) ||
            !await db.StudentEnrollments.AnyAsync(x => x.SectionId == section.Id && x.StudentId == user.Id && x.Status == "Active", ct))
            throw ApiFault.Forbidden();
        return user.Id;
    }

    public static string Text(string? value, int max, string field)
    {
        var text = value?.Trim() ?? "";
        if (text.Length == 0 || text.Length > max) throw ApiFault.Bad($"Invalid {field} (1-{max} characters).");
        return text;
    }
}
