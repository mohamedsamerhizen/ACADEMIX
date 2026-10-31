using Academix.Api.DTOs.Lifecycle;
using Academix.Api.Services.Lifecycle;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Academix.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/lifecycle")]
public sealed partial class StudentLifecycleController(LifecycleService service) : ControllerBase
{
    [HttpPost("programs/{programId:guid}/admissions")]
    public async Task<ActionResult<AdmissionView>> Apply(Guid programId, SubmitAdmission body, CancellationToken ct)
        => Ok(await service.Apply(User, programId, body, ct));

    [HttpGet("me/admissions")]
    public async Task<ActionResult<IReadOnlyList<AdmissionView>>> MyAdmissions(CancellationToken ct)
        => Ok(await service.MyAdmissions(User, ct));

    [HttpGet("institutions/{institutionId:guid}/admissions")]
    public async Task<ActionResult<IReadOnlyList<AdmissionView>>> Admissions(Guid institutionId,
        [FromQuery] string? status, CancellationToken ct)
        => Ok(await service.InstitutionAdmissions(User, institutionId, status, ct));

    [HttpPost("admissions/{id:guid}/decision")]
    public async Task<ActionResult<AdmissionView>> AdmissionDecision(Guid id, LifecycleDecision body, CancellationToken ct)
        => Ok(await service.DecideAdmission(User, id, body, ct));

    [HttpPost("transfers")]
    public async Task<ActionResult<TransferView>> Transfer(SubmitTransferCredit body, CancellationToken ct)
        => Ok(await service.RequestTransfer(User, body, ct));

    [HttpGet("me/transfers")]
    public async Task<ActionResult<IReadOnlyList<TransferView>>> MyTransfers(CancellationToken ct)
        => Ok(await service.MyTransfers(User, ct));

    [HttpGet("institutions/{institutionId:guid}/transfers")]
    public async Task<ActionResult<IReadOnlyList<TransferView>>> Transfers(Guid institutionId, CancellationToken ct)
        => Ok(await service.InstitutionTransfers(User, institutionId, ct));

    [HttpPost("transfers/{id:guid}/decision")]
    public async Task<ActionResult<TransferView>> TransferDecision(Guid id, LifecycleDecision body, CancellationToken ct)
        => Ok(await service.DecideTransfer(User, id, body, ct));

    [HttpGet("students/{studentId:guid}/approved-credits")]
    public async Task<ActionResult<TransferView[]>> Credits(Guid studentId, CancellationToken ct)
        => Ok(await service.ApprovedCredits(User, studentId, ct));

    [HttpPost("graduations")]
    public async Task<ActionResult<GraduationView>> GraduationApplication(CancellationToken ct,
        [FromBody(EmptyBodyBehavior=Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)] GraduationSubmission? body=null)
        => Ok(await service.SubmitGraduation(User, body??new(null), ct));

    [HttpGet("me/graduations")]
    public async Task<ActionResult<IReadOnlyList<GraduationView>>> MyGraduations(CancellationToken ct)
        => Ok(await service.MyGraduations(User, ct));

    [HttpGet("institutions/{institutionId:guid}/graduations")]
    public async Task<ActionResult<IReadOnlyList<GraduationView>>> Graduations(Guid institutionId, CancellationToken ct)
        => Ok(await service.InstitutionGraduations(User, institutionId, ct));

    [HttpPost("graduations/{id:guid}/decision")]
    public async Task<ActionResult<GraduationView>> GraduationDecision(Guid id, LifecycleDecision body, CancellationToken ct)
        => Ok(await service.ReviewGraduation(User, id, body, ct));

    [HttpPost("graduations/{id:guid}/issue")]
    public async Task<ActionResult<GraduationView>> Issue(Guid id, IssueGraduation body, CancellationToken ct)
        => Ok(await service.IssueGraduation(User, id, body, ct));

    [HttpGet("students/{studentId:guid}/credential")]
    public async Task<ActionResult<GraduationView>> Credential(Guid studentId, CancellationToken ct)
        => Ok(await service.Credential(User, studentId, ct));

    [HttpGet("institutions/{institutionId:guid}/events")]
    public async Task<ActionResult<IReadOnlyList<LifecycleEventView>>> Events(Guid institutionId,
        [FromQuery] int page = 1, CancellationToken ct = default)
        => Ok(await service.Events(User, institutionId, page, ct));
}
