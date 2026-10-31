using System.Security.Claims;
using System.Text.Json;
using Academix.Api.Domain.Finance;
using Academix.Api.DTOs.Finance;
using Academix.Api.Middleware;
using Academix.Api.Services.Institutional;
using Microsoft.EntityFrameworkCore;
namespace Academix.Api.Services.Finance;

public sealed partial class FinanceService
{
    private sealed record QuoteLine(Guid FeeItemId,string Code,string Category,decimal Quantity,long UnitMinor,long AmountMinor);
    private sealed record BillingQuote(Guid ProgramId,Guid PolicyId,Guid ScheduleId,long Total,string Evidence,IReadOnlyList<QuoteLine> Lines);
    private static string CanonicalDigest(string json)=>DocumentIntegrity.Sign(new byte[32],Guid.Empty,"FINANCE-EVIDENCE-HASH",json);
    private async Task<BillingQuote> Quote(FinancialAccount account,Guid scheduleId,Guid termId,IReadOnlyList<string> optionalCodes,CancellationToken ct)
    {
        if(account.Status!="Active")throw ApiFault.Conflict("Frozen accounts cannot receive new tuition assessments.");
        var policy=await ActivePolicy(account.InstitutionId,account.Currency,ct);var today=FinanceRules.Today(policy.TimeZoneId);
        var membership=await db.StudentProgramMemberships.AsNoTracking().SingleOrDefaultAsync(x=>x.InstitutionId==account.InstitutionId&&x.StudentId==account.StudentId&&x.Status=="Active",ct)??throw ApiFault.Conflict("Active academic program membership required.");
        var term=await db.AcademicTerms.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==termId&&x.InstitutionId==account.InstitutionId&&x.Status=="Active",ct)??throw ApiFault.Conflict("An active institutional academic term is required.");
        var schedule=await db.Set<FeeSchedule>().AsNoTracking().SingleOrDefaultAsync(x=>x.Id==scheduleId&&x.InstitutionId==account.InstitutionId&&x.Status=="Published",ct)??throw ApiFault.NotFound();
        FinanceRules.SameCurrency(schedule.Currency,account.Currency);
        if(schedule.ProgramId!=membership.ProgramId||!FinanceRules.ScheduleScope(schedule.TermId,schedule.AcademicYear,termId,term.StartsOn,schedule.EffectiveFrom,schedule.EffectiveUntil)||today<schedule.EffectiveFrom||today>=schedule.EffectiveUntil)
            throw ApiFault.Conflict("Published fee scope/effective period does not match authoritative student records.");
        var enrollments=await (from e in db.StudentEnrollments.AsNoTracking() join s in db.CourseSections.AsNoTracking() on e.SectionId equals s.Id
            where e.InstitutionId==account.InstitutionId&&s.InstitutionId==account.InstitutionId&&e.StudentId==account.StudentId&&e.Status=="Active"&&s.TermId==termId
            orderby e.Id select new{EnrollmentId=e.Id,s.Id,s.CourseId,e.Status}).ToListAsync(ct);
        if(enrollments.Count==0)throw ApiFault.Conflict("Only authoritative active enrollment can be assessed; proposed/advisory plans cannot be billed.");
        var assigned=await db.StudentCurriculumAssignments.AsNoTracking().Where(x=>x.InstitutionId==account.InstitutionId&&x.StudentId==account.StudentId).Select(x=>(Guid?)x.PlanId).SingleOrDefaultAsync(ct);
        if(assigned.HasValue&&!await db.CurriculumPlans.AnyAsync(x=>x.Id==assigned&&x.InstitutionId==account.InstitutionId&&x.ProgramId==membership.ProgramId&&x.Status=="Published",ct))throw ApiFault.Conflict("Billing credit mapping must use the published assigned curriculum of this program.");
        var credits=await db.CurriculumCourses.AsNoTracking().Where(x=>x.InstitutionId==account.InstitutionId&&x.PlanId==assigned).ToDictionaryAsync(x=>x.CourseId,x=>x.CreditHours,ct);
        var courseIds=enrollments.Select(x=>x.CourseId).Distinct().Order().ToArray();
        var fees=await db.Set<FeeScheduleItem>().AsNoTracking().Where(x=>x.ScheduleId==schedule.Id).OrderBy(x=>x.Code).ToListAsync(ct);
        if(optionalCodes is null||optionalCodes.Count>100)throw ApiFault.Bad("Optional fee selection is required.");
        var selected=optionalCodes.Select(x=>FinanceRules.Text(x,60,"optional fee code").ToUpperInvariant()).ToArray();
        if(selected.Distinct(StringComparer.Ordinal).Count()!=selected.Length||selected.Any(x=>!fees.Any(f=>f.Code==x&&f.Optional)))throw ApiFault.Bad("Select distinct published optional fee codes only.");
        var lines=new List<QuoteLine>();
        foreach(var fee in fees.Where(x=>!x.Optional||selected.Contains(x.Code)))
        {
            var eligible=courseIds.Where(x=>fee.CourseId is null||x==fee.CourseId).ToArray();if(eligible.Length==0)continue;
            if(fee.Mode=="PerCredit"&&eligible.Any(x=>!credits.ContainsKey(x)))throw ApiFault.Conflict("Authoritative curriculum credit mapping is missing for an enrolled course.");
            var quantity=FinanceRules.FeeQuantity(fee.Mode,fee.Mode=="Fixed"?0:eligible.Sum(x=>credits[x]));var amount=FinanceRules.Price(fee.UnitMinor,quantity);
            lines.Add(new(fee.Id,fee.Code,fee.Category,quantity,fee.UnitMinor,amount));
        }
        var total=FinanceRules.Sum(lines.Select(x=>x.AmountMinor));FinanceRules.Money(total);
        var evidence=JsonSerializer.Serialize(new{Schema="ACADEMIX-BILLING-V1",schedule.Id,schedule.Code,schedule.Revision,schedule.AcademicYear,schedule.EffectiveFrom,schedule.EffectiveUntil,PolicyId=policy.Id,PolicyRevision=policy.Revision,
            account.Currency,membership.ProgramId,TermId=termId,AssignedCurriculumId=assigned,Enrollments=enrollments,
            Credits=courseIds.Select(id=>new{CourseId=id,CreditHours=credits.TryGetValue(id,out var c)?(decimal?)c:null}),Lines=lines});
        return new(membership.ProgramId,policy.Id,schedule.Id,total,evidence,lines);
    }
    private void AddInvoiceLines(FinanceInvoice invoice,BillingQuote quote)
        =>db.AddRange(quote.Lines.Select(x=>new FinanceInvoiceLine{InstitutionId=invoice.InstitutionId,InvoiceId=invoice.Id,FeeItemId=x.FeeItemId,Code=x.Code,Category=x.Category,Quantity=x.Quantity,UnitMinor=x.UnitMinor,AmountMinor=x.AmountMinor}));
    public Task<FinanceResult> Assess(ClaimsPrincipal user,Guid accountId,AssessmentInput input,CancellationToken ct)
        =>AccountWrite(user,accountId,input.RequestId,"Assess",input,FinancePermission.Billing,async(s,actor)=>
        {
            var account=await s.Account(accountId,ct);var quote=await s.Quote(account,input.ScheduleId,input.TermId,input.OptionalCodes,ct);
            if(input.DueDate.Year is <2000 or >2200)throw ApiFault.Bad("Invoice due date must be within 2000-2200.");
            var row=await s.db.Set<FinanceInvoice>().SingleOrDefaultAsync(x=>x.AccountId==accountId&&x.TermId==input.TermId&&x.Scope=="Tuition",ct);
            if(row is not null)
            {
                if(row.Status!="Draft")throw ApiFault.Conflict("Tuition for this account and term is already posted or reversed; use correcting entries.");
                if(CanonicalDigest(row.PricingEvidenceJson)==CanonicalDigest(quote.Evidence)&&row.DueDate==input.DueDate)return new(row.Id,"Invoice",row.Status,accountId);
                s.Audit(actor,account.InstitutionId,accountId,row.Id,"invoice.reassessed","Previous evidence digest "+CanonicalDigest(row.PricingEvidenceJson));
                await s.db.Set<FinanceInvoiceLine>().Where(x=>x.InvoiceId==row.Id).ExecuteDeleteAsync(ct);row.Version++;
            }
            else {row=new(){InstitutionId=account.InstitutionId,AccountId=accountId,Currency=account.Currency,TermId=input.TermId};s.db.Add(row);}
            row.ProgramId=quote.ProgramId;row.ScheduleId=quote.ScheduleId;row.PolicyId=quote.PolicyId;row.TotalMinor=quote.Total;row.PricingEvidenceJson=quote.Evidence;
            row.OptionalCodesJson=JsonSerializer.Serialize(input.OptionalCodes.Select(x=>x.Trim().ToUpperInvariant()).Order());row.DueDate=input.DueDate;s.AddInvoiceLines(row,quote);
            s.Audit(actor,account.InstitutionId,accountId,row.Id,"invoice.assessed","Draft; pricing/effective version preserved; no journal posted.");return new(row.Id,"Invoice",row.Status,accountId);
        },ct);
    public async Task<FinanceResult> InvoiceAction(ClaimsPrincipal user,Guid id,FinanceAction input,CancellationToken ct)
    {
        var original=await Invoice(id,ct);var permission=input.Action=="Reverse"?FinancePermission.Reverse:FinancePermission.Billing;
        return await AccountWrite(user,original.AccountId,input.RequestId,"InvoiceAction",new{id,input},permission,async(s,actor)=>
        {
            var invoice=await s.db.Set<FinanceInvoice>().SingleAsync(x=>x.Id==id,ct);var account=await s.Account(invoice.AccountId,ct);
            if(input.Action=="Post")
            {
                if(invoice.Status=="Posted")return new(id,"Invoice","Posted",account.Id);
                FinanceRules.Version(invoice.Version,input.ExpectedVersion);if(invoice.Status!="Draft")throw ApiFault.Conflict("Only a draft invoice can post.");
                var codes=JsonSerializer.Deserialize<string[]>(invoice.OptionalCodesJson)!;var current=await s.Quote(account,invoice.ScheduleId,invoice.TermId,codes,ct);
                if(CanonicalDigest(current.Evidence)!=CanonicalDigest(invoice.PricingEvidenceJson))throw ApiFault.Conflict("Pricing/academic evidence changed; reassess the draft before posting.");
                invoice.Status="Posted";invoice.PostedAtUtc=DateTime.UtcNow;invoice.Version++;
                await s.Post(account,"InvoicePosted",id,$"{id:N}:invoice",invoice.TotalMinor,"Receivable","Revenue",actor,input.Reason,ct,id);
                s.Notify(account,"FinanceInvoicePosted",id);
            }
            else if(input.Action=="Reverse")
            {
                if(invoice.Status=="Reversed")return new(id,"Invoice","Reversed",account.Id);
                FinanceRules.Version(invoice.Version,input.ExpectedVersion);if(invoice.Status!="Posted")throw ApiFault.Conflict("Only posted invoices can be reversed.");
                await s.ReclaimAwards(account,invoice.Id,actor,input.RequestId,ct);
                var awards=await s.db.Set<ScholarshipAward>().Where(x=>x.InvoiceId==id&&x.Status=="Approved").ToListAsync(ct);
                foreach(var award in awards){award.Status="Revoked";award.Version++;s.Audit(actor,account.InstitutionId,account.Id,award.Id,"award.revoked","Underlying invoice reversed.");}
                await s.ReleaseAllocations(account,id,await s.Allocated(id,ct),actor,input.RequestId,ct);
                var lines=await s.db.Set<FinanceInvoiceLine>().Where(x=>x.InvoiceId==id).ToListAsync(ct);
                foreach(var line in lines)
                {
                    var baseAmount=FinanceRules.Sum([line.AmountMinor,await s.db.Set<FinanceAdjustment>().Where(x=>x.InvoiceLineId==line.Id).SumAsync(x=>x.SignedMinor,ct)]);if(baseAmount==0)continue;
                    var journal=await s.Post(account,"CreditNote",id,$"{input.RequestId:N}:reverse:{line.Id:N}",baseAmount,"Revenue","Receivable",actor,input.Reason,ct,id);
                    s.db.Add(new FinanceAdjustment{InstitutionId=account.InstitutionId,InvoiceId=id,InvoiceLineId=line.Id,JournalId=journal,SignedMinor=-baseAmount});await s.db.SaveChangesAsync(ct);
                }
                await s.SupersedePlan(id,ct);invoice.Status="Reversed";invoice.Version++;s.Notify(account,"FinanceInvoiceReversed",id);
            }
            else throw ApiFault.Bad("Invoice action must be Post or Reverse; a changed draft is reassessed with assessment input.");
            s.Audit(actor,account.InstitutionId,account.Id,id,"invoice."+invoice.Status.ToLowerInvariant(),FinanceRules.Text(input.Reason,1000,"reason"));return new(id,"Invoice",invoice.Status,account.Id);
        },ct);
    }
    public async Task<FinanceResult> Adjust(ClaimsPrincipal user,Guid invoiceId,AdjustmentInput input,CancellationToken ct)
    {
        var original=await Invoice(invoiceId,ct);
        return await AccountWrite(user,original.AccountId,input.RequestId,"Adjustment",new{invoiceId,input},FinancePermission.Billing,async(s,actor)=>
        {
            var invoice=await s.db.Set<FinanceInvoice>().SingleAsync(x=>x.Id==invoiceId,ct);var account=await s.Account(invoice.AccountId,ct);
            FinanceRules.Version(invoice.Version,input.ExpectedVersion);FinanceRules.Money(input.AmountMinor);FinanceRules.SameCurrency(input.Currency,account.Currency);
            var restoring=invoice.Status=="Reversed"&&input.Direction=="Debit";
            if(restoring)await s.access.Require(user,account.InstitutionId,FinancePermission.Reverse,ct);
            if(invoice.Status!="Posted"&&!restoring||input.Direction is not ("Debit" or "Credit")||input.Direction=="Debit"&&account.Status!="Active")throw ApiFault.Conflict("Invalid adjustment or restricted account.");
            var line=await s.db.Set<FinanceInvoiceLine>().SingleOrDefaultAsync(x=>x.Id==input.InvoiceLineId&&x.InvoiceId==invoiceId,ct)??throw ApiFault.NotFound();
            var current=FinanceRules.Sum([line.AmountMinor,await s.db.Set<FinanceAdjustment>().Where(x=>x.InvoiceLineId==line.Id).SumAsync(x=>x.SignedMinor,ct)]);
            if(input.Direction=="Credit"&&input.AmountMinor>current)throw ApiFault.Conflict("Credit note cannot exceed this charge's remaining gross liability.");
            await s.ReclaimAwards(account,invoiceId,actor,input.RequestId,ct);
            if(input.Direction=="Credit")await s.ReleaseAllocations(account,invoiceId,Math.Max(0,input.AmountMinor-await s.Outstanding(invoiceId,ct)),actor,input.RequestId,ct);
            var debit=input.Direction=="Debit";var journal=await s.Post(account,debit?"DebitAdjustment":"CreditNote",input.InvoiceLineId,$"{input.RequestId:N}:adjust",input.AmountMinor,debit?"Receivable":"Revenue",debit?"Revenue":"Receivable",actor,input.Reason,ct,invoiceId);
            var row=new FinanceAdjustment{InstitutionId=account.InstitutionId,InvoiceId=invoiceId,InvoiceLineId=line.Id,JournalId=journal,SignedMinor=debit?input.AmountMinor:-input.AmountMinor};s.db.Add(row);if(restoring)invoice.Status="Posted";invoice.Version++;await s.db.SaveChangesAsync(ct);
            await s.RepriceAwards(account,invoice,actor,input.RequestId,ct);
            s.Audit(actor,account.InstitutionId,account.Id,row.Id,"invoice.adjusted",FinanceRules.Text(input.Reason,1000,"adjustment reason"));s.Notify(account,"FinanceInvoiceAdjusted",row.Id);return new(row.Id,"Adjustment","Posted",account.Id);
        },ct);
    }
}
