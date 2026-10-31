using System.Security.Claims;
using System.Text.Json;
using Academix.Api.Domain.Finance;
using Academix.Api.DTOs.Finance;
using Academix.Api.Middleware;
using Academix.Api.Services.Infrastructure;
using Microsoft.EntityFrameworkCore;
namespace Academix.Api.Services.Finance;

public sealed partial class FinanceService
{
    public async Task<FinanceResult> ProposePlan(ClaimsPrincipal user,Guid invoiceId,PaymentPlanInput input,CancellationToken ct)
    {
        var original=await Invoice(invoiceId,ct);
        return await AccountWrite(user,original.AccountId,input.RequestId,"PaymentPlan",new{invoiceId,input},FinancePermission.Billing,async(s,actor)=>
        {
            var invoice=await s.Invoice(invoiceId,ct);var account=await s.Account(invoice.AccountId,ct);
            if(actor!=account.StudentId)await s.access.Require(user,account.InstitutionId,FinancePermission.Billing,ct);
            if(account.Status!="Active"||invoice.Status!="Posted")throw ApiFault.Conflict("A posted invoice in an unrestricted account is required.");
            if(await s.db.Set<FinancePaymentPlan>().AnyAsync(x=>x.InvoiceId==invoiceId&&x.Status=="Proposed",ct))throw ApiFault.Conflict("A proposed revision already awaits review.");
            var policy=await s.db.Set<FinancialPolicy>().AsNoTracking().SingleAsync(x=>x.Id==invoice.PolicyId,ct);var debt=await s.Outstanding(invoiceId,ct);
            var dates=FinanceRules.Installments(debt,input.Dates,policy.MaximumInstallments,FinanceRules.Today(policy.TimeZoneId),invoice.DueDate,policy.MaximumExtensionDays);
            var row=new FinancePaymentPlan{InstitutionId=account.InstitutionId,InvoiceId=invoiceId,Revision=(await s.db.Set<FinancePaymentPlan>().Where(x=>x.InvoiceId==invoiceId).Select(x=>(int?)x.Revision).MaxAsync(ct)??0)+1,
                BaseDebtMinor=debt,RequestedBy=actor,Reason=FinanceRules.Text(input.Reason,1000,"rescheduling reason")};
            if(!policy.RequirePlanApproval&&actor!=account.StudentId)
            {
                await s.SupersedePlan(invoiceId,ct);row.Status="Approved";row.ReviewedBy=actor;row.ApprovedAtUtc=DateTime.UtcNow;
            }
            s.db.Add(row);s.db.AddRange(dates.Select((x,i)=>new FinanceInstallment{InstitutionId=account.InstitutionId,PlanId=row.Id,Position=i+1,DueDate=x.Date,AmountMinor=x.Amount}));
            s.Audit(actor,account.InstitutionId,account.Id,row.Id,"plan.proposed","Existing debt rescheduled; no new monetary charge.");return new(row.Id,"PaymentPlan",row.Status,account.Id);
        },ct,student:true);
    }
    private async Task SupersedePlan(Guid invoice,CancellationToken ct)
    {
        var previous=await db.Set<FinancePaymentPlan>().SingleOrDefaultAsync(x=>x.InvoiceId==invoice&&x.Status=="Approved",ct);
        if(previous is not null){previous.Status="Superseded";previous.SupersededAtUtc=DateTime.UtcNow;previous.Version++;await db.SaveChangesAsync(ct);}
    }
    public async Task<FinanceResult> DecidePlan(ClaimsPrincipal user,Guid planId,FinanceAction input,CancellationToken ct)
    {
        var original=await db.Set<FinancePaymentPlan>().AsNoTracking().SingleOrDefaultAsync(x=>x.Id==planId,ct)??throw ApiFault.NotFound();var inv=await Invoice(original.InvoiceId,ct);
        return await AccountWrite(user,inv.AccountId,input.RequestId,"PlanDecision",new{planId,input},FinancePermission.Approve,async(s,actor)=>
        {
            var row=await s.db.Set<FinancePaymentPlan>().SingleAsync(x=>x.Id==planId,ct);var invoice=await s.Invoice(row.InvoiceId,ct);var account=await s.Account(invoice.AccountId,ct);
            FinanceRules.Version(row.Version,input.ExpectedVersion);FinanceAccess.Independent(actor,account.StudentId,row.RequestedBy);
            if(row.Status!="Proposed"||input.Action is not ("Approve" or "Reject"))throw ApiFault.Conflict("Only a proposed plan can be decided.");
            if(input.Action=="Approve")
            {
                if(invoice.Status!="Posted"||account.Status!="Active"||await s.Outstanding(invoice.Id,ct)!=row.BaseDebtMinor)throw ApiFault.Conflict("Debt changed; reject and submit a reconciled revision.");
                var policy=await s.db.Set<FinancialPolicy>().AsNoTracking().SingleAsync(x=>x.Id==invoice.PolicyId,ct);
                var dates=await s.db.Set<FinanceInstallment>().Where(x=>x.PlanId==row.Id).OrderBy(x=>x.Position).Select(x=>x.DueDate).ToListAsync(ct);
                FinanceRules.Installments(row.BaseDebtMinor,dates,policy.MaximumInstallments,FinanceRules.Today(policy.TimeZoneId),invoice.DueDate,policy.MaximumExtensionDays);
                await s.SupersedePlan(invoice.Id,ct);row.Status="Approved";row.ApprovedAtUtc=DateTime.UtcNow;
            }
            else row.Status="Rejected";
            row.ReviewedBy=actor;row.Version++;s.Audit(actor,account.InstitutionId,account.Id,planId,"plan."+row.Status.ToLowerInvariant(),FinanceRules.Text(input.Reason,1000,"decision reason"));
            s.Notify(account,"FinancePlanReviewed",planId);return new(planId,"PaymentPlan",row.Status,account.Id);
        },ct);
    }
    public Task<FinanceResult> CreateScholarship(ClaimsPrincipal user,Guid inst,ScholarshipInput input,CancellationToken ct)
        =>Write(user,inst,input.RequestId,"Scholarship",input,FinancePermission.Configure,null,async(s,actor)=>
        {
            var currency=FinanceRules.Currency(input.Currency);FinanceRules.Dates(input.EffectiveFrom,input.EffectiveUntil);await s.ActivePolicy(inst,currency,ct);
            if(input.Revision<1||input.Priority is <0 or >1000000||input.Category is not ("All" or "Tuition" or "Registration" or "Laboratory" or "Service" or "Optional")||
                input.Mode is not ("Fixed" or "Percent" or "Full")||input.BasisPoints is <0 or >10000||input.MinimumGpa is <0 or >4||input.MinimumGpa.HasValue&&decimal.Round(input.MinimumGpa.Value,2)!=input.MinimumGpa.Value||
                input.Mode=="Percent"&&(input.BasisPoints==0||input.FixedMinor!=0)||input.Mode=="Fixed"&&(input.FixedMinor<=0||input.BasisPoints!=0)||input.Mode=="Full"&&(input.FixedMinor!=0||input.BasisPoints!=0))throw ApiFault.Bad("Invalid scholarship pricing or academic eligibility rules.");
            FinanceRules.Money(input.FixedMinor,true);if(input.FundingLimitMinor is long limit)FinanceRules.Money(limit,true);
            if(input.ProgramId is Guid program&&!await s.db.AcademicPrograms.AnyAsync(x=>x.Id==program&&x.InstitutionId==inst,ct)||input.TermId is Guid term&&!await s.db.AcademicTerms.AnyAsync(x=>x.Id==term&&x.InstitutionId==inst,ct))throw ApiFault.NotFound();
            var code=FinanceRules.Text(input.Code,60,"scholarship code").ToUpperInvariant();
            if(await s.db.Set<ScholarshipProgram>().AnyAsync(x=>x.InstitutionId==inst&&x.Code==code&&x.Currency==currency&&x.Revision==input.Revision,ct))throw ApiFault.Conflict("Scholarship program revision already exists.");
            var row=new ScholarshipProgram{InstitutionId=inst,ProgramId=input.ProgramId,TermId=input.TermId,Currency=currency,Code=code,Revision=input.Revision,Priority=input.Priority,Category=input.Category,
                Mode=input.Mode,BasisPoints=input.BasisPoints,FixedMinor=input.FixedMinor,FundingLimitMinor=input.FundingLimitMinor,MinimumGpa=input.MinimumGpa,RequireGoodStanding=input.RequireGoodStanding,
                EffectiveFrom=input.EffectiveFrom,EffectiveUntil=input.EffectiveUntil};s.db.Add(row);s.Audit(actor,inst,null,row.Id,"scholarship.published","Immutable assistance program; funding is not collected cash.");return new(row.Id,"Scholarship","Published");
        },ct);
    public async Task<FinanceResult> RequestAward(ClaimsPrincipal user,Guid invoiceId,AwardInput input,CancellationToken ct)
    {
        var original=await Invoice(invoiceId,ct);
        return await AccountWrite(user,original.AccountId,input.RequestId,"AwardRequest",new{invoiceId,input},FinancePermission.Billing,async(s,actor)=>
        {
            var invoice=await s.Invoice(invoiceId,ct);var account=await s.Account(invoice.AccountId,ct);
            if(actor!=account.StudentId)await s.access.Require(user,account.InstitutionId,FinancePermission.Billing,ct);
            var scholarship=await s.db.Set<ScholarshipProgram>().AsNoTracking().SingleOrDefaultAsync(x=>x.Id==input.ScholarshipId&&x.InstitutionId==account.InstitutionId,ct)??throw ApiFault.NotFound();
            await s.CheckScholarshipScope(invoice,account,scholarship,ct);
            if(await s.db.Set<ScholarshipAward>().AnyAsync(x=>x.InvoiceId==invoiceId&&x.ProgramCode==scholarship.Code&&(x.Status=="Requested"||x.Status=="Approved"),ct))throw ApiFault.Conflict("This scholarship program already has an active application/award for this invoice, including another revision.");
            var row=new ScholarshipAward{InstitutionId=account.InstitutionId,InvoiceId=invoiceId,ScholarshipId=scholarship.Id,ProgramCode=scholarship.Code,RequestedBy=actor,Reason=FinanceRules.Text(input.Reason,1000,"award application reason")};s.db.Add(row);
            s.Audit(actor,account.InstitutionId,account.Id,row.Id,"award.requested","No financial reduction until independent approval.");return new(row.Id,"Award",row.Status,account.Id);
        },ct,student:true);
    }
    private async Task CheckScholarshipScope(FinanceInvoice invoice,FinancialAccount account,ScholarshipProgram program,CancellationToken ct)
    {
        FinanceRules.SameCurrency(account.Currency,program.Currency);var policy=await db.Set<FinancialPolicy>().AsNoTracking().SingleAsync(x=>x.Id==invoice.PolicyId,ct);var today=FinanceRules.Today(policy.TimeZoneId);
        if(invoice.Status!="Posted"||program.ProgramId.HasValue&&program.ProgramId!=invoice.ProgramId||program.TermId.HasValue&&program.TermId!=invoice.TermId||today<program.EffectiveFrom||today>=program.EffectiveUntil)
            throw ApiFault.Conflict("Scholarship scope/effective period does not match the posted invoice.");
    }
    public async Task<FinanceResult> AwardAction(ClaimsPrincipal user,Guid id,FinanceAction input,CancellationToken ct)
    {
        var original=await db.Set<ScholarshipAward>().AsNoTracking().SingleOrDefaultAsync(x=>x.Id==id,ct)??throw ApiFault.NotFound();var inv=await Invoice(original.InvoiceId,ct);
        return await AccountWrite(user,inv.AccountId,input.RequestId,"AwardAction",new{id,input},input.Action=="Revoke"?FinancePermission.Reverse:FinancePermission.Approve,async(s,actor)=>
        {
            var row=await s.db.Set<ScholarshipAward>().SingleAsync(x=>x.Id==id,ct);var invoice=await s.Invoice(row.InvoiceId,ct);var account=await s.Account(invoice.AccountId,ct);
            FinanceRules.Version(row.Version,input.ExpectedVersion);var reason=FinanceRules.Text(input.Reason,1000,"award decision reason");
            if(input.Action is "Approve" or "Reject")
            {
                FinanceAccess.Independent(actor,account.StudentId,row.RequestedBy);if(row.Status!="Requested")throw ApiFault.Conflict("Only a requested award can be decided.");
                if(input.Action=="Approve")
                {
                    var program=await s.db.Set<ScholarshipProgram>().AsNoTracking().SingleAsync(x=>x.Id==row.ScholarshipId,ct);await s.CheckScholarshipScope(invoice,account,program,ct);
                    decimal? gpa=null;string? standing=null;Guid? plan=null;
                    if(program.MinimumGpa is >0||program.RequireGoodStanding)await AcademicFreshness.Ensure(s.db,account.InstitutionId,account.StudentId,ct);
                    if(program.MinimumGpa is >0)
                    {
                        var record=await s.curriculum.Gpa(user,account.StudentId,ct);gpa=record.CumulativeGpa;
                        plan=await s.db.StudentCurriculumAssignments.Where(x=>x.InstitutionId==account.InstitutionId&&x.StudentId==account.StudentId).Select(x=>(Guid?)x.PlanId).SingleOrDefaultAsync(ct);
                        if(gpa is null||gpa<program.MinimumGpa)throw ApiFault.Conflict("Authoritative GPA does not satisfy this scholarship.");
                    }
                    if(program.RequireGoodStanding)
                    {
                        var record=await s.db.StudentTermStandings.AsNoTracking().SingleOrDefaultAsync(x=>x.InstitutionId==account.InstitutionId&&x.StudentId==account.StudentId&&x.TermId==invoice.TermId,ct);
                        if(record is null||record.Status!="GoodStanding"||await AcademicFreshness.StandingStale(s.db,account.InstitutionId,account.StudentId,invoice.TermId,record.CalculatedAtUtc,ct))throw ApiFault.Conflict("Current authoritative good standing is required.");standing=record.Status;
                    }
                    row.ApprovalEvidenceJson=JsonSerializer.Serialize(new{ScholarshipId=program.Id,program.Code,program.Revision,program.MinimumGpa,program.RequireGoodStanding,Gpa=gpa,Standing=standing,CurriculumId=plan,
                        InvoiceId=invoice.Id,PricingDigest=CanonicalDigest(invoice.PricingEvidenceJson),ReviewedBy=actor,AtUtc=DateTime.UtcNow});row.Status="Approved";
                }
                else row.Status="Rejected";row.ReviewedBy=actor;
            }
            else if(input.Action=="Revoke"&&row.Status=="Approved")row.Status="Revoked";
            else throw ApiFault.Conflict("Invalid scholarship award transition.");
            row.Version++;await s.db.SaveChangesAsync(ct);await s.RepriceAwards(account,invoice,actor,input.RequestId,ct);
            if(input.Action=="Approve"&&await s.AppliedAid(row.Id,ct)==0)throw ApiFault.Conflict("No eligible charge or funding remains for this award.");
            s.Audit(actor,account.InstitutionId,account.Id,id,"award."+row.Status.ToLowerInvariant(),reason);s.Notify(account,"FinanceAwardReviewed",id,input.RequestId);return new(id,"Award",row.Status,account.Id);
        },ct);
    }
    private async Task<long> AppliedAid(Guid award,CancellationToken ct)=>await db.Set<FinanceJournal>().Where(x=>x.SourceId==award&&(x.Kind=="ScholarshipGranted"||x.Kind=="ScholarshipReclaimed"))
        .SumAsync(x=>x.Kind=="ScholarshipGranted"?x.AmountMinor:-x.AmountMinor,ct);
    private async Task ReclaimAwards(FinancialAccount account,Guid invoice,Guid actor,Guid command,CancellationToken ct)
    {
        var ids=await db.Set<ScholarshipAward>().Where(x=>x.InvoiceId==invoice).Select(x=>x.Id).ToListAsync(ct);
        foreach(var id in ids.Order())
        {var applied=await AppliedAid(id,ct);if(applied>0)await Post(account,"ScholarshipReclaimed",id,$"{command:N}:aid-reclaim:{id:N}",applied,"Receivable","Aid",actor,"Correcting scholarship allocation before deterministic repricing.",ct,invoice);}
    }
    private async Task RepriceAwards(FinancialAccount account,FinanceInvoice invoice,Guid actor,Guid command,CancellationToken ct)
    {
        await db.SaveChangesAsync(ct);await ReclaimAwards(account,invoice.Id,actor,command,ct);
        var awards=await db.Set<ScholarshipAward>().AsNoTracking().Where(x=>x.InvoiceId==invoice.Id&&x.Status=="Approved").ToListAsync(ct);
        var lines=await db.Set<FinanceInvoiceLine>().AsNoTracking().Where(x=>x.InvoiceId==invoice.Id).ToListAsync(ct);
        var adjustments=await db.Set<FinanceAdjustment>().AsNoTracking().Where(x=>x.InvoiceId==invoice.Id).ToListAsync(ct);
        var byCategory=lines.GroupBy(x=>x.Category).ToDictionary(g=>g.Key,g=>FinanceRules.Sum(g.Select(l=>FinanceRules.Sum([l.AmountMinor,FinanceRules.Sum(adjustments.Where(x=>x.InvoiceLineId==l.Id).Select(x=>x.SignedMinor))]))));
        var rules=new List<DiscountRule>();
        foreach(var award in awards)
        {
            var program=await db.Set<ScholarshipProgram>().AsNoTracking().SingleAsync(x=>x.Id==award.ScholarshipId,ct);
            var otherAwards=db.Set<ScholarshipAward>().Where(x=>x.ScholarshipId==program.Id&&x.InvoiceId!=invoice.Id).Select(x=>x.Id);
            var used=program.FundingLimitMinor.HasValue?await db.Set<FinanceJournal>().Where(x=>otherAwards.Contains(x.SourceId)&&(x.Kind=="ScholarshipGranted"||x.Kind=="ScholarshipReclaimed")).SumAsync(x=>x.Kind=="ScholarshipGranted"?x.AmountMinor:-x.AmountMinor,ct):0;
            rules.Add(new(award.Id,program.Priority,program.Code+":"+program.Revision.ToString("D6"),program.Category,program.Mode,program.BasisPoints,program.FixedMinor,program.FundingLimitMinor.HasValue?Math.Max(0,program.FundingLimitMinor.Value-used):FinanceRules.MaximumMinor));
        }
        var amounts=FinanceRules.Discounts(byCategory,rules);
        foreach(var rule in rules.OrderBy(x=>x.Priority).ThenBy(x=>x.Code,StringComparer.Ordinal).ThenBy(x=>x.Id))
        {
            var amount=amounts[rule.Id];if(amount==0)continue;
            await ReleaseAllocations(account,invoice.Id,Math.Max(0,amount-await Outstanding(invoice.Id,ct)),actor,command,ct);
            await Post(account,"ScholarshipGranted",rule.Id,$"{command:N}:aid-grant:{rule.Id:N}",amount,"Aid","Receivable",actor,"Approved scholarship applied in stable priority/code order within remaining charge and funding limits.",ct,invoice.Id);
        }
    }
}
