using System.Security.Claims;
using Academix.Api.Data;
using Academix.Api.Domain.Advising;
using Academix.Api.Domain.Identity;
using Academix.Api.Middleware;
using Academix.Api.Services.Identity;
using Microsoft.EntityFrameworkCore;

namespace Academix.Api.Services.Advising;

public sealed class AcademicProgressAccess(AcademixDbContext db,IdentitySecurity security)
{
    public static bool CanAccess(Guid actor,Guid? actorInstitution,IReadOnlyCollection<string> roles,
        Guid institution,Guid student,bool assigned,bool staffOnly)
        => roles.Contains(AcademixRoles.SuperAdministrator)||actorInstitution==institution&&
            (roles.Contains(AcademixRoles.InstitutionAdministrator)||roles.Contains(AcademixRoles.Instructor)&&assigned||
                !staffOnly&&actor==student&&roles.Contains(AcademixRoles.Student));

    public async Task<(Guid Institution,Guid Actor)> Require(ClaimsPrincipal principal,Guid student,bool staffOnly,CancellationToken ct)
    {
        var (user,roles)=await security.CurrentAsync(principal,ct);
        var institution=await db.Users.AsNoTracking().Where(x=>x.Id==student&&x.IsActive).Select(x=>x.InstitutionId).SingleOrDefaultAsync(ct)
            ??throw ApiFault.NotFound();
        if(!await db.UserRoles.Join(db.Roles,x=>x.RoleId,x=>x.Id,(u,r)=>new{u.UserId,r.Name}).AnyAsync(x=>x.UserId==student&&x.Name==AcademixRoles.Student,ct))throw ApiFault.NotFound();
        var assigned=await db.Set<AdvisorAssignment>().AnyAsync(x=>x.InstitutionId==institution&&x.StudentId==student&&x.AdvisorId==user.Id&&x.EndedAtUtc==null,ct);
        if(!CanAccess(user.Id,user.InstitutionId,roles,institution,student,assigned,staffOnly))throw ApiFault.Forbidden();
        return(institution,user.Id);
    }
    public async Task RequireInstructor(Guid institution,Guid id,CancellationToken ct)
    {
        if(!await db.Users.AnyAsync(x=>x.Id==id&&x.IsActive&&x.InstitutionId==institution,ct)||
            !await db.UserRoles.Join(db.Roles,x=>x.RoleId,x=>x.Id,(u,r)=>new{u.UserId,r.Name}).AnyAsync(x=>x.UserId==id&&x.Name==AcademixRoles.Instructor,ct))
            throw ApiFault.Bad("An active institutional Instructor is required.");
    }
}
