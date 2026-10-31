using Academix.Api.Data;
using Academix.Api.Domain.Finance;
using Academix.Api.DTOs.Finance;
using Academix.Api.Middleware;
using Microsoft.EntityFrameworkCore;
namespace Academix.Api.Services.Finance;

public sealed record FinanceDebtPosition(FinanceInvoice Invoice,FinancialPolicy Policy,long NetCharges,long Allocated,long Outstanding,
    long PastDue,DateOnly NextDue,Guid? PlanId,IReadOnlyList<InstallmentView> Installments,long Unscheduled);

public static class FinanceDebt
{
    // Batched source reads, also used for historical reports. No stored balances.
    public static async Task<IReadOnlyList<FinanceDebtPosition>> Load(AcademixDbContext db,Guid inst,CancellationToken ct,Guid? account=null,Guid? student=null,DateTime? asOf=null)
    {
        var cutoff=asOf??DateTime.UtcNow;
        var q=db.Set<FinanceInvoice>().AsNoTracking().Where(x=>x.InstitutionId==inst&&x.Status!="Draft"&&x.PostedAtUtc<cutoff);
        if(account.HasValue)q=q.Where(x=>x.AccountId==account);
        if(student.HasValue){var accounts=db.Set<FinancialAccount>().Where(x=>x.InstitutionId==inst&&x.StudentId==student).Select(x=>x.Id);q=q.Where(x=>accounts.Contains(x.AccountId));}
        var invoices=await q.ToListAsync(ct);if(invoices.Count==0)return [];
        var ids=q.Select(x=>x.Id);
        var journals=db.Set<FinanceJournal>().AsNoTracking().Where(x=>x.InstitutionId==inst&&x.PostedAtUtc<cutoff);
        var adjustments=await (from a in db.Set<FinanceAdjustment>().AsNoTracking() join j in journals on a.JournalId equals j.Id where ids.Contains(a.InvoiceId)
            group a by a.InvoiceId into g select new{Id=g.Key,Amount=g.Sum(x=>x.SignedMinor)}).ToDictionaryAsync(x=>x.Id,x=>x.Amount,ct);
        var aid=await journals.Where(x=>x.InvoiceId!=null&&ids.Contains(x.InvoiceId.Value)&&(x.Kind=="ScholarshipGranted"||x.Kind=="ScholarshipReclaimed"))
            .GroupBy(x=>x.InvoiceId!.Value).Select(g=>new{Id=g.Key,Amount=g.Sum(x=>x.Kind=="ScholarshipGranted"?x.AmountMinor:-x.AmountMinor)}).ToDictionaryAsync(x=>x.Id,x=>x.Amount,ct);
        var allocations=await (from a in db.Set<FinanceAllocation>().AsNoTracking() join j in journals on a.JournalId equals j.Id where ids.Contains(a.InvoiceId)
            group a by a.InvoiceId into g select new{Id=g.Key,Amount=g.Sum(x=>x.SignedMinor)}).ToDictionaryAsync(x=>x.Id,x=>x.Amount,ct);
        var plans=await db.Set<FinancePaymentPlan>().AsNoTracking().Where(x=>ids.Contains(x.InvoiceId)&&x.ApprovedAtUtc<cutoff&&(x.SupersededAtUtc==null||x.SupersededAtUtc>=cutoff)).ToListAsync(ct);
        var planIds=plans.Select(x=>x.Id).ToArray();var installments=await db.Set<FinanceInstallment>().AsNoTracking().Where(x=>planIds.Contains(x.PlanId)).OrderBy(x=>x.Position).ToListAsync(ct);
        var policyIds=invoices.Select(x=>x.PolicyId).Distinct().ToArray();var policies=await db.Set<FinancialPolicy>().AsNoTracking().Where(x=>policyIds.Contains(x.Id)).ToDictionaryAsync(x=>x.Id,ct);
        var results=new List<FinanceDebtPosition>();
        foreach(var invoice in invoices)
        {
            var policy=policies[invoice.PolicyId];var today=FinanceRules.Today(policy.TimeZoneId,new DateTimeOffset(DateTime.SpecifyKind(cutoff.AddTicks(-1),DateTimeKind.Utc)));
            var net=FinanceRules.Sum([invoice.TotalMinor,adjustments.GetValueOrDefault(invoice.Id),-aid.GetValueOrDefault(invoice.Id)]);var allocated=allocations.GetValueOrDefault(invoice.Id);var outstanding=FinanceRules.Sum([net,-allocated]);
            if(outstanding<0)throw ApiFault.Conflict("Invoice allocation exceeds its reconciled charges.");
            var plan=plans.SingleOrDefault(x=>x.InvoiceId==invoice.Id);IReadOnlyList<InstallmentView> schedule=[];long extra=0;DateOnly next=invoice.DueDate;long overdue=0;
            if(plan is null)overdue=invoice.DueDate.AddDays(policy.GraceDays)<today?outstanding:0;
            else
            {
                var original=installments.Where(x=>x.PlanId==plan.Id).Select(x=>(x.DueDate,x.AmountMinor)).ToArray();var state=FinanceRules.Settlement(plan.BaseDebtMinor,outstanding,original,today);schedule=state.Lines;extra=state.Extra;
                var unpaid=schedule.Where(x=>x.OutstandingMinor>0).Select(x=>x.DueDate).ToList();if(extra>0)unpaid.Add(invoice.DueDate);
                next=unpaid.Count==0?invoice.DueDate:unpaid.Min();overdue=FinanceRules.Sum(schedule.Where(x=>x.DueDate.AddDays(policy.GraceDays)<today).Select(x=>x.OutstandingMinor));
                if(invoice.DueDate.AddDays(policy.GraceDays)<today)overdue=FinanceRules.Sum([overdue,extra]);
            }
            results.Add(new(invoice,policy,net,allocated,outstanding,overdue,next,plan?.Id,schedule,extra));
        }
        return results;
    }
    public static async Task<IReadOnlyList<string>> Restrictions(AcademixDbContext db,Guid inst,Guid student,string scope,DateTime cutoff,CancellationToken ct)
    {
        var debt=await Load(db,inst,ct,student:student);var reasons=new List<string>();
        foreach(var position in debt)
        {
            var p=position.Policy;var enabled=scope switch{"Registration"=>p.BlockRegistration,"Graduation"=>p.BlockGraduation,"Documents"=>p.BlockDocuments,_=>false};
            var date=FinanceRules.Today(p.TimeZoneId);var from=position.NextDue.AddDays(p.GraceDays+1);
            if(FinanceRules.HoldApplies(enabled,position.PastDue,p.OverdueThresholdMinor,date,from,position.Invoice.PostedAtUtc!.Value,p.PublishedAtUtc,cutoff))
                reasons.Add($"FinancialOverdue:{position.Invoice.Currency}:invoice={position.Invoice.Id}:policy={p.Id}");
        }
        return reasons;
    }
    public static async Task RequireClearance(AcademixDbContext db,Guid inst,Guid student,string scope,DateTime cutoff,CancellationToken ct)
    {if((await Restrictions(db,inst,student,scope,cutoff,ct)).Count>0)throw ApiFault.Conflict("Configured financial clearance is required. Financial statements and payment information remain available.");}
}

