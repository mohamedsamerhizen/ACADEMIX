using System.Globalization;
using System.Security.Claims;
using System.Text.Json;
using Academix.Api.Domain.Finance;
using Academix.Api.Domain.Institutional;
using Academix.Api.Domain.Registrar;
using Academix.Api.DTOs.Finance;
using Academix.Api.Middleware;
using Academix.Api.Services.Institutional;
using Microsoft.EntityFrameworkCore;
namespace Academix.Api.Services.Finance;

public sealed partial class FinanceService
{
    public Task<FinanceResult> RegisterPayment(ClaimsPrincipal user,Guid accountId,PaymentInput input,CancellationToken ct)
        =>AccountWrite(user,accountId,input.RequestId,"RegisterPayment",input,FinancePermission.Payments,async(s,actor)=>
        {
            var account=await s.Account(accountId,ct);FinanceRules.SameCurrency(account.Currency,input.Currency);FinanceRules.Money(input.AmountMinor);
            if(input.Method is not ("Cash" or "BankTransfer" or "ManualTransfer" or "ExternalReference"))throw ApiFault.Bad("Only internal cash/transfer/reference recording is supported; card processing is unsupported.");
            var owner=actor==account.StudentId;if(!owner)await s.access.Require(user,account.InstitutionId,FinancePermission.Payments,ct);
            if(owner&&input.Method is "Cash" or "ExternalReference")throw ApiFault.Forbidden();
            var reference=FinanceRules.Reference(input.Reference);
            if(await s.db.Set<FinancePayment>().AnyAsync(x=>x.InstitutionId==account.InstitutionId&&x.Reference==reference&&x.Status!="Rejected",ct))throw ApiFault.Conflict("Payment reference is already recorded.");
            var policy=await s.AccountPolicy(account,ct);
            var row=new FinancePayment{InstitutionId=account.InstitutionId,AccountId=accountId,Currency=account.Currency,Reference=reference,Method=input.Method,AmountMinor=input.AmountMinor,
                RegisteredBy=actor,RequiresVerification=owner||policy.RequirePaymentVerification};s.db.Add(row);
            s.Audit(actor,account.InstitutionId,accountId,row.Id,"payment.registered","Pending claim only; no collected funds or receipt.");return new(row.Id,"Payment",row.Status,accountId);
        },ct,student:true);
    public async Task<FinanceResult> PaymentAction(ClaimsPrincipal user,Guid id,FinanceAction input,CancellationToken ct)
    {
        var original=await Payment(id,ct);var permission=input.Action switch{"Verify" or "Reject"=>FinancePermission.Approve,"Post"=>FinancePermission.Payments,"Reverse"=>FinancePermission.Reverse,_=>throw ApiFault.Bad("Invalid payment action.")};
        return await AccountWrite(user,original.AccountId,input.RequestId,"PaymentAction",new{id,input},permission,async(s,actor)=>
        {
            var row=await s.db.Set<FinancePayment>().SingleAsync(x=>x.Id==id,ct);var account=await s.Account(row.AccountId,ct);
            if(input.Action=="Post"&&row.Status=="Posted"||input.Action=="Reverse"&&row.Status=="Reversed")return new(id,"Payment",row.Status,account.Id);
            FinanceRules.Version(row.Version,input.ExpectedVersion);var reason=FinanceRules.Text(input.Reason,1000,"payment decision reason");
            if(input.Action is "Verify" or "Reject")
            {
                FinanceAccess.Independent(actor,account.StudentId,row.RegisteredBy);
                if(row.Status!="Pending")throw ApiFault.Conflict("Only pending payment claims can be verified or rejected.");
                row.Status=input.Action=="Verify"?"Verified":"Rejected";row.VerifiedBy=input.Action=="Verify"?actor:null;row.Version++;
            }
            else if(input.Action=="Post")
            {
                FinanceRules.PaymentPost(row.Status,row.RequiresVerification);row.Status="Posted";row.PostedAtUtc=DateTime.UtcNow;row.Version++;
                await s.Post(account,"PaymentPosted",id,$"{id:N}:payment",row.AmountMinor,"Funds","StudentCredit",actor,reason,ct,payment:id);
                var key=await s.documentKeys.Active(account.InstitutionId,ct);
                var receipt=new FinanceReceipt{InstitutionId=account.InstitutionId,PaymentId=id,VerificationKeyId=key.Id};receipt.Number=$"RCPT-{DateTime.UtcNow:yyyy}-{receipt.Id:N}".ToUpperInvariant();
                var institution=await s.db.Institutions.Where(x=>x.Id==account.InstitutionId).Select(x=>x.Name).SingleAsync(ct);var digits=FinanceRules.Currencies[account.Currency];var divisor=digits switch{0=>1m,1=>10m,2=>100m,3=>1000m,4=>10000m,_=>throw new InvalidOperationException("Unsupported currency exponent.")};
                receipt.SnapshotJson=JsonSerializer.Serialize(new{Schema="ACADEMIX-FINANCE-RECEIPT-V1",receipt.Number,PaymentId=id,StudentId=account.StudentId,Institution=institution,
                    account.Currency,AmountMinor=row.AmountMinor,MinorDigits=digits,Amount=(row.AmountMinor/divisor).ToString("F"+digits,CultureInfo.InvariantCulture),row.Method,row.Reference,row.PostedAtUtc,
                    Meaning="Receipt for an internally posted payment record; this does not manufacture a bank confirmation."});
                receipt.Proof=s.documentKeys.Sign(key,ReceiptEnvelope(receipt));s.db.Add(receipt);s.Notify(account,"FinancePaymentPosted",id);
            }
            else
            {
                if(row.Status!="Posted")throw ApiFault.Conflict("Only a posted payment can be reversed.");
                if(await s.db.Set<FinanceRefund>().AnyAsync(x=>x.PaymentId==id&&(x.Status=="Requested"||x.Status=="Approved"||x.Status=="Executed"),ct))throw ApiFault.Conflict("Release pending refund reservations and correct executed refunds before reversing their source payment.");
                var allocations=await s.db.Set<FinanceAllocation>().AsNoTracking().Where(x=>x.PaymentId==id&&x.SignedMinor>0).OrderBy(x=>x.Id).ToListAsync(ct);
                foreach(var allocation in allocations)
                {
                    var remaining=FinanceRules.Sum([allocation.SignedMinor,await s.db.Set<FinanceAllocation>().Where(x=>x.OriginalAllocationId==allocation.Id).SumAsync(x=>x.SignedMinor,ct)]);
                    if(remaining>0)await s.ReverseAllocation(account,allocation,remaining,actor,input.RequestId,ct);
                }
                var posted=await s.db.Set<FinanceJournal>().SingleAsync(x=>x.SourceId==id&&x.Kind=="PaymentPosted",ct);
                await s.Post(account,"PaymentReversed",id,$"{id:N}:payment-reversal",row.AmountMinor,"StudentCredit","Funds",actor,reason,ct,payment:id,reverses:posted.Id);
                row.Status="Reversed";row.Version++;s.Notify(account,"FinancePaymentReversed",id);
            }
            s.Audit(actor,account.InstitutionId,account.Id,id,"payment."+row.Status.ToLowerInvariant(),reason);return new(id,"Payment",row.Status,account.Id);
        },ct);
    }
    public async Task<FinanceResult> AllocatePayment(ClaimsPrincipal user,Guid paymentId,AllocationInput input,CancellationToken ct)
    {
        var original=await Payment(paymentId,ct);
        return await AccountWrite(user,original.AccountId,input.RequestId,"Allocate",new{paymentId,input},FinancePermission.Payments,async(s,actor)=>
        {
            var payment=await s.Payment(paymentId,ct);var account=await s.Account(payment.AccountId,ct);var invoice=await s.Invoice(input.InvoiceId,ct);
            FinanceRules.SameCurrency(input.Currency,account.Currency);
            if(invoice.AccountId!=account.Id||invoice.Currency!=account.Currency||invoice.Status!="Posted"||payment.Status!="Posted")throw ApiFault.Conflict("Allocation requires a posted payment and invoice in the same student/currency account.");
            FinanceRules.Allocate(input.AmountMinor,await s.PaymentAvailable(payment,ct),await s.Outstanding(invoice.Id,ct));
            var row=new FinanceAllocation{InstitutionId=account.InstitutionId,AccountId=account.Id,Currency=account.Currency,InvoiceId=invoice.Id,PaymentId=paymentId,SignedMinor=input.AmountMinor};
            row.JournalId=await s.Post(account,"Allocation",row.Id,$"{input.RequestId:N}:allocation",input.AmountMinor,"StudentCredit","Receivable",actor,"Cash allocated to an existing invoice; no new tuition charge.",ct,invoice.Id,paymentId);
            s.db.Add(row);s.Audit(actor,account.InstitutionId,account.Id,row.Id,"payment.allocated",$"Invoice {invoice.Id}.");return new(row.Id,"Allocation","Posted",account.Id);
        },ct);
    }
    private static IssuedAcademicDocument ReceiptEnvelope(FinanceReceipt receipt)=>new(){Id=receipt.Id,InstitutionId=receipt.InstitutionId,Kind="PaymentReceipt",SnapshotJson=receipt.SnapshotJson,VerificationKeyId=receipt.VerificationKeyId};
    private async Task<(string Status,long Refunded)> ReceiptState(FinancePayment payment,CancellationToken ct)
    {
        var refunded=await db.Set<FinanceRefund>().Where(x=>x.PaymentId==payment.Id&&x.Status=="Executed").SumAsync(x=>x.AmountMinor,ct);
        return(payment.Status!="Posted"?"Reversed":refunded>=payment.AmountMinor?"Refunded":refunded>0?"PartiallyRefunded":"Valid",refunded);
    }
    public async Task<ReceiptView> Receipt(ClaimsPrincipal user,Guid paymentId,CancellationToken ct)
    {
        var payment=await Payment(paymentId,ct);await access.ReadAccount(user,await Account(payment.AccountId,ct),ct);
        var receipt=await db.Set<FinanceReceipt>().AsNoTracking().SingleOrDefaultAsync(x=>x.PaymentId==paymentId,ct)??throw ApiFault.NotFound();
        var state=await ReceiptState(payment,ct);return new(paymentId,receipt.Number,payment.Currency,payment.AmountMinor,receipt.Proof,receipt.SnapshotJson,state.Status);
    }
    public async Task<ReceiptVerification> VerifyReceipt(ReceiptVerificationInput input,CancellationToken ct)
    {
        if(input.Proof is not {Length:64}||!input.Proof.All(Uri.IsHexDigit)||string.IsNullOrWhiteSpace(input.Number))throw ApiFault.NotFound();
        var receipt=await db.Set<FinanceReceipt>().AsNoTracking().SingleOrDefaultAsync(x=>x.Number==input.Number,ct)??throw ApiFault.NotFound();
        if(!DocumentIntegrity.Equal(input.Proof,receipt.Proof)||!DocumentIntegrity.Equal(input.Proof,await documentKeys.Expected(ReceiptEnvelope(receipt),ct)))throw ApiFault.NotFound();
        var payment=await Payment(receipt.PaymentId,ct);var revoked=await db.Set<DocumentVerificationKey>().AnyAsync(x=>x.Id==receipt.VerificationKeyId&&x.InstitutionId==receipt.InstitutionId&&x.RevokedAtUtc!=null,ct);
        var state=await ReceiptState(payment,ct);var status=revoked?"KeyRevoked":state.Status;
        return new(!revoked&&payment.Status=="Posted",status,receipt.Number,payment.Currency,payment.AmountMinor,DocumentIntegrity.Digest(System.Text.Encoding.UTF8.GetBytes(receipt.SnapshotJson)),state.Refunded);
    }
    public async Task<Academix.Api.DTOs.Registrar.ArtifactContent> ReceiptPdf(ClaimsPrincipal user,Guid paymentId,CancellationToken ct)
    {
        var view=await Receipt(user,paymentId,ct);var receipt=await db.Set<FinanceReceipt>().AsNoTracking().SingleAsync(x=>x.PaymentId==paymentId,ct);
        var envelope=ReceiptEnvelope(receipt);envelope.ReferenceNumber=receipt.Number;envelope.LegacyProof=receipt.Proof;envelope.IssuedAtUtc=receipt.IssuedAtUtc;envelope.ExpiresAtUtc=receipt.IssuedAtUtc.AddYears(100);
        var template=new DocumentTemplateRevision{Kind="PaymentReceipt",Revision=1,Heading="Institutional payment receipt — "+view.Status,Footer="Verify the current payment/refund/reversal status with the receipt reference and proof. HMAC verification is not a PAdES digital signature.",FontFamily=pdfRenderer.DefaultFontFamily};
        var bytes=pdfRenderer.Render(envelope,template);return new(bytes,DocumentIntegrity.Digest(bytes),view.Number+".pdf");
    }
    public async Task<FinanceResult> RequestRefund(ClaimsPrincipal user,Guid paymentId,RefundInput input,CancellationToken ct)
    {
        var original=await Payment(paymentId,ct);
        return await AccountWrite(user,original.AccountId,input.RequestId,"RequestRefund",new{paymentId,input},FinancePermission.Refunds,async(s,actor)=>
        {
            var payment=await s.Payment(paymentId,ct);var account=await s.Account(payment.AccountId,ct);
            if(actor!=account.StudentId)await s.access.Require(user,account.InstitutionId,FinancePermission.Refunds,ct);
            FinanceRules.SameCurrency(input.Currency,account.Currency);if(account.Status!="Active"||payment.Status!="Posted")throw ApiFault.Conflict("Only unallocated posted cash in an unrestricted account is refundable.");
            var policy=await s.AccountPolicy(account,ct);var ledger=await s.Ledger(account.Id,ct);
            FinanceRules.Refund(input.AmountMinor,await s.PaymentAvailable(payment,ct),policy.AllowRefunds,policy.BlockRefundWithDebt,ledger.GetValueOrDefault("Receivable"));
            var row=new FinanceRefund{InstitutionId=account.InstitutionId,AccountId=account.Id,Currency=account.Currency,PaymentId=paymentId,AmountMinor=input.AmountMinor,RequestedBy=actor,PolicyId=policy.Id,Reason=FinanceRules.Text(input.Reason,1000,"refund reason")};s.db.Add(row);
            s.Audit(actor,account.InstitutionId,account.Id,row.Id,"refund.requested","Cash credit reserved; no disbursement or journal posted.");return new(row.Id,"Refund",row.Status,account.Id);
        },ct,student:true);
    }
    public async Task<FinanceResult> RefundDecision(ClaimsPrincipal user,Guid id,RefundAction input,CancellationToken ct)
    {
        var original=await db.Set<FinanceRefund>().AsNoTracking().SingleOrDefaultAsync(x=>x.Id==id,ct)??throw ApiFault.NotFound();
        var permission=input.Action switch{"Approve" or "Reject"=>FinancePermission.Approve,"Execute"=>FinancePermission.Refunds,"Reverse"=>FinancePermission.Reverse,_=>throw ApiFault.Bad("Invalid refund action.")};
        return await AccountWrite(user,original.AccountId,input.RequestId,"RefundAction",new{id,input},permission,async(s,actor)=>
        {
            var row=await s.db.Set<FinanceRefund>().SingleAsync(x=>x.Id==id,ct);var account=await s.Account(row.AccountId,ct);
            if(input.Action=="Execute"&&row.Status=="Executed"||input.Action=="Reverse"&&row.Status=="Reversed")return new(id,"Refund",row.Status,account.Id);
            FinanceRules.Version(row.Version,input.ExpectedVersion);var reason=FinanceRules.Text(input.Reason,1000,"refund decision reason");
            if(input.Action=="Reject")
            {FinanceAccess.Independent(actor,account.StudentId,row.RequestedBy);if(row.Status is not ("Requested" or "Approved"))throw ApiFault.Conflict("Only reserved refunds can be rejected.");row.Status="Rejected";row.ReviewedBy=actor;}
            else if(input.Action is "Approve" or "Execute")
            {
                if(row.Status!=(input.Action=="Approve"?"Requested":"Approved")||account.Status!="Active")throw ApiFault.Conflict("Refund state/account restriction does not permit this transition.");
                var payment=await s.Payment(row.PaymentId,ct);if(payment.Status!="Posted")throw ApiFault.Conflict("Refund source payment is not posted.");
                var policy=await s.db.Set<FinancialPolicy>().AsNoTracking().SingleAsync(x=>x.Id==row.PolicyId,ct);var ledger=await s.Ledger(account.Id,ct);
                FinanceRules.Refund(row.AmountMinor,await s.PaymentAvailable(payment,ct,row.Id),policy.AllowRefunds,policy.BlockRefundWithDebt,ledger.GetValueOrDefault("Receivable"));
                if(input.Action=="Approve"){FinanceAccess.Independent(actor,account.StudentId,row.RequestedBy);row.Status="Approved";row.ReviewedBy=actor;}
                else
                {
                    var reference=FinanceRules.Reference(input.ExecutionReference);
                    if(await s.db.Set<FinanceRefund>().AnyAsync(x=>x.InstitutionId==account.InstitutionId&&x.Id!=id&&x.ExecutionReference==reference,ct))throw ApiFault.Conflict("Refund execution reference already recorded.");
                    row.ExecutionReference=reference;row.Status="Executed";
                    await s.Post(account,"RefundExecuted",id,$"{id:N}:refund",row.AmountMinor,"StudentCredit","Funds",actor,reason,ct,payment:row.PaymentId);s.Notify(account,"FinanceRefundRecorded",id);
                }
            }
            else
            {
                if(row.Status!="Executed")throw ApiFault.Conflict("Only an executed refund record can be corrected.");
                var posting=await s.db.Set<FinanceJournal>().SingleAsync(x=>x.SourceId==id&&x.Kind=="RefundExecuted",ct);
                await s.Post(account,"RefundReversed",id,$"{id:N}:refund-reversal",row.AmountMinor,"Funds","StudentCredit",actor,reason,ct,payment:row.PaymentId,reverses:posting.Id);row.Status="Reversed";
            }
            row.Version++;s.Audit(actor,account.InstitutionId,account.Id,id,"refund."+row.Status.ToLowerInvariant(),reason+" Internal execution/correction record only; no bank transfer was initiated.");return new(id,"Refund",row.Status,account.Id);
        },ct);
    }
}
