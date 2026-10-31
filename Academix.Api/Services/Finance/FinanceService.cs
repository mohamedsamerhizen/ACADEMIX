using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Academix.Api.Data;
using Academix.Api.Domain.Finance;
using Academix.Api.Domain.Identity;
using Academix.Api.DTOs.Finance;
using Academix.Api.Middleware;
using Academix.Api.Services.Academic;
using Academix.Api.Services.Identity;
using Academix.Api.Services.Infrastructure;
using Academix.Api.Services.Institutional;
using Academix.Api.Services.Curriculum;
using Microsoft.EntityFrameworkCore;
namespace Academix.Api.Services.Finance;

public sealed partial class FinanceService
{
    private readonly AcademixDbContext db;
    private readonly IdentitySecurity security;
    private readonly FinanceAccess access;
    private readonly RetryingServiceWork retry;
    private readonly DocumentKeyService documentKeys;
    private readonly DocumentPdfRenderer pdfRenderer;
    private readonly CurriculumService curriculum;
    public FinanceService(AcademixDbContext db,IdentitySecurity security,FinanceAccess access,RetryingServiceWork retry,
        DocumentKeyService documentKeys,DocumentPdfRenderer pdfRenderer,CurriculumService curriculum)
    {this.db=db;this.security=security;this.access=access;this.retry=retry;this.documentKeys=documentKeys;this.pdfRenderer=pdfRenderer;this.curriculum=curriculum;}
    private async Task<FinancialAccount> Account(Guid id,CancellationToken ct)=>await db.Set<FinancialAccount>().AsNoTracking().SingleOrDefaultAsync(x=>x.Id==id,ct)??throw ApiFault.NotFound();
    private async Task<FinanceInvoice> Invoice(Guid id,CancellationToken ct)=>await db.Set<FinanceInvoice>().AsNoTracking().SingleOrDefaultAsync(x=>x.Id==id,ct)??throw ApiFault.NotFound();
    private async Task<FinancePayment> Payment(Guid id,CancellationToken ct)=>await db.Set<FinancePayment>().AsNoTracking().SingleOrDefaultAsync(x=>x.Id==id,ct)??throw ApiFault.NotFound();
    private async Task<FinancialPolicy> ActivePolicy(Guid inst,string currency,CancellationToken ct)
    {
        var candidates=await db.Set<FinancialPolicy>().AsNoTracking().Where(x=>x.InstitutionId==inst&&x.Currency==currency&&x.Status=="Published").ToListAsync(ct);
        var matches=candidates.Where(x=>{var date=FinanceRules.Today(x.TimeZoneId);return x.EffectiveFrom<=date&&date<x.EffectiveUntil;}).ToArray();
        if(matches.Length!=1)throw ApiFault.Conflict("Exactly one effective published financial policy is required for this currency.");return matches[0];
    }
    private Task<FinanceResult> Write(ClaimsPrincipal user,Guid inst,Guid request,string operation,object input,FinancePermission permission,
        Guid? accountId,Func<FinanceService,Guid,Task<FinanceResult>> action,CancellationToken ct,bool authorityAdmin=false,bool studentRequest=false)
        =>retry.ExecuteAsync<FinanceService,FinanceResult>(async service=>
        {
            if(request==Guid.Empty)throw ApiFault.Bad("A nonempty idempotency RequestId is required.");
            await using var tx=await service.db.Database.BeginTransactionAsync(ct);
            // One lock order for academic integration, budgets, cash reservations and
            // account posting. No independently committed monetary side effects.
            await RegistrationCoordination.Lock(service.db,inst,ct);
            ApplicationUser actor;
            if(authorityAdmin)actor=await service.security.RequireAdminAsync(user,inst,ct);
            else if(studentRequest&&accountId is Guid owned)actor=await service.access.ReadAccount(user,await service.Account(owned,ct),ct);
            else actor=await service.access.Require(user,inst,permission,ct);
            if(accountId is Guid target)
            {
                var account=await service.Account(target,ct);if(account.InstitutionId!=inst)throw ApiFault.NotFound();
                if(!studentRequest&&actor.Id==account.StudentId)throw ApiFault.Forbidden();
            }
            var fingerprint=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new{operation,accountId,input}))));
            var prior=await service.db.Set<FinanceCommand>().AsNoTracking().SingleOrDefaultAsync(x=>x.InstitutionId==inst&&x.RequestId==request,ct);
            if(prior is not null)
            {if(prior.Fingerprint!=fingerprint)throw ApiFault.Conflict("Idempotency key was used with different financial input.");await tx.CommitAsync(ct);return JsonSerializer.Deserialize<FinanceResult>(prior.ResultJson)!;}
            var result=await action(service,actor.Id);
            service.db.Add(new FinanceCommand{InstitutionId=inst,RequestId=request,Fingerprint=fingerprint,ResultJson=JsonSerializer.Serialize(result)});
            await service.db.SaveChangesAsync(ct);
            if(result.AccountId is Guid changed)await service.SyncHolds(changed,actor.Id,ct);
            await service.db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return result;
        },ct);
    private async Task<FinanceResult> AccountWrite(ClaimsPrincipal user,Guid accountId,Guid request,string operation,object input,FinancePermission permission,
        Func<FinanceService,Guid,Task<FinanceResult>> action,CancellationToken ct,bool student=false)
    {
        var account=await Account(accountId,ct);
        return await Write(user,account.InstitutionId,request,operation,input,permission,accountId,action,ct,studentRequest:student);
    }
    private void Audit(Guid actor,Guid inst,Guid? account,Guid entity,string action,string detail)
    {
        db.Add(new FinanceAuditEvent{InstitutionId=inst,AccountId=account,ActorId=actor,EntityId=entity,Action=action,Detail=detail});
        security.Audit(actor,inst,"finance."+action,entity.ToString());
    }
    private void Notify(FinancialAccount account,string kind,Guid source,Guid? transition=null)
        =>OutboxService.Record(db,account.InstitutionId,kind,transition??source,new{StudentId=account.StudentId,AccountId=account.Id,EntityId=source});
    private async Task<Dictionary<string,long>> Ledger(Guid account,CancellationToken ct)
        =>await (from line in db.Set<FinanceJournalLine>().AsNoTracking() join journal in db.Set<FinanceJournal>().AsNoTracking() on line.JournalId equals journal.Id
            where journal.AccountId==account group line by line.Bucket into g select new{Bucket=g.Key,Amount=g.Sum(x=>x.SignedMinor)}).ToDictionaryAsync(x=>x.Bucket,x=>x.Amount,ct);
    private async Task<Guid> Post(FinancialAccount account,string kind,Guid source,string key,long amount,string debit,string credit,Guid actor,string reason,
        CancellationToken ct,Guid? invoice=null,Guid? payment=null,Guid? reverses=null)
    {
        if(db.Database.CurrentTransaction is null)throw new InvalidOperationException("Financial posting requires an owning transaction.");
        FinanceRules.Journal(amount,[new(debit,amount),new(credit,-amount)]);
        var balances=await Ledger(account.Id,ct);balances[debit]=FinanceRules.Sum([balances.GetValueOrDefault(debit),amount]);balances[credit]=FinanceRules.Sum([balances.GetValueOrDefault(credit),-amount]);
        FinanceRules.Balances(balances);
        var journal=new FinanceJournal{InstitutionId=account.InstitutionId,AccountId=account.Id,Currency=account.Currency,Kind=kind,SourceId=source,
            PostingKey=key,AmountMinor=amount,ActorId=actor,Reason=FinanceRules.Text(reason,1000,"posting reason"),InvoiceId=invoice,PaymentId=payment,ReversesJournalId=reverses};db.Add(journal);
        db.AddRange(new FinanceJournalLine{InstitutionId=account.InstitutionId,JournalId=journal.Id,Bucket=debit,SignedMinor=amount},
            new FinanceJournalLine{InstitutionId=account.InstitutionId,JournalId=journal.Id,Bucket=credit,SignedMinor=-amount});
        await db.SaveChangesAsync(ct);return journal.Id;
    }
    private async Task<long> NetCharges(Guid invoice,CancellationToken ct)
    {
        var baseAmount=await db.Set<FinanceInvoice>().Where(x=>x.Id==invoice&&x.Status!="Draft").Select(x=>x.TotalMinor).SingleOrDefaultAsync(ct);
        var adjustments=await db.Set<FinanceAdjustment>().Where(x=>x.InvoiceId==invoice).SumAsync(x=>x.SignedMinor,ct);
        var aid=await db.Set<FinanceJournal>().Where(x=>x.InvoiceId==invoice&&(x.Kind=="ScholarshipGranted"||x.Kind=="ScholarshipReclaimed"))
            .SumAsync(x=>x.Kind=="ScholarshipGranted"?x.AmountMinor:-x.AmountMinor,ct);
        return FinanceRules.Sum([baseAmount,adjustments,-aid]);
    }
    private async Task<long> Allocated(Guid invoice,CancellationToken ct)=>await db.Set<FinanceAllocation>().Where(x=>x.InvoiceId==invoice).SumAsync(x=>x.SignedMinor,ct);
    private async Task<long> Outstanding(Guid invoice,CancellationToken ct)=>FinanceRules.Sum([await NetCharges(invoice,ct),-await Allocated(invoice,ct)]);
    private async Task<long> PaymentAvailable(FinancePayment payment,CancellationToken ct,Guid? excludeRefund=null)
    {
        var allocated=await db.Set<FinanceAllocation>().Where(x=>x.PaymentId==payment.Id).SumAsync(x=>x.SignedMinor,ct);
        var refunded=await db.Set<FinanceRefund>().Where(x=>x.PaymentId==payment.Id&&x.Status=="Executed").SumAsync(x=>x.AmountMinor,ct);
        var reserved=await db.Set<FinanceRefund>().Where(x=>x.PaymentId==payment.Id&&x.Id!=excludeRefund&&(x.Status=="Requested"||x.Status=="Approved")).SumAsync(x=>x.AmountMinor,ct);
        return FinanceRules.AvailablePayment(payment.Status=="Posted"?payment.AmountMinor:0,allocated,refunded,reserved);
    }
    private async Task ReleaseAllocations(FinancialAccount account,Guid invoice,long amount,Guid actor,Guid command,CancellationToken ct)
    {
        var allocations=await db.Set<FinanceAllocation>().AsNoTracking().Where(x=>x.InvoiceId==invoice&&x.SignedMinor>0).OrderByDescending(x=>x.Id).ToListAsync(ct);
        foreach(var original in allocations)
        {
            if(amount==0)break;var reversed=-await db.Set<FinanceAllocation>().Where(x=>x.OriginalAllocationId==original.Id).SumAsync(x=>x.SignedMinor,ct);
            var part=Math.Min(amount,original.SignedMinor-reversed);if(part==0)continue;
            await ReverseAllocation(account,original,part,actor,command,ct);amount-=part;
        }
        if(amount!=0)throw ApiFault.Conflict("Allocated debt cannot be reconciled for correction.");
    }
    private async Task ReverseAllocation(FinancialAccount account,FinanceAllocation original,long amount,Guid actor,Guid command,CancellationToken ct)
    {
        var row=new FinanceAllocation{InstitutionId=account.InstitutionId,AccountId=account.Id,Currency=account.Currency,InvoiceId=original.InvoiceId,PaymentId=original.PaymentId,OriginalAllocationId=original.Id,SignedMinor=-amount};
        row.JournalId=await Post(account,"AllocationReversal",row.Id,$"{command:N}:release:{row.Id:N}",amount,"Receivable","StudentCredit",actor,"Allocation released by an authorized correcting transaction.",ct,original.InvoiceId,original.PaymentId);
        db.Add(row);await db.SaveChangesAsync(ct);
    }
}
