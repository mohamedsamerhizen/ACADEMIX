using Academix.Api.DTOs.Finance;
using Academix.Api.Services.Finance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Academix.Api.Controllers;

[ApiController,Authorize,Route("api/finance")]
public sealed class FinanceController(FinanceService service):ControllerBase
{
    [HttpPut("institutions/{institutionId:guid}/authorities/{userId:guid}")]
    public async Task<IActionResult> Authority(Guid institutionId,Guid userId,AuthorityInput body,CancellationToken ct)=>Ok(await service.SetAuthority(User,institutionId,userId,body,ct));
    [HttpPost("institutions/{institutionId:guid}/policies")]
    public async Task<IActionResult> Policy(Guid institutionId,PolicyInput body,CancellationToken ct)=>Ok(await service.ConfigurePolicy(User,institutionId,body,ct));
    [HttpPost("policies/{id:guid}/actions")]
    public async Task<IActionResult> PolicyAction(Guid id,FinanceAction body,CancellationToken ct)=>Ok(await service.RetirePolicy(User,id,body,ct));
    [HttpGet("institutions/{institutionId:guid}/catalog")]
    public async Task<IActionResult> Catalog(Guid institutionId,[FromQuery]string kind,[FromQuery]int page=1,CancellationToken ct=default)=>Ok(await service.Catalog(User,institutionId,kind,page,ct));
    [HttpPost("institutions/{institutionId:guid}/fee-schedules")]
    public async Task<IActionResult> Schedule(Guid institutionId,FeeScheduleInput body,CancellationToken ct)=>Ok(await service.CreateSchedule(User,institutionId,body,ct));
    [HttpPost("fee-schedules/{id:guid}/actions")]
    public async Task<IActionResult> ScheduleAction(Guid id,FinanceAction body,CancellationToken ct)=>Ok(await service.ScheduleAction(User,id,body,ct));
    [HttpPost("institutions/{institutionId:guid}/students/{studentId:guid}/accounts")]
    public async Task<IActionResult> OpenAccount(Guid institutionId,Guid studentId,OpenAccountInput body,CancellationToken ct)=>Ok(await service.OpenAccount(User,institutionId,studentId,body,ct));
    [HttpGet("me/accounts")]
    public async Task<IActionResult> MyAccounts([FromQuery]string? currency,[FromQuery]int page=1,CancellationToken ct=default)=>Ok(await service.Accounts(User,null,null,currency,page,ct));
    [HttpGet("institutions/{institutionId:guid}/accounts")]
    public async Task<IActionResult> Accounts(Guid institutionId,[FromQuery]Guid? studentId,[FromQuery]string? currency,[FromQuery]int page=1,CancellationToken ct=default)=>Ok(await service.Accounts(User,institutionId,studentId,currency,page,ct));
    [HttpGet("accounts/{id:guid}/statement")]
    public async Task<IActionResult> Statement(Guid id,[FromQuery]DateOnly? from,[FromQuery]DateOnly? until,[FromQuery]int page=1,CancellationToken ct=default)=>Ok(await service.Statement(User,id,page,from,until,ct));
    [HttpGet("accounts/{id:guid}/history")]
    public async Task<IActionResult> History(Guid id,[FromQuery]string kind,[FromQuery]int page=1,CancellationToken ct=default)=>Ok(await service.History(User,id,kind,page,ct));
    [HttpPost("accounts/{id:guid}/actions")]
    public async Task<IActionResult> AccountAction(Guid id,FinanceAction body,CancellationToken ct)=>Ok(await service.AccountAction(User,id,body,ct));
    [HttpPost("accounts/{id:guid}/assessments")]
    public async Task<IActionResult> Assess(Guid id,AssessmentInput body,CancellationToken ct)=>Ok(await service.Assess(User,id,body,ct));
    [HttpGet("invoices/{id:guid}")]
    public async Task<IActionResult> Invoice(Guid id,CancellationToken ct)=>Ok(await service.InvoiceDetail(User,id,ct));
    [HttpPost("invoices/{id:guid}/actions")]
    public async Task<IActionResult> InvoiceAction(Guid id,FinanceAction body,CancellationToken ct)=>Ok(await service.InvoiceAction(User,id,body,ct));
    [HttpPost("invoices/{id:guid}/adjustments")]
    public async Task<IActionResult> Adjust(Guid id,AdjustmentInput body,CancellationToken ct)=>Ok(await service.Adjust(User,id,body,ct));
    [HttpPost("invoices/{id:guid}/payment-plans")]
    public async Task<IActionResult> Plan(Guid id,PaymentPlanInput body,CancellationToken ct)=>Ok(await service.ProposePlan(User,id,body,ct));
    [HttpPost("payment-plans/{id:guid}/decision")]
    public async Task<IActionResult> PlanDecision(Guid id,FinanceAction body,CancellationToken ct)=>Ok(await service.DecidePlan(User,id,body,ct));
    [HttpPost("institutions/{institutionId:guid}/scholarships")]
    public async Task<IActionResult> Scholarship(Guid institutionId,ScholarshipInput body,CancellationToken ct)=>Ok(await service.CreateScholarship(User,institutionId,body,ct));
    [HttpPost("invoices/{id:guid}/awards")]
    public async Task<IActionResult> Award(Guid id,AwardInput body,CancellationToken ct)=>Ok(await service.RequestAward(User,id,body,ct));
    [HttpPost("awards/{id:guid}/actions")]
    public async Task<IActionResult> AwardAction(Guid id,FinanceAction body,CancellationToken ct)=>Ok(await service.AwardAction(User,id,body,ct));
    [HttpPost("accounts/{id:guid}/payments")]
    public async Task<IActionResult> Payment(Guid id,PaymentInput body,CancellationToken ct)=>Ok(await service.RegisterPayment(User,id,body,ct));
    [HttpPost("payments/{id:guid}/actions")]
    public async Task<IActionResult> PaymentAction(Guid id,FinanceAction body,CancellationToken ct)=>Ok(await service.PaymentAction(User,id,body,ct));
    [HttpPost("payments/{id:guid}/allocations")]
    public async Task<IActionResult> Allocation(Guid id,AllocationInput body,CancellationToken ct)=>Ok(await service.AllocatePayment(User,id,body,ct));
    [HttpGet("payments/{id:guid}/receipt")]
    public async Task<IActionResult> Receipt(Guid id,CancellationToken ct)=>Ok(await service.Receipt(User,id,ct));
    [HttpGet("payments/{id:guid}/receipt/pdf")]
    public async Task<IActionResult> ReceiptPdf(Guid id,CancellationToken ct)
    {var result=await service.ReceiptPdf(User,id,ct);return File(result.Bytes,"application/pdf",result.FileName);}
    [AllowAnonymous,HttpPost("receipts/verify")]
    public async Task<IActionResult> Verify(ReceiptVerificationInput body,CancellationToken ct)=>Ok(await service.VerifyReceipt(body,ct));
    [HttpPost("payments/{id:guid}/refunds")]
    public async Task<IActionResult> Refund(Guid id,RefundInput body,CancellationToken ct)=>Ok(await service.RequestRefund(User,id,body,ct));
    [HttpPost("refunds/{id:guid}/actions")]
    public async Task<IActionResult> RefundAction(Guid id,RefundAction body,CancellationToken ct)=>Ok(await service.RefundDecision(User,id,body,ct));
    [HttpGet("accounts/{id:guid}/reconciliation")]
    public async Task<IActionResult> Reconciliation(Guid id,CancellationToken ct)=>Ok(await service.ReconcileAccount(User,id,ct));
    [HttpPost("institutions/{institutionId:guid}/bank-reconciliations")]
    public async Task<IActionResult> Bank(Guid institutionId,BankReconciliationInput body,CancellationToken ct)=>Ok(await service.ReconcileBank(User,institutionId,body,ct));
    [HttpGet("bank-reconciliations/{id:guid}")]
    public async Task<IActionResult> BankResult(Guid id,CancellationToken ct)=>Ok(await service.BankReconciliation(User,id,ct));
    [HttpGet("institutions/{institutionId:guid}/reports")]
    public async Task<IActionResult> Report(Guid institutionId,[FromQuery]DateOnly from,[FromQuery]DateOnly until,[FromQuery]int page=1,CancellationToken ct=default)=>Ok(await service.Report(User,institutionId,from,until,ct,page));
}