public sealed partial class FinanceService
{
    private async Task SyncHolds(Guid accountId,Guid actor,CancellationToken ct)
    {
        var account=await Account(accountId,ct);var positions=await FinanceDebt.Load(db,account.InstitutionId,ct,accountId);
        var open=await db.Set<FinanceHold>().Where(x=>x.AccountId==accountId&&x.Status=="Open").ToListAsync(ct);
        foreach(var position in positions)
        {
            foreach(var scope in new[]{"Registration","Graduation","Documents"})
            {
                var enabled=scope switch{"Registration"=>position.Policy.BlockRegistration,"Graduation"=>position.Policy.BlockGraduation,_=>position.Policy.BlockDocuments};
                var needed=enabled&&position.Outstanding>position.Policy.OverdueThresholdMinor;
                var effective=position.NextDue.AddDays(position.Policy.GraceDays+1);var old=open.SingleOrDefault(x=>x.InvoiceId==position.Invoice.Id&&x.Scope==scope);
                if(old is not null&&(!needed||old.EffectiveFrom!=effective||old.PlanId!=position.PlanId))
                {old.Status="Released";old.ReleasedAtUtc=DateTime.UtcNow;Audit(actor,account.InstitutionId,accountId,old.Id,"hold.released","Underlying debt/approved due schedule changed; prior hold history retained.");await db.SaveChangesAsync(ct);old=null;}
                if(needed&&old is null)
                {
                    var row=new FinanceHold{InstitutionId=account.InstitutionId,AccountId=accountId,InvoiceId=position.Invoice.Id,PolicyId=position.Policy.Id,PlanId=position.PlanId,Scope=scope,
                        EffectiveFrom=effective,Reason=$"Invoice {position.Invoice.Id} has unpaid obligations after the approved due schedule and {position.Policy.GraceDays}-day grace period; threshold {position.Policy.OverdueThresholdMinor} {account.Currency} minor units."};
                    db.Add(row);Audit(actor,account.InstitutionId,accountId,row.Id,"hold.scheduled","Conditional financial hold; active only while its effective date and live overdue threshold are satisfied.");
                }
            }
        }
    }
}
