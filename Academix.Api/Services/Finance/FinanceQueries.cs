using System.Security.Claims;
using System.Text.Json;
using Academix.Api.Domain.Finance;
using Academix.Api.Domain.Identity;
using Academix.Api.DTOs.Finance;
using Academix.Api.DTOs.Workspaces;
using Academix.Api.Middleware;
using Microsoft.EntityFrameworkCore;
namespace Academix.Api.Services.Finance;

public sealed partial class FinanceService
{
    private static AccountView AccountView(FinancialAccount a)=>new(a.Id,a.StudentId,a.Currency,FinanceRules.Currencies[a.Currency],a.Status,a.Version);
    public async Task<PageResult<AccountView>> Accounts(ClaimsPrincipal user,Guid? inst,Guid? student,string? currency,int page,CancellationToken ct)
    {
        var offset=FinanceRules.Offset(page);var (actor,roles)=await security.CurrentAsync(user,ct);
        Guid institution;
        if(inst.HasValue){institution=inst.Value;await access.Require(user,institution,FinancePermission.Report,ct);}
        else {institution=actor.InstitutionId??throw ApiFault.Forbidden();if(!roles.Contains(AcademixRoles.Student))throw ApiFault.Forbidden();student=actor.Id;}
        if(currency is not null)currency=FinanceRules.Currency(currency);
        var q=db.Set<FinancialAccount>().AsNoTracking().Where(x=>x.InstitutionId==institution&&(!student.HasValue||x.StudentId==student)&&(currency==null||x.Currency==currency));
        return new(page,50,await q.CountAsync(ct),(await q.OrderBy(x=>x.StudentId).ThenBy(x=>x.Currency).Skip(offset).Take(50).ToListAsync(ct)).Select(AccountView).ToArray());
    }
    private Task<T> ReadSnapshot<T>(Func<FinanceService,Task<T>> read,CancellationToken ct)
        =>retry.ExecuteAsync<FinanceService,T>(async s=>
        {await using var tx=await s.db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead,ct);var result=await read(s);await tx.CommitAsync(ct);return result;},ct);
    public Task<InvoiceView> InvoiceDetail(ClaimsPrincipal user,Guid id,CancellationToken ct)
        =>ReadSnapshot(s=>s.InvoiceDetailCore(user,id,ct),ct);
    private async Task<InvoiceView> InvoiceDetailCore(ClaimsPrincipal user,Guid id,CancellationToken ct)
    {
        var row=await Invoice(id,ct);await access.ReadAccount(user,await Account(row.AccountId,ct),ct);
        var lines=await db.Set<FinanceInvoiceLine>().AsNoTracking().Where(x=>x.InvoiceId==id).OrderBy(x=>x.Code).Select(x=>new InvoiceLineView(x.Id,x.Code,x.Category,x.Quantity,x.UnitMinor,x.AmountMinor)).ToListAsync(ct);
        return new(row.Id,row.AccountId,row.Currency,row.ProgramId,row.TermId,row.ScheduleId,row.PolicyId,row.Status,row.Version,row.DueDate,row.TotalMinor,
            row.Status=="Draft"?0:await NetCharges(id,ct),await Allocated(id,ct),row.Status=="Draft"?0:await Outstanding(id,ct),row.PricingEvidenceJson,lines);
    }
    private async Task<AccountBalances> Balances(FinancialAccount account,CancellationToken ct)
    {
        var ledger=await Ledger(account.Id,ct);var debt=ledger.GetValueOrDefault("Receivable");var credit=-ledger.GetValueOrDefault("StudentCredit");
        var reserved=await db.Set<FinanceRefund>().Where(x=>x.AccountId==account.Id&&(x.Status=="Requested"||x.Status=="Approved")).SumAsync(x=>x.AmountMinor,ct);
        var policy=await AccountPolicy(account,ct);var refundable=account.Status=="Active"&&policy.AllowRefunds&&(!policy.BlockRefundWithDebt||debt==0)?Math.Max(0,credit-reserved):0;
        return new(debt,credit,ledger.GetValueOrDefault("Funds"),reserved,refundable);
    }
    public Task<StatementView> Statement(ClaimsPrincipal user,Guid accountId,int page,DateOnly? from,DateOnly? until,CancellationToken ct)
        =>retry.ExecuteAsync<FinanceService,StatementView>(async s=>
        {
            await using var tx=await s.db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead,ct);
            var account=await s.Account(accountId,ct);await s.access.ReadAccount(user,account,ct);var offset=FinanceRules.Offset(page);var policy=await s.AccountPolicy(account,ct);
            var q=s.db.Set<FinanceJournal>().AsNoTracking().Where(x=>x.AccountId==accountId);
            if(from.HasValue){var start=FinanceRules.UtcStart(from.Value,policy.TimeZoneId);q=q.Where(x=>x.PostedAtUtc>=start);}
            if(until.HasValue){var end=FinanceRules.UtcStart(until.Value,policy.TimeZoneId);q=q.Where(x=>x.PostedAtUtc<end);}
            if(from.HasValue&&until.HasValue)FinanceRules.Dates(from.Value,until.Value);
            var total=await q.CountAsync(ct);var journals=await q.OrderByDescending(x=>x.PostedAtUtc).ThenBy(x=>x.Id).Skip(offset).Take(50).ToListAsync(ct);var ids=journals.Select(x=>x.Id).ToArray();
            var legs=await s.db.Set<FinanceJournalLine>().AsNoTracking().Where(x=>ids.Contains(x.JournalId)).ToListAsync(ct);
            var views=journals.Select(x=>new JournalView(x.Id,x.Kind,x.SourceId,x.InvoiceId,x.PaymentId,x.AmountMinor,x.Reason,x.PostedAtUtc,legs.Where(l=>l.JournalId==x.Id).OrderBy(l=>l.Bucket).Select(l=>new LedgerLeg(l.Bucket,l.SignedMinor)).ToArray())).ToArray();
            var positions=(await FinanceDebt.Load(s.db,account.InstitutionId,ct,accountId)).ToDictionary(x=>x.Invoice.Id);
            var hq=s.db.Set<FinanceHold>().AsNoTracking().Where(x=>x.AccountId==accountId);var htotal=await hq.CountAsync(ct);
            var holds=await hq.OrderByDescending(x=>x.CreatedAtUtc).ThenBy(x=>x.Id).Skip(offset).Take(50).ToListAsync(ct);
            var holdViews=holds.Select(h=>
            {
                positions.TryGetValue(h.InvoiceId,out var position);var status=h.Status;
                if(status=="Open"&&position is not null){var today=FinanceRules.Today(position.Policy.TimeZoneId);status=today<h.EffectiveFrom?"Scheduled":position.PastDue>position.Policy.OverdueThresholdMinor?"Active":"Conditional";}
                return new HoldView(h.Id,h.InvoiceId,h.PolicyId,h.Scope,status,h.EffectiveFrom,h.Reason,position?.PastDue??0);
            }).ToArray();
            var result=new StatementView(AccountView(account),await s.Balances(account,ct),new(page,50,total,views),new(page,50,htotal,holdViews));await tx.CommitAsync(ct);return result;
        },ct);
    private static async Task<PageResult<T>> Page<T>(IQueryable<T> query,int page,CancellationToken ct)
        =>new(page,50,await query.CountAsync(ct),await query.Skip(FinanceRules.Offset(page)).Take(50).ToListAsync(ct));
    public Task<object> History(ClaimsPrincipal user,Guid accountId,string kind,int page,CancellationToken ct)
        =>ReadSnapshot(s=>s.HistoryCore(user,accountId,kind,page,ct),ct);
    private async Task<object> HistoryCore(ClaimsPrincipal user,Guid accountId,string kind,int page,CancellationToken ct)
    {
        var account=await Account(accountId,ct);await access.ReadAccount(user,account,ct);FinanceRules.Offset(page);
        var invoices=db.Set<FinanceInvoice>().Where(x=>x.AccountId==accountId).Select(x=>x.Id);
        if(kind=="Invoices")return await Page(db.Set<FinanceInvoice>().AsNoTracking().Where(x=>x.AccountId==accountId).OrderByDescending(x=>x.CreatedAtUtc).ThenBy(x=>x.Id),page,ct);
        if(kind=="Payments")return await Page(db.Set<FinancePayment>().AsNoTracking().Where(x=>x.AccountId==accountId).OrderByDescending(x=>x.CreatedAtUtc).ThenBy(x=>x.Id),page,ct);
        if(kind=="Refunds")return await Page(db.Set<FinanceRefund>().AsNoTracking().Where(x=>x.AccountId==accountId).OrderBy(x=>x.Id).Select(x=>new RefundView(x.Id,x.PaymentId,x.AmountMinor,x.Currency,x.Status,x.Version,x.Reason,x.ExecutionReference)),page,ct);
        if(kind=="Awards")
        {
            var q=db.Set<ScholarshipAward>().AsNoTracking().Where(x=>invoices.Contains(x.InvoiceId));var count=await q.CountAsync(ct);var rows=await q.OrderBy(x=>x.Id).Skip(FinanceRules.Offset(page)).Take(50).ToListAsync(ct);
            var ids=rows.Select(x=>x.Id).ToArray();var amounts=await db.Set<FinanceJournal>().Where(x=>ids.Contains(x.SourceId)&&(x.Kind=="ScholarshipGranted"||x.Kind=="ScholarshipReclaimed")).GroupBy(x=>x.SourceId)
                .Select(g=>new{Id=g.Key,Amount=g.Sum(x=>x.Kind=="ScholarshipGranted"?x.AmountMinor:-x.AmountMinor)}).ToDictionaryAsync(x=>x.Id,x=>x.Amount,ct);
            return new PageResult<AwardView>(page,50,count,rows.Select(x=>new AwardView(x.Id,x.InvoiceId,x.ScholarshipId,x.Status,x.Version,amounts.GetValueOrDefault(x.Id),x.ApprovalEvidenceJson)).ToArray());
        }
        if(kind=="Plans")
        {
            var q=db.Set<FinancePaymentPlan>().AsNoTracking().Where(x=>invoices.Contains(x.InvoiceId));var count=await q.CountAsync(ct);var rows=await q.OrderByDescending(x=>x.CreatedAtUtc).ThenBy(x=>x.Id).Skip(FinanceRules.Offset(page)).Take(50).ToListAsync(ct);
            var planIds=rows.Select(x=>x.Id).ToArray();var schedule=await db.Set<FinanceInstallment>().AsNoTracking().Where(x=>planIds.Contains(x.PlanId)).OrderBy(x=>x.Position).ToListAsync(ct);
            var positions=(await FinanceDebt.Load(db,account.InstitutionId,ct,accountId)).ToDictionary(x=>x.Invoice.Id);var today=FinanceRules.Today((await AccountPolicy(account,ct)).TimeZoneId);
            return new PageResult<PaymentPlanView>(page,50,count,rows.Select(p=>{var state=FinanceRules.Settlement(p.BaseDebtMinor,positions[p.InvoiceId].Outstanding,schedule.Where(x=>x.PlanId==p.Id).Select(x=>(x.DueDate,x.AmountMinor)).ToArray(),today);
                return new PaymentPlanView(p.Id,p.Revision,p.Status,p.Version,p.BaseDebtMinor,p.Reason,state.Lines,state.Extra);}).ToArray());
        }
        if(kind=="Audit")return await Page(db.Set<FinanceAuditEvent>().AsNoTracking().Where(x=>x.AccountId==accountId).OrderByDescending(x=>x.AtUtc).ThenBy(x=>x.Id),page,ct);
        throw ApiFault.Bad("History kind must be Invoices, Payments, Refunds, Awards, Plans or Audit.");
    }
    public async Task<object> Catalog(ClaimsPrincipal user,Guid inst,string kind,int page,CancellationToken ct)
    {
        FinanceRules.Offset(page);var (actor,roles)=await security.CurrentAsync(user,ct);
        var student=kind=="Scholarships"&&actor.InstitutionId==inst&&roles.Contains(AcademixRoles.Student);
        if(kind=="Authorities")await security.RequireAdminAsync(user,inst,ct);
        else if(!student)await access.Require(user,inst,FinancePermission.None,ct);
        if(kind=="Authorities")return await Page(db.Set<FinanceAuthority>().AsNoTracking().Where(x=>x.InstitutionId==inst).OrderBy(x=>x.UserId),page,ct);
        if(kind=="Policies")return await Page(db.Set<FinancialPolicy>().AsNoTracking().Where(x=>x.InstitutionId==inst).OrderBy(x=>x.Currency).ThenByDescending(x=>x.Revision),page,ct);
        if(kind=="Schedules")
        {
            var q=db.Set<FeeSchedule>().AsNoTracking().Where(x=>x.InstitutionId==inst);var count=await q.CountAsync(ct);var rows=await q.OrderBy(x=>x.Code).ThenByDescending(x=>x.Revision).Skip(FinanceRules.Offset(page)).Take(50).ToListAsync(ct);
            var ids=rows.Select(x=>x.Id).ToArray();var items=await db.Set<FeeScheduleItem>().AsNoTracking().Where(x=>ids.Contains(x.ScheduleId)).OrderBy(x=>x.Code).ToListAsync(ct);
            return new PageResult<object>(page,50,count,rows.Select(x=>(object)new{Schedule=x,Items=items.Where(i=>i.ScheduleId==x.Id).ToArray()}).ToArray());
        }
        if(kind=="Scholarships")return await Page(db.Set<ScholarshipProgram>().AsNoTracking().Where(x=>x.InstitutionId==inst).OrderBy(x=>x.Code).ThenByDescending(x=>x.Revision)
            .Select(x=>new{x.Id,x.Code,x.Revision,x.Currency,x.ProgramId,x.TermId,x.Priority,x.Category,x.Mode,x.BasisPoints,x.FixedMinor,x.MinimumGpa,x.RequireGoodStanding,x.EffectiveFrom,x.EffectiveUntil,FundingLimitMinor=student?null:x.FundingLimitMinor}),page,ct);
        throw ApiFault.Bad("Catalog kind must be Authorities, Policies, Schedules or Scholarships.");
    }
    public Task<AccountReconciliation> ReconcileAccount(ClaimsPrincipal user,Guid accountId,CancellationToken ct)
        =>retry.ExecuteAsync<FinanceService,AccountReconciliation>(async s=>
        {
            await using var tx=await s.db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead,ct);var account=await s.Account(accountId,ct);await s.access.ReadAccount(user,account,ct);
            var ledger=await s.Ledger(accountId,ct);var positions=await FinanceDebt.Load(s.db,account.InstitutionId,ct,accountId);
            var collected=await s.db.Set<FinancePayment>().Where(x=>x.AccountId==accountId&&x.Status=="Posted").SumAsync(x=>(decimal)x.AmountMinor,ct);
            var refunded=await s.db.Set<FinanceRefund>().Where(x=>x.AccountId==accountId&&x.Status=="Executed").SumAsync(x=>(decimal)x.AmountMinor,ct);var allocations=await s.db.Set<FinanceAllocation>().Where(x=>x.AccountId==accountId).SumAsync(x=>x.SignedMinor,ct);
            var ar=FinanceRules.Sum([ledger.GetValueOrDefault("Receivable"),-FinanceRules.Sum(positions.Select(x=>x.Outstanding))]);var funds=(decimal)ledger.GetValueOrDefault("Funds")-collected+refunded;
            var credit=-(decimal)ledger.GetValueOrDefault("StudentCredit")-collected+allocations+refunded;
            if(funds<long.MinValue||funds>long.MaxValue||credit<long.MinValue||credit>long.MaxValue)throw ApiFault.Conflict("Account reconciliation discrepancy exceeds supported bounds.");
            var result=new AccountReconciliation(accountId,account.Currency,ar==0&&funds==0&&credit==0,ar,(long)funds,(long)credit);await tx.CommitAsync(ct);return result;
        },ct);
    public Task<FinanceResult> ReconcileBank(ClaimsPrincipal user,Guid inst,BankReconciliationInput input,CancellationToken ct)
        =>Write(user,inst,input.RequestId,"BankReconciliation",input,FinancePermission.Report,null,async(s,actor)=>
        {
            if(input.Rows is null||input.Rows.Count is <1 or >500)throw ApiFault.Bad("Provide 1-500 bank/reference credit rows.");
            var source=FinanceRules.Text(input.SourceName,160,"provided data source");var seen=new HashSet<string>(StringComparer.Ordinal);var normalized=new List<ProvidedBankRow>();
            foreach(var row in input.Rows){if(row is null)throw ApiFault.Bad("A provided reconciliation row is required.");var reference=FinanceRules.Reference(row.Reference);FinanceRules.Money(row.AmountMinor);if(!seen.Add(reference))throw ApiFault.Bad("Duplicate reference in provided reconciliation rows.");normalized.Add(row with{Reference=reference,Currency=FinanceRules.Currency(row.Currency)});}
            var refs=normalized.Select(x=>x.Reference).ToArray();var payments=await s.db.Set<FinancePayment>().AsNoTracking().Where(x=>x.InstitutionId==inst&&refs.Contains(x.Reference)&&x.Status!="Rejected").ToDictionaryAsync(x=>x.Reference,ct);
            var matches=normalized.Select(row=>{payments.TryGetValue(row.Reference,out var p);var status=p is null?"MissingInternal":p.Currency!=row.Currency?"CurrencyMismatch":p.AmountMinor!=row.AmountMinor?"AmountMismatch":p.Status=="Reversed"?"ReversedInternal":p.Status!="Posted"?"PendingInternalVerification":"MatchedProvidedData";
                return new BankMatch(row.Reference,row.Currency,row.AmountMinor,status,p?.Id);}).ToArray();
            var result=new FinanceReconciliation{InstitutionId=inst,ActorId=actor,SourceName=source,ProvidedRowsJson=JsonSerializer.Serialize(normalized),ResultsJson=JsonSerializer.Serialize(matches)};s.db.Add(result);
            s.Audit(actor,inst,null,result.Id,"reconciliation.recorded","Comparison to user-provided reference data only; no bank confirmation inferred and no monetary posting.");return new(result.Id,"Reconciliation","Compared");
        },ct);
    public async Task<FinanceReconciliation> BankReconciliation(ClaimsPrincipal user,Guid id,CancellationToken ct)
    {var row=await db.Set<FinanceReconciliation>().AsNoTracking().SingleOrDefaultAsync(x=>x.Id==id,ct)??throw ApiFault.NotFound();await access.Require(user,row.InstitutionId,FinancePermission.Report,ct);return row;}
}
