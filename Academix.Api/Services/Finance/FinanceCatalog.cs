using System.Security.Claims;
using Academix.Api.Domain.Finance;
using Academix.Api.Domain.Identity;
using Academix.Api.DTOs.Finance;
using Academix.Api.Middleware;
using Microsoft.EntityFrameworkCore;
namespace Academix.Api.Services.Finance;

public sealed partial class FinanceService
{
    public Task<FinanceResult> SetAuthority(ClaimsPrincipal user,Guid inst,Guid target,AuthorityInput input,CancellationToken ct)
        =>Write(user,inst,input.RequestId,"Authority",new{target,input},FinancePermission.Configure,null,async(s,actor)=>
        {
            if((int)input.Permissions is <0 or >255)throw ApiFault.Bad("Invalid financial permissions.");
            var person=await s.db.Users.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==target&&x.IsActive,ct)??throw ApiFault.NotFound();
            var roles=await s.db.UserRoles.Join(s.db.Roles,x=>x.RoleId,x=>x.Id,(r,n)=>new{r.UserId,n.Name}).Where(x=>x.UserId==target).Select(x=>x.Name).ToListAsync(ct);
            if(!roles.Contains(AcademixRoles.SuperAdministrator)&&(person.InstitutionId!=inst||!roles.Contains(AcademixRoles.InstitutionAdministrator)))throw ApiFault.Bad("Finance authority requires an existing current institutional administrator role.");
            var row=await s.db.Set<FinanceAuthority>().SingleOrDefaultAsync(x=>x.InstitutionId==inst&&x.UserId==target,ct);
            FinanceRules.Version(row?.Version??0,input.ExpectedVersion);
            if(row is null){row=new(){InstitutionId=inst,UserId=target,Version=0};s.db.Add(row);}row.Permissions=input.Permissions;row.Version++;
            s.Audit(actor,inst,null,row.Id,"authority.changed",$"User {target}; permissions {(int)input.Permissions}.");return new(row.Id,"Authority","Configured");
        },ct,authorityAdmin:true);
    public Task<FinanceResult> ConfigurePolicy(ClaimsPrincipal user,Guid inst,PolicyInput input,CancellationToken ct)
        =>Write(user,inst,input.RequestId,"Policy",input,FinancePermission.Configure,null,async(s,actor)=>
        {
            var currency=FinanceRules.Currency(input.Currency);FinanceRules.Zone(input.TimeZoneId);FinanceRules.Dates(input.EffectiveFrom,input.EffectiveUntil);
            if(input.MaximumInstallments is <1 or >60||input.MaximumExtensionDays is <1 or >730||input.GraceDays is <0 or >365)throw ApiFault.Bad("Invalid installment or hold policy.");FinanceRules.Money(input.OverdueThresholdMinor,true);
            var previous=await s.db.Set<FinancialPolicy>().Where(x=>x.InstitutionId==inst).ToListAsync(ct);
            if(previous.Any(x=>x.TimeZoneId!=input.TimeZoneId))throw ApiFault.Conflict("Institution financial time zone is stable across currency/policy revisions.");
            if(previous.Any(x=>x.Currency==currency&&x.Status=="Published"&&x.EffectiveFrom<input.EffectiveUntil&&input.EffectiveFrom<x.EffectiveUntil))throw ApiFault.Conflict("Financial policy periods for a currency cannot overlap.");
            var row=new FinancialPolicy{InstitutionId=inst,Currency=currency,Revision=previous.Where(x=>x.Currency==currency).Select(x=>x.Revision).DefaultIfEmpty().Max()+1,
                TimeZoneId=input.TimeZoneId,EffectiveFrom=input.EffectiveFrom,EffectiveUntil=input.EffectiveUntil,RequirePaymentVerification=input.RequirePaymentVerification,
                AllowRefunds=input.AllowRefunds,BlockRefundWithDebt=input.BlockRefundWithDebt,MaximumInstallments=input.MaximumInstallments,MaximumExtensionDays=input.MaximumExtensionDays,
                RequirePlanApproval=input.RequirePlanApproval,GraceDays=input.GraceDays,OverdueThresholdMinor=input.OverdueThresholdMinor,
                BlockRegistration=input.BlockRegistration,BlockGraduation=input.BlockGraduation,BlockDocuments=input.BlockDocuments};s.db.Add(row);
            s.Audit(actor,inst,null,row.Id,"policy.published",$"Currency {currency}; revision {row.Revision}. Applies to new assessments; old invoice contracts remain unchanged.");return new(row.Id,"Policy","Published");
        },ct);
    public async Task<FinanceResult> RetirePolicy(ClaimsPrincipal user,Guid id,FinanceAction input,CancellationToken ct)
    {
        var original=await db.Set<FinancialPolicy>().AsNoTracking().SingleOrDefaultAsync(x=>x.Id==id,ct)??throw ApiFault.NotFound();
        return await Write(user,original.InstitutionId,input.RequestId,"PolicyRetire",new{id,input},FinancePermission.Configure,null,async(s,actor)=>
        {
            var row=await s.db.Set<FinancialPolicy>().SingleAsync(x=>x.Id==id,ct);
            FinanceRules.Version(row.Revision,input.ExpectedVersion);if(input.Action!="Retire")throw ApiFault.Bad("A published policy can only be retired; create a new immutable revision to change rules.");
            if(row.Status!="Retired"){row.Status="Retired";s.Audit(actor,row.InstitutionId,null,id,"policy.retired",FinanceRules.Text(input.Reason,1000,"retirement reason"));}
            return new(id,"Policy",row.Status);
        },ct);
    }
    public Task<FinanceResult> CreateSchedule(ClaimsPrincipal user,Guid inst,FeeScheduleInput input,CancellationToken ct)
        =>Write(user,inst,input.RequestId,"Schedule",input,FinancePermission.Configure,null,async(s,actor)=>
        {
            var currency=FinanceRules.Currency(input.Currency);FinanceRules.Dates(input.EffectiveFrom,input.EffectiveUntil);
            if(input.Revision<1||input.Items is null||input.Items.Count is <1 or >100||(input.TermId.HasValue==input.AcademicYear.HasValue)||input.AcademicYear is <2000 or >2200)throw ApiFault.Bad("A versioned fee schedule needs a term or term-start academic year and 1-100 fees.");
            if(!await s.db.AcademicPrograms.AnyAsync(x=>x.Id==input.ProgramId&&x.InstitutionId==inst&&x.IsActive,ct)||
                input.TermId is Guid term&&!await s.db.AcademicTerms.AnyAsync(x=>x.Id==term&&x.InstitutionId==inst,ct))throw ApiFault.NotFound();
            var code=FinanceRules.Text(input.Code,60,"schedule code").ToUpperInvariant();
            if(await s.db.Set<FeeSchedule>().AnyAsync(x=>x.InstitutionId==inst&&x.Code==code&&x.Currency==currency&&x.Revision==input.Revision,ct))throw ApiFault.Conflict("Schedule revision already exists.");
            var row=new FeeSchedule{InstitutionId=inst,ProgramId=input.ProgramId,TermId=input.TermId,AcademicYear=input.AcademicYear,Currency=currency,Code=code,Revision=input.Revision,EffectiveFrom=input.EffectiveFrom,EffectiveUntil=input.EffectiveUntil};s.db.Add(row);
            var codes=new HashSet<string>(StringComparer.Ordinal);
            foreach(var fee in input.Items)
            {
                if(fee is null)throw ApiFault.Bad("Fee is required.");var feeCode=FinanceRules.Text(fee.Code,60,"fee code").ToUpperInvariant();
                if(!codes.Add(feeCode)||fee.Category is not ("Tuition" or "Registration" or "Laboratory" or "Service" or "Optional")||fee.Mode is not ("Fixed" or "PerCredit"))throw ApiFault.Bad("Distinct fee codes and supported pricing/category rules are required.");
                FinanceRules.Money(fee.UnitMinor);if(fee.CourseId is Guid course&&!await s.db.ProgramCourses.AnyAsync(x=>x.InstitutionId==inst&&x.ProgramId==input.ProgramId&&x.CourseId==course,ct))throw ApiFault.Bad("Course-specific fees must reference this program.");
                s.db.Add(new FeeScheduleItem{InstitutionId=inst,ScheduleId=row.Id,CourseId=fee.CourseId,Code=feeCode,Category=fee.Category,Mode=fee.Mode,UnitMinor=fee.UnitMinor,Optional=fee.Optional});
            }
            s.Audit(actor,inst,null,row.Id,"schedule.created","Draft pricing only; no student liability posted.");return new(row.Id,"Schedule",row.Status);
        },ct);
    public async Task<FinanceResult> ScheduleAction(ClaimsPrincipal user,Guid id,FinanceAction input,CancellationToken ct)
    {
        var original=await db.Set<FeeSchedule>().AsNoTracking().SingleOrDefaultAsync(x=>x.Id==id,ct)??throw ApiFault.NotFound();
        return await Write(user,original.InstitutionId,input.RequestId,"ScheduleAction",new{id,input},FinancePermission.Configure,null,async(s,actor)=>
        {
            var row=await s.db.Set<FeeSchedule>().SingleAsync(x=>x.Id==id,ct);FinanceRules.Version(row.Version,input.ExpectedVersion);
            if(input.Action=="Publish"&&row.Status=="Draft")
            {
                if(await s.db.Set<FeeSchedule>().AnyAsync(x=>x.InstitutionId==row.InstitutionId&&x.Id!=row.Id&&x.ProgramId==row.ProgramId&&x.Currency==row.Currency&&x.Status=="Published"&&
                    x.EffectiveFrom<row.EffectiveUntil&&row.EffectiveFrom<x.EffectiveUntil&&
                    (x.TermId!=null&&row.TermId!=null&&x.TermId==row.TermId||x.AcademicYear!=null&&row.AcademicYear!=null||
                     x.TermId!=null&&row.AcademicYear!=null&&s.db.AcademicTerms.Any(t=>t.Id==x.TermId&&t.StartsOn>=row.EffectiveFrom&&t.StartsOn<row.EffectiveUntil)||
                     x.AcademicYear!=null&&row.TermId!=null&&s.db.AcademicTerms.Any(t=>t.Id==row.TermId&&t.StartsOn>=x.EffectiveFrom&&t.StartsOn<x.EffectiveUntil)),ct))
                    throw ApiFault.Conflict("Retire an overlapping published program schedule before publishing a replacement.");
                if(!await s.db.Set<FeeScheduleItem>().AnyAsync(x=>x.ScheduleId==id,ct))throw ApiFault.Conflict("A schedule cannot publish without fees.");
                row.Status="Published";
            }
            else if(input.Action=="Retire"&&row.Status=="Published")row.Status="Retired";
            else throw ApiFault.Conflict("Invalid fee schedule transition.");
            row.Version++;s.Audit(actor,row.InstitutionId,null,id,"schedule."+row.Status.ToLowerInvariant(),FinanceRules.Text(input.Reason,1000,"reason"));return new(id,"Schedule",row.Status);
        },ct);
    }
    public Task<FinanceResult> OpenAccount(ClaimsPrincipal user,Guid inst,Guid student,OpenAccountInput input,CancellationToken ct)
        =>Write(user,inst,input.RequestId,"OpenAccount",new{student,input},FinancePermission.Billing,null,async(s,actor)=>
        {
            if(!await s.db.Users.AnyAsync(x=>x.Id==student&&x.InstitutionId==inst,ct)||!await s.db.UserRoles.Join(s.db.Roles,x=>x.RoleId,x=>x.Id,(r,n)=>new{r.UserId,n.Name}).AnyAsync(x=>x.UserId==student&&x.Name==AcademixRoles.Student,ct))throw ApiFault.NotFound();
            var currency=FinanceRules.Currency(input.Currency);await s.ActivePolicy(inst,currency,ct);
            var row=await s.db.Set<FinancialAccount>().SingleOrDefaultAsync(x=>x.InstitutionId==inst&&x.StudentId==student&&x.Currency==currency,ct);
            if(row is null){row=new(){InstitutionId=inst,StudentId=student,Currency=currency};s.db.Add(row);s.Audit(actor,inst,row.Id,row.Id,"account.opened","Currency-specific student financial account.");}
            return new(row.Id,"Account",row.Status,row.Id);
        },ct);
    public Task<FinanceResult> AccountAction(ClaimsPrincipal user,Guid id,FinanceAction input,CancellationToken ct)
        =>AccountWrite(user,id,input.RequestId,"AccountAction",input,FinancePermission.Configure,async(s,actor)=>
        {
            var row=await s.db.Set<FinancialAccount>().SingleAsync(x=>x.Id==id,ct);
            FinanceRules.Version(row.Version,input.ExpectedVersion);
            row.Status=input.Action switch{"Freeze"=>"Frozen","Unfreeze"=>"Active","RefreshHolds"=>row.Status,_=>throw ApiFault.Bad("Invalid account action.")};
            row.Version++;
            s.Audit(actor,row.InstitutionId,id,id,"account."+input.Action.ToLowerInvariant(),FinanceRules.Text(input.Reason,1000,"reason"));return new(id,"Account",row.Status,id);
        },ct);
    private async Task<FinancialPolicy> AccountPolicy(FinancialAccount account,CancellationToken ct)
    {
        var rows=await db.Set<FinancialPolicy>().AsNoTracking().Where(x=>x.InstitutionId==account.InstitutionId&&x.Currency==account.Currency).OrderByDescending(x=>x.Revision).ToListAsync(ct);
        return rows.FirstOrDefault(x=>x.EffectiveFrom<=FinanceRules.Today(x.TimeZoneId))??throw ApiFault.Conflict("Account currency has no configured financial policy.");
    }
}
