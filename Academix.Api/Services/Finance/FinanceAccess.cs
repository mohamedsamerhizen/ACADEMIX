using System.Security.Claims;
using Academix.Api.Data;
using Academix.Api.Domain.Finance;
using Academix.Api.Domain.Identity;
using Academix.Api.Middleware;
using Academix.Api.Services.Identity;
using Microsoft.EntityFrameworkCore;
namespace Academix.Api.Services.Finance;

public sealed class FinanceAccess(AcademixDbContext db,IdentitySecurity security)
{
    public static bool Allowed(Guid? actorInstitution,IReadOnlyCollection<string> roles,Guid institution,FinancePermission granted,FinancePermission required)
        =>(roles.Contains(AcademixRoles.SuperAdministrator)||actorInstitution==institution&&roles.Contains(AcademixRoles.InstitutionAdministrator))&&
            granted!=FinancePermission.None&&(required==FinancePermission.None||(granted&required)==required);
    public static bool OwnAccount(Guid actor,Guid? institution,IReadOnlyCollection<string> roles,FinancialAccount account)
        =>actor==account.StudentId&&institution==account.InstitutionId&&roles.Contains(AcademixRoles.Student);
    public async Task<ApplicationUser> Require(ClaimsPrincipal user,Guid institution,FinancePermission permission,CancellationToken ct)
    {
        var (actor,roles)=await security.CurrentAsync(user,ct);
        var grant=await db.Set<FinanceAuthority>().AsNoTracking().Where(x=>x.InstitutionId==institution&&x.UserId==actor.Id).Select(x=>x.Permissions).SingleOrDefaultAsync(ct);
        if(!Allowed(actor.InstitutionId,roles,institution,grant,permission))throw ApiFault.Forbidden();return actor;
    }
    public async Task<ApplicationUser> ReadAccount(ClaimsPrincipal user,FinancialAccount account,CancellationToken ct)
    {
        var (actor,roles)=await security.CurrentAsync(user,ct);
        if(OwnAccount(actor.Id,actor.InstitutionId,roles,account))return actor;
        return await Require(user,account.InstitutionId,FinancePermission.None,ct);
    }
    public static void Independent(Guid actor,Guid student,Guid requestedBy)
    {if(actor==student||actor==requestedBy)throw ApiFault.Forbidden();}
}
