using Academix.Api.DTOs.Registrar;
using Academix.Api.Services.Registrar;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
namespace Academix.Api.Controllers;
[ApiController,Authorize,Route("api/applicants")]
public sealed class ApplicantOnboardingController(ApplicantService service):ControllerBase
{
    [HttpPost("/api/institutions/{institutionId:guid}/applicants/{id:guid}/invitation")]
    public async Task<ActionResult<InvitationReceipt>> Renew(Guid institutionId,Guid id,CancellationToken ct)=>Ok(await service.RenewInvitation(User,institutionId,id,ct));
    [HttpPost("/api/institutions/{institutionId:guid}/applicants")]
    public async Task<ActionResult<InvitationReceipt>> Invite(Guid institutionId,ApplicantInvitation input,CancellationToken ct)=>Ok(await service.Invite(User,institutionId,input,ct));
    [HttpGet("/api/institutions/{institutionId:guid}/applicants")]
    public async Task<ActionResult<IReadOnlyList<ApplicantView>>> List(Guid institutionId,CancellationToken ct,int page=1)=>Ok(await service.List(User,institutionId,page,ct));
    [HttpPost("/api/institutions/{institutionId:guid}/applicants/{id:guid}/decision")]
    public async Task<ActionResult<ApplicantView>> Review(Guid institutionId,Guid id,ApplicantDecision input,CancellationToken ct)=>Ok(await service.Review(User,institutionId,id,input,ct));
    [HttpGet("/api/institutions/{institutionId:guid}/applicants/{id:guid}/history")]
    public async Task<ActionResult<IReadOnlyList<ApplicantStatusView>>> History(Guid institutionId,Guid id,CancellationToken ct)=>Ok(await service.History(User,institutionId,id,ct));
    [AllowAnonymous,EnableRateLimiting("authentication"),HttpPost("access")]
    public async Task<ActionResult<ApplicantView>> Access(ApplicantAccess input,CancellationToken ct)=>Ok(await service.Access(input,ct));
    [AllowAnonymous,EnableRateLimiting("authentication"),HttpPost("profile")]
    public async Task<ActionResult<ApplicantView>> Profile(ApplicantProfileUpdate input,CancellationToken ct)=>Ok(await service.Update(input,ct));
    [AllowAnonymous,EnableRateLimiting("authentication"),HttpPost("activate")]
    public async Task<ActionResult<ApplicantView>> Activate(ApplicantActivation input,CancellationToken ct)=>Ok(await service.Activate(input,ct));
}
