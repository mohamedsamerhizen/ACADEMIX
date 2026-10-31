using Academix.Api.DTOs.AcademicOperations;
using Academix.Api.DTOs.Registrar;
using Academix.Api.Services.AcademicOperations;
using Academix.Api.Services.Curriculum;
using Academix.Api.Services.Infrastructure;
using Academix.Api.Services.Institutional;
using Academix.Api.Services.Lifecycle;
using Academix.Api.DTOs.Lifecycle;
using Academix.Api.DTOs.Institutional;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
namespace Academix.Api.Controllers;
[ApiController,Authorize,Route("api/institutions/{institutionId:guid}/registrar")]
public sealed class RegistrarCompletionController(InstitutionalDocumentService documents,CurriculumService curriculum,RegistrarOperationsService requests,OutboxService outbox,LifecycleService lifecycle):ControllerBase
{
    [HttpPost("/api/institutional-documents/documents/{id:guid}/reissue")]
    public async Task<ActionResult<DocumentRequestView>> Reissue(Guid id,ReissueDocumentRequest input,CancellationToken ct)=>Ok(await documents.RequestReissuance(User,id,input,ct));
    [HttpPost("credentials/{id:guid}/reassess")]
    public async Task<ActionResult<GraduationView>> Reassess(Guid institutionId,Guid id,HoldInput input,CancellationToken ct)=>Ok(await lifecycle.ReassessCredential(User,institutionId,id,input.Reason,ct));
    [HttpPost("document-templates")]
    public async Task<ActionResult<TemplateView>> Template(Guid institutionId,TemplateInput input,CancellationToken ct)=>Ok(await documents.CreateTemplate(User,institutionId,input,ct));
    [HttpPost("document-templates/{id:guid}/publish")]
    public async Task<ActionResult<TemplateView>> PublishTemplate(Guid institutionId,Guid id,CancellationToken ct)=>Ok(await documents.PublishTemplate(User,institutionId,id,ct));
    [HttpGet("document-templates")]
    public async Task<ActionResult<IReadOnlyList<TemplateView>>> Templates(Guid institutionId,CancellationToken ct)=>Ok(await documents.Templates(User,institutionId,ct));
    [HttpGet("document-keys")]
    public async Task<ActionResult<IReadOnlyList<VerificationKeyView>>> Keys(Guid institutionId,CancellationToken ct)=>Ok(await documents.Keys(User,institutionId,ct));
    [HttpPost("document-keys/rotate")]
    public async Task<ActionResult<VerificationKeyView>> Rotate(Guid institutionId,CancellationToken ct)=>Ok(await documents.RotateKey(User,institutionId,ct));
    [HttpPost("document-keys/{id:guid}/revoke")]
    public async Task<IActionResult> RevokeKey(Guid institutionId,Guid id,CancellationToken ct){await documents.RevokeKey(User,institutionId,id,ct);return NoContent();}
    [HttpGet("/api/institutional-documents/documents/{id:guid}/pdf")]
    public async Task<IActionResult> Pdf(Guid id,CancellationToken ct)
    {
        var artifact=await documents.Download(User,id,ct);Response.Headers["X-Content-Type-Options"]="nosniff";Response.Headers["Cache-Control"]="private, no-store";Response.Headers.ETag="\""+artifact.Digest+"\"";
        return File(artifact.Bytes,"application/pdf",artifact.FileName);
    }
    [HttpPost("/api/institutional-documents/documents/{id:guid}/supersede")]
    public async Task<IActionResult> Supersede(Guid id,SupersedeDocument input,CancellationToken ct){await documents.Supersede(User,id,input,ct);return NoContent();}
    [HttpPost("students/{studentId:guid}/transitions")]
    public async Task<ActionResult<TransitionView>> Transition(Guid institutionId,Guid studentId,TransitionInput input,CancellationToken ct)=>Ok(await curriculum.RequestTransition(User,institutionId,studentId,input,ct));
    [HttpGet("students/{studentId:guid}/transitions")]
    public async Task<ActionResult<IReadOnlyList<TransitionView>>> Transitions(Guid institutionId,Guid studentId,CancellationToken ct)=>Ok(await curriculum.Transitions(User,institutionId,studentId,ct));
    [HttpPost("transitions/{id:guid}/review")]
    public async Task<ActionResult<TransitionView>> ReviewTransition(Guid institutionId,Guid id,ExceptionReview input,CancellationToken ct)=>Ok(await curriculum.ReviewTransition(User,institutionId,id,input.Approve,ct));
    [HttpPost("transitions/{id:guid}/apply")]
    public async Task<ActionResult<TransitionView>> ApplyTransition(Guid institutionId,Guid id,CancellationToken ct)=>Ok(await curriculum.ApplyTransition(User,institutionId,id,ct));
    [HttpPost("/api/academic-operations/institutions/{institutionId:guid}/requests/{id:guid}/review")]
    public async Task<ActionResult<AcademicRequestView>> ReviewRequest(Guid institutionId,Guid id,[FromBody(EmptyBodyBehavior=Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)] RequestExecutionData? input,CancellationToken ct)=>Ok(await requests.BeginReview(User,institutionId,id,input,ct));
    [HttpPost("/api/academic-operations/institutions/{institutionId:guid}/requests/{id:guid}/fulfill")]
    public async Task<ActionResult<AcademicRequestView>> Fulfill(Guid institutionId,Guid id,FulfillAcademicRequest input,CancellationToken ct)=>Ok(await requests.Fulfill(User,institutionId,id,input,ct));
    [HttpGet("outbox")]
    public async Task<ActionResult<IReadOnlyList<EventDeliveryView>>> Deliveries(Guid institutionId,CancellationToken ct,int page=1)=>Ok(await outbox.Deliveries(User,institutionId,page,ct));
    [HttpGet("events")]
    public async Task<ActionResult<IReadOnlyList<Academix.Api.DTOs.Registrar.InstitutionalEventView>>> Events(Guid institutionId,CancellationToken ct,int page=1)=>Ok(await outbox.Feed(User,institutionId,page,ct));
    [HttpPost("outbox/{id:guid}/retry")]
    public async Task<IActionResult> Retry(Guid institutionId,Guid id,CancellationToken ct){await outbox.Control(User,institutionId,id,false,ct);return NoContent();}
    [HttpPost("outbox/{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid institutionId,Guid id,CancellationToken ct){await outbox.Control(User,institutionId,id,true,ct);return NoContent();}
}
