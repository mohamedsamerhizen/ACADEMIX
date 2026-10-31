using Academix.Api.Services.Infrastructure;
using System.Security.Claims;
using Academix.Api.Data;
using Academix.Api.Domain.Academic;
using Academix.Api.DTOs.Academic;
using Academix.Api.Middleware;
using Academix.Api.Services.Identity;
using Microsoft.EntityFrameworkCore;

namespace Academix.Api.Services.Academic;

public sealed class StructureService(AcademixDbContext db, IdentitySecurity access, RetryingServiceWork retry)
{
    private static string Code(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > 30 || !value.Trim().All(c => char.IsAsciiLetterUpper(c) || char.IsAsciiDigit(c) || c == '-'))
            throw ApiFault.Bad("Code must contain 1-30 uppercase ASCII letters, numbers, or hyphens.");
        return value.Trim();
    }
    private static string Name(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > 200) throw ApiFault.Bad("Name must contain 1-200 characters.");
        return value.Trim();
    }
    private async Task Guard(Guid institutionId, ClaimsPrincipal principal, CancellationToken ct)
    {
        await access.RequireAdminAsync(principal, institutionId, ct);
        if (!await db.Institutions.AnyAsync(x => x.Id == institutionId && x.IsActive, ct)) throw ApiFault.Bad("Institution is inactive or missing.");
    }

    public async Task<object> GetInstitution(ClaimsPrincipal principal, Guid id, CancellationToken ct)
    {
        await access.RequireAdminAsync(principal, id, ct);
        return await db.Institutions.AsNoTracking().Where(x => x.Id == id).Select(x => new { x.Id, x.Code, x.Name, x.IsActive, x.Version }).FirstOrDefaultAsync(ct) ?? throw ApiFault.NotFound();
    }
    public async Task<object> UpdateInstitution(ClaimsPrincipal principal, Guid id, InstitutionUpdate req, CancellationToken ct)
    {
        var actor = await access.RequireAdminAsync(principal, id, ct, superOnly: true);
        var x = await db.Institutions.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw ApiFault.NotFound();
        if (x.Version != req.ExpectedVersion) throw ApiFault.Conflict("Stale institution version.");
        x.Name = Name(req.Name); x.IsActive = req.IsActive; x.Version++;
        access.Audit(actor.Id, id, "institution.updated", id.ToString());
        await db.SaveChangesAsync(ct);
        return x;
    }

    public async Task<object> GetFaculty(ClaimsPrincipal user, Guid id, CancellationToken ct)
    {
        var x = await db.Faculties.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw ApiFault.NotFound();
        await Guard(x.InstitutionId, user, ct); return x;
    }
    public async Task<object> GetDepartment(ClaimsPrincipal user, Guid id, CancellationToken ct)
    {
        var x = await db.Departments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw ApiFault.NotFound();
        await Guard(x.InstitutionId, user, ct); return x;
    }
    public async Task<object> GetProgram(ClaimsPrincipal user, Guid id, CancellationToken ct)
    {
        var x = await db.AcademicPrograms.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw ApiFault.NotFound();
        await Guard(x.InstitutionId, user, ct); return x;
    }
    public async Task<object> GetTerm(ClaimsPrincipal user, Guid id, CancellationToken ct)
    {
        var x = await db.AcademicTerms.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw ApiFault.NotFound();
        await Guard(x.InstitutionId, user, ct); return x;
    }
    public async Task<object> CreateFaculty(ClaimsPrincipal user, NamedCreate req, CancellationToken ct)
    {
        await Guard(req.InstitutionId, user, ct);
        var x = new Faculty { InstitutionId = req.InstitutionId, Code = Code(req.Code), Name = Name(req.Name) };
        db.Faculties.Add(x); await db.SaveChangesAsync(ct); return x;
    }
    public async Task<object> ListFaculties(ClaimsPrincipal user, Guid inst, int page, CancellationToken ct)
    {
        await Guard(inst, user, ct);
        return await db.Faculties.AsNoTracking().Where(x => x.InstitutionId == inst).OrderBy(x => x.Code)
            .Skip((Math.Clamp(page, 1, 100000) - 1) * 50).Take(50).ToListAsync(ct);
    }
    public async Task<object> UpdateFaculty(ClaimsPrincipal user, Guid id, NamedUpdate req, CancellationToken ct)
    {
        var x = await db.Faculties.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw ApiFault.NotFound();
        await Guard(x.InstitutionId, user, ct);
        if (x.Version != req.ExpectedVersion) throw ApiFault.Conflict("Stale faculty version.");
        if (!req.IsActive && await db.Departments.AnyAsync(d => d.FacultyId == x.Id && d.IsActive, ct)) throw ApiFault.Conflict("Deactivate dependent departments first.");
        x.Name = Name(req.Name); x.IsActive = req.IsActive; x.Version++;
        await db.SaveChangesAsync(ct); return x;
    }
    public async Task<object> CreateDepartment(ClaimsPrincipal user, DepartmentCreate req, CancellationToken ct)
    {
        await Guard(req.InstitutionId, user, ct);
        if (req.FacultyId is Guid faculty && !await db.Faculties.AnyAsync(x => x.Id == faculty && x.InstitutionId == req.InstitutionId && x.IsActive, ct)) throw ApiFault.Bad("Invalid faculty.");
        var x = new Department { InstitutionId = req.InstitutionId, FacultyId = req.FacultyId, Code = Code(req.Code), Name = Name(req.Name) };
        db.Departments.Add(x); await db.SaveChangesAsync(ct); return x;
    }
    public async Task<object> ListDepartments(ClaimsPrincipal user, Guid inst, int page, CancellationToken ct)
    {
        await Guard(inst, user, ct);
        return await db.Departments.AsNoTracking().Where(x => x.InstitutionId == inst).OrderBy(x => x.Code)
            .Skip((Math.Clamp(page, 1, 100000) - 1) * 50).Take(50).ToListAsync(ct);
    }
    public async Task<object> UpdateDepartment(ClaimsPrincipal user, Guid id, NamedUpdate req, CancellationToken ct)
    {
        var x = await db.Departments.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw ApiFault.NotFound();
        await Guard(x.InstitutionId, user, ct);
        if (x.Version != req.ExpectedVersion) throw ApiFault.Conflict("Stale department version.");
        if (!req.IsActive && await db.AcademicPrograms.AnyAsync(p => p.DepartmentId == x.Id && p.IsActive, ct)) throw ApiFault.Conflict("Deactivate dependent programs first.");
        x.Name = Name(req.Name); x.IsActive = req.IsActive; x.Version++;
        await db.SaveChangesAsync(ct); return x;
    }
    public async Task<object> CreateProgram(ClaimsPrincipal user, ProgramCreate req, CancellationToken ct)
    {
        await Guard(req.InstitutionId, user, ct);
        if (!await db.Departments.AnyAsync(d => d.Id == req.DepartmentId && d.InstitutionId == req.InstitutionId && d.IsActive, ct)) throw ApiFault.Bad("Invalid department.");
        var x = new AcademicProgram { InstitutionId = req.InstitutionId, DepartmentId = req.DepartmentId, Code = Code(req.Code), Name = Name(req.Name) };
        db.AcademicPrograms.Add(x); await db.SaveChangesAsync(ct); return x;
    }
    public async Task<object> ListPrograms(ClaimsPrincipal user, Guid inst, int page, CancellationToken ct)
    {
        await Guard(inst, user, ct);
        return await db.AcademicPrograms.AsNoTracking().Where(x => x.InstitutionId == inst).OrderBy(x => x.Code)
            .Skip((Math.Clamp(page, 1, 100000) - 1) * 50).Take(50).ToListAsync(ct);
    }
    public async Task<object> UpdateProgram(ClaimsPrincipal user, Guid id, NamedUpdate req, CancellationToken ct)
    {
        var x = await db.AcademicPrograms.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw ApiFault.NotFound();
        await Guard(x.InstitutionId, user, ct);
        if (x.Version != req.ExpectedVersion) throw ApiFault.Conflict("Stale program version.");
        x.Name = Name(req.Name); x.IsActive = req.IsActive; x.Version++;
        await db.SaveChangesAsync(ct); return x;
    }
    public async Task<object> CreateTerm(ClaimsPrincipal user, TermCreate req, CancellationToken ct)
    {
        await Guard(req.InstitutionId, user, ct);
        if (req.StartsOn >= req.EndsOn) throw ApiFault.Bad("Term start must be before end.");
        if (await db.AcademicTerms.AnyAsync(x => x.InstitutionId == req.InstitutionId && x.Status != "Closed" && x.StartsOn < req.EndsOn && req.StartsOn < x.EndsOn, ct)) throw ApiFault.Conflict("Term dates overlap.");
        var x = new AcademicTerm { InstitutionId = req.InstitutionId, Code = Code(req.Code), Name = Name(req.Name), StartsOn = req.StartsOn, EndsOn = req.EndsOn };
        db.AcademicTerms.Add(x); await db.SaveChangesAsync(ct); return x;
    }
    public async Task<object> ListTerms(ClaimsPrincipal user, Guid inst, int page, CancellationToken ct)
    {
        await Guard(inst, user, ct);
        return await db.AcademicTerms.AsNoTracking().Where(x => x.InstitutionId == inst).OrderByDescending(x => x.StartsOn)
            .Skip((Math.Clamp(page, 1, 100000) - 1) * 50).Take(50).ToListAsync(ct);
    }
    public Task<object> UpdateTerm(ClaimsPrincipal user, Guid id, TermUpdate req, CancellationToken ct)
        => retry.ExecuteAsync<StructureService, object>(service => service.UpdateTermCore(user, id, req, ct), ct);

    private async Task<object> UpdateTermCore(ClaimsPrincipal user, Guid id, TermUpdate req, CancellationToken ct)
    {
        var institutionId = await db.AcademicTerms.AsNoTracking().Where(x => x.Id == id)
            .Select(x => x.InstitutionId).SingleOrDefaultAsync(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await RegistrationCoordination.Lock(db, institutionId, ct);
        var x = (await db.AcademicTerms.FromSqlInterpolated(
            $"SELECT * FROM academic_terms WHERE \"Id\" = {id} FOR UPDATE").ToListAsync(ct)).SingleOrDefault()
            ?? throw ApiFault.NotFound();
        await Guard(x.InstitutionId, user, ct);
        if (x.Version != req.ExpectedVersion) throw ApiFault.Conflict("Stale term version.");
        if (req.StartsOn >= req.EndsOn) throw ApiFault.Bad("Invalid date range.");
        if (!(x.Status == req.Status || x.Status == "Planned" && req.Status == "Active" || x.Status == "Active" && req.Status == "Closed")) throw ApiFault.Bad("Invalid term transition.");
        if (x.Status != "Planned" && (req.StartsOn != x.StartsOn || req.EndsOn != x.EndsOn)) throw ApiFault.Conflict("Dates cannot change after activation.");
        if (req.Status == "Active" && await db.AcademicTerms.AnyAsync(t => t.Id != id && t.InstitutionId == x.InstitutionId && t.Status == "Active" && t.StartsOn < req.EndsOn && req.StartsOn < t.EndsOn, ct)) throw ApiFault.Conflict("Active terms may not overlap.");
        x.Name = Name(req.Name); x.StartsOn = req.StartsOn; x.EndsOn = req.EndsOn; x.Status = req.Status; x.Version++;
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return x;
    }
}
