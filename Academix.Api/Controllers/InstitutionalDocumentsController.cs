using Academix.Api.DTOs.Institutional;
using Academix.Api.Services.Institutional;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Academix.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/institutional-documents")]
public sealed class InstitutionalDocumentsController(InstitutionalDocumentService service) : ControllerBase
{
    [HttpGet("institutions/{institutionId:guid}/policy")]
    public async Task<ActionResult<DocumentPolicyView>> Policy(Guid institutionId, CancellationToken ct)
        => Ok(await service.Policy(User, institutionId, ct));

    [HttpPut("institutions/{institutionId:guid}/policy")]
    public async Task<ActionResult<DocumentPolicyView>> UpdatePolicy(Guid institutionId,
        UpdateDocumentPolicy body, CancellationToken ct)
        => Ok(await service.UpdatePolicy(User, institutionId, body, ct));

    [HttpPost("requests")]
    public async Task<ActionResult<DocumentRequestView>> SubmitApplication(
        SubmitDocumentRequest body, CancellationToken ct)
        => Ok(await service.SubmitApplication(User, body, ct));

    [HttpGet("me/requests")]
    public async Task<ActionResult<IReadOnlyList<DocumentRequestView>>> MyRequests(CancellationToken ct)
        => Ok(await service.MyRequests(User, ct));

    [HttpPost("requests/{id:guid}/cancel")]
    public async Task<ActionResult<DocumentRequestView>> Cancel(Guid id, CancelDocumentRequest body, CancellationToken ct)
        => Ok(await service.Cancel(User, id, body, ct));

    [HttpGet("institutions/{institutionId:guid}/requests")]
    public async Task<ActionResult<IReadOnlyList<DocumentRequestView>>> InstitutionRequests(Guid institutionId,
        [FromQuery] string? status, [FromQuery] int page = 1, CancellationToken ct = default)
        => Ok(await service.InstitutionRequests(User, institutionId, status, page, ct));

    [HttpPost("requests/{id:guid}/review")]
    public async Task<ActionResult<DocumentRequestView>> Review(Guid id, DecideDocumentRequest body, CancellationToken ct)
        => Ok(await service.Review(User, id, body, ct));

    [HttpPost("requests/{id:guid}/issue")]
    public async Task<ActionResult<AcademicDocumentView>> Issue(Guid id, IssueDocumentRequest body, CancellationToken ct)
        => Ok(await service.Issue(User, id, body, ct));

    [HttpGet("me/documents")]
    public async Task<ActionResult<IReadOnlyList<AcademicDocumentView>>> MyDocuments(CancellationToken ct)
        => Ok(await service.MyDocuments(User, ct));

    [HttpGet("documents/{id:guid}")]
    public async Task<ActionResult<AcademicDocumentView>> Document(Guid id, CancellationToken ct)
        => Ok(await service.Document(User, id, ct));

    [HttpGet("institutions/{institutionId:guid}/documents")]
    public async Task<ActionResult<IReadOnlyList<AcademicDocumentView>>> InstitutionDocuments(Guid institutionId,
        [FromQuery] int page = 1, CancellationToken ct = default)
        => Ok(await service.InstitutionDocuments(User, institutionId, page, ct));

    [HttpPost("documents/{id:guid}/revoke")]
    public async Task<ActionResult<AcademicDocumentView>> Revoke(Guid id, RevokeAcademicDocument body, CancellationToken ct)
        => Ok(await service.Revoke(User, id, body, ct));

    [HttpGet("institutions/{institutionId:guid}/events")]
    public async Task<ActionResult<IReadOnlyList<InstitutionalEventView>>> Events(Guid institutionId,
        [FromQuery] int page = 1, CancellationToken ct = default)
        => Ok(await service.Events(User, institutionId, page, ct));

    [AllowAnonymous]
    [EnableRateLimiting("authentication")]
    [HttpPost("verify")]
    public async Task<ActionResult<AcademicDocumentVerification>> Verify(VerifyAcademicDocument body, CancellationToken ct)
        => Ok(await service.Verify(body, ct));
}
