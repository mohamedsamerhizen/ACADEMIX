using Academix.Api.Data;
using Academix.Api.Domain.Identity;
using Microsoft.EntityFrameworkCore;
namespace Academix.Api.Services.Registrar;
public static class RegistrarAuthority
{
    public static Task<bool> IsAdmin(AcademixDbContext db,Guid user,Guid inst,CancellationToken ct)=>
        (from u in db.Users join ur in db.UserRoles on u.Id equals ur.UserId join role in db.Roles on ur.RoleId equals role.Id
         where u.Id==user&&u.IsActive&&(role.Name==AcademixRoles.SuperAdministrator||u.InstitutionId==inst&&role.Name==AcademixRoles.InstitutionAdministrator)
         select u.Id).AnyAsync(ct);
}
