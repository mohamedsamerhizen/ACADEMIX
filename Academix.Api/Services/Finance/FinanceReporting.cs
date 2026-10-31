using System.Security.Claims;
using Academix.Api.Domain.Finance;
using Academix.Api.DTOs.Finance;
using Academix.Api.DTOs.Workspaces;
using Academix.Api.Middleware;
using Microsoft.EntityFrameworkCore;
namespace Academix.Api.Services.Finance;

public sealed partial class FinanceService
{
    public Task<FinanceReport> Report(ClaimsPrincipal user,Guid inst,DateOnly from,DateOnly until,CancellationToken ct,int page=1)
        =>retry.ExecuteAsync<FinanceService,FinanceReport>(async s=>
        {
            FinanceRules.Dates(from,until);var offset=FinanceRules.Offset(page);if(until.DayNumber-from.DayNumber>3660)throw ApiFault.Bad("Reporting period must be at most ten years.");
            await using var tx=await s.db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead,ct);await s.access.Require(user,inst,FinancePermission.Report,ct);
            var zones=await s.db.Set<FinancialPolicy>().Where(x=>x.InstitutionId==inst).Select(x=>x.TimeZoneId).Distinct().ToListAsync(ct);
            if(zones.Count>1)throw ApiFault.Conflict("Institution financial time zone is inconsistent.");var zone=zones.SingleOrDefault()??"UTC";
            var start=FinanceRules.UtcStart(from,zone);var end=FinanceRules.UtcStart(until,zone);var asOf=end<DateTime.UtcNow?end:DateTime.UtcNow;
            var journals=s.db.Set<FinanceJournal>().AsNoTracking().Where(x=>x.InstitutionId==inst&&x.PostedAtUtc<asOf);var flows=journals.Where(x=>x.PostedAtUtc>=start);
            var amounts=await flows.GroupBy(x=>new{x.Currency,x.Kind}).Select(g=>new{g.Key.Currency,g.Key.Kind,Amount=g.Sum(x=>(decimal)x.AmountMinor)}).ToListAsync(ct);
            var balances=await (from l in s.db.Set<FinanceJournalLine>().AsNoTracking() join j in journals on l.JournalId equals j.Id
                group l by new{j.Currency,l.Bucket} into g select new{g.Key.Currency,g.Key.Bucket,Amount=g.Sum(x=>(decimal)x.SignedMinor)}).ToListAsync(ct);
            var allocations=from a in s.db.Set<FinanceAllocation>().AsNoTracking() join j in flows on a.JournalId equals j.Id
                join i in s.db.Set<FinanceInvoice>().AsNoTracking() on a.InvoiceId equals i.Id where i.InstitutionId==inst select new{a.Currency,i.TermId,i.ProgramId,a.SignedMinor};
            var terms=await allocations.GroupBy(x=>new{x.Currency,x.TermId}).Select(g=>new{g.Key.Currency,g.Key.TermId,Amount=g.Sum(x=>(decimal)x.SignedMinor)}).ToListAsync(ct);
            var programs=await allocations.GroupBy(x=>new{x.Currency,x.ProgramId}).Select(g=>new{g.Key.Currency,g.Key.ProgramId,Amount=g.Sum(x=>(decimal)x.SignedMinor)}).ToListAsync(ct);
            var methods=await (from j in flows join p in s.db.Set<FinancePayment>().AsNoTracking() on j.PaymentId equals (Guid?)p.Id
                where j.Kind=="PaymentPosted"||j.Kind=="PaymentReversed" group j by new{j.Currency,p.Method} into g
                select new{g.Key.Currency,g.Key.Method,Amount=g.Sum(x=>x.Kind=="PaymentPosted"?(decimal)x.AmountMinor:-x.AmountMinor)}).ToListAsync(ct);
            var positions=await FinanceDebt.Load(s.db,inst,ct,asOf:asOf);var currencies=amounts.Select(x=>x.Currency).Concat(balances.Select(x=>x.Currency)).Distinct().Order(StringComparer.Ordinal).ToArray();
            var reports=new List<CurrencyFinanceReport>();
            PageResult<ReportGroup> Breakdown(IEnumerable<ReportGroup> rows)
            {var all=rows.ToArray();return new(page,50,all.Length,all.Skip(offset).Take(50).ToArray());}
            foreach(var currency in currencies)
            {
                var net=FinanceRules.NetFlows(amounts.Where(x=>x.Currency==currency).ToDictionary(x=>x.Kind,x=>x.Amount));
                decimal Balance(string bucket)=>balances.Where(x=>x.Currency==currency&&x.Bucket==bucket).Sum(x=>x.Amount);
                var aging=new Dictionary<string,decimal>{{"NotDue",0},{"1-30",0},{"31-60",0},{"61-90",0},{"Over90",0}};
                foreach(var position in positions.Where(x=>x.Invoice.Currency==currency))
                {
                    var today=FinanceRules.Today(position.Policy.TimeZoneId,new DateTimeOffset(asOf.AddTicks(-1),TimeSpan.Zero));
                    void Add(DateOnly due,long amount)
                    {var days=today.DayNumber-due.DayNumber;var key=days<=position.Policy.GraceDays?"NotDue":days<=30?"1-30":days<=60?"31-60":days<=90?"61-90":"Over90";aging[key]+=amount;}
                    if(position.PlanId is null)Add(position.Invoice.DueDate,position.Outstanding);
                    else{foreach(var installment in position.Installments)Add(installment.DueDate,installment.OutstandingMinor);Add(position.Invoice.DueDate,position.Unscheduled);}
                }
                reports.Add(new(currency,net.Assessed,net.Aid,net.Collected,net.Refunded,Balance("Receivable"),-Balance("StudentCredit"),aging.Where(x=>x.Key!="NotDue").Sum(x=>x.Value),
                    aging.Select(x=>new ReportGroup(x.Key,x.Value)).ToArray(),Breakdown(terms.Where(x=>x.Currency==currency).OrderBy(x=>x.TermId).Select(x=>new ReportGroup(x.TermId.ToString(),x.Amount))),
                    Breakdown(programs.Where(x=>x.Currency==currency).OrderBy(x=>x.ProgramId).Select(x=>new ReportGroup(x.ProgramId.ToString(),x.Amount))),Breakdown(methods.Where(x=>x.Currency==currency).OrderBy(x=>x.Method).Select(x=>new ReportGroup(x.Method,x.Amount)))));
            }
            await tx.CommitAsync(ct);return new(from,until,asOf,reports);
        },ct);
}
