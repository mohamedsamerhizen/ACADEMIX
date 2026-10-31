using System.Security.Claims;
using Academix.Api.Data;
using Academix.Api.Domain.Academic;
using Academix.Api.DTOs.Workspaces;
using Academix.Api.Middleware;
using Academix.Api.Services.Identity;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Academix.Api.Services.Workspaces;

public sealed class PlatformGovernanceService(AcademixDbContext db, IdentitySecurity security)
{
    private Task RequireGlobal(ClaimsPrincipal principal, CancellationToken ct)
        => security.RequireAdminAsync(principal, Guid.Empty, ct, superOnly: true);

    public async Task<PageResult<InstitutionDirectoryItem>> Institutions(ClaimsPrincipal principal, int page, CancellationToken ct)
    {
        var offset = WorkspacePolicy.Offset(page);
        await RequireGlobal(principal, ct);
        var query = db.Institutions.AsNoTracking();
        var total = await query.CountAsync(ct);
        var items = await query.OrderBy(x => x.Code).ThenBy(x => x.Id)
            .Skip(offset).Take(WorkspacePolicy.PageSize)
            .Select(x => new InstitutionDirectoryItem(x.Id, x.Code, x.Name, x.IsActive, x.Version))
            .ToListAsync(ct);
        return new PageResult<InstitutionDirectoryItem>(page, WorkspacePolicy.PageSize, total, items);
    }

    public async Task<InstitutionDirectoryItem> Create(ClaimsPrincipal principal,
        InstitutionCreateRequest request, CancellationToken ct)
    {
        var actor = await security.RequireAdminAsync(principal, Guid.Empty, ct, superOnly: true);
        var (code, name) = WorkspacePolicy.InstitutionFields(request.Code, request.Name);
        if (await db.Institutions.AnyAsync(x => x.Code == code, ct))
            throw ApiFault.Conflict("Institution code already exists.");
        var institution = new Institution { Code = code, Name = name, IsActive = true };
        db.Institutions.Add(institution);
        security.Audit(actor.Id, institution.Id, "institution.created", institution.Id.ToString());
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            throw ApiFault.Conflict("Institution code already exists.");
        }
        return new InstitutionDirectoryItem(institution.Id, institution.Code,
            institution.Name, institution.IsActive, institution.Version);
    }

    public async Task<PlatformOverview> Overview(ClaimsPrincipal principal, CancellationToken ct)
    {
        await RequireGlobal(principal, ct);
        return new PlatformOverview(
            await db.Institutions.AsNoTracking().CountAsync(ct),
            await db.Institutions.AsNoTracking().CountAsync(x => x.IsActive, ct),
            await db.Users.AsNoTracking().CountAsync(ct),
            await db.Courses.AsNoTracking().CountAsync(ct),
            await db.CourseSections.AsNoTracking().CountAsync(ct),
            await db.StudentEnrollments.AsNoTracking().CountAsync(x => x.Status == "Active", ct),
            await db.Exams.AsNoTracking().CountAsync(x => x.Status == "Published", ct));
    }
}
