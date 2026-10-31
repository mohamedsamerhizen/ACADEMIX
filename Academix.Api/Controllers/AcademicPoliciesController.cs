using Academix.Api.DTOs.Registrar;
using Academix.Api.Services.Registrar;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Academix.Api.Controllers;

[ApiController, Authorize, Route("api/institutions/{institutionId:guid}/registrar")]
public sealed class AcademicPoliciesController(AcademicPolicyService service) : ControllerBase
{
    [HttpPost("policies")]
    public async Task<ActionResult<PolicyView>> Draft(Guid institutionId, PolicyDraft input, CancellationToken ct) => Ok(await service.Draft(User, institutionId, input, ct));
    [HttpPost("policies/{id:guid}/publish")]
    public async Task<ActionResult<PolicyView>> Publish(Guid institutionId, Guid id, CancellationToken ct) => Ok(await service.Publish(User, institutionId, id, ct));
    [HttpGet("policies")]
    public async Task<ActionResult<IReadOnlyList<PolicyView>>> History(Guid institutionId, CancellationToken ct, int page = 1) => Ok(await service.History(User, institutionId, page, ct));
    [HttpPost("evaluate")]
    public async Task<ActionResult<PolicyDecision>> Evaluate(Guid institutionId, PolicyEvaluation input, CancellationToken ct) => Ok(await service.Evaluate(User, institutionId, input, ct));
    [HttpPost("students/{studentId:guid}/holds")]
    public async Task<ActionResult<HoldView>> Hold(Guid institutionId, Guid studentId, HoldInput input, CancellationToken ct) => Ok(await service.AddHold(User, institutionId, studentId, input, ct));
    [HttpPost("holds/{id:guid}/release")]
    public async Task<IActionResult> Release(Guid institutionId, Guid id, CancellationToken ct) { await service.ReleaseHold(User, institutionId, id, ct); return NoContent(); }
    [HttpPost("exceptions")]
    public async Task<ActionResult<ExceptionView>> Exception(Guid institutionId, ExceptionInput input, CancellationToken ct) => Ok(await service.RequestException(User, institutionId, input, ct));
    [HttpPost("exceptions/{id:guid}/review")]
    public async Task<ActionResult<ExceptionView>> Review(Guid institutionId, Guid id, ExceptionReview input, CancellationToken ct) => Ok(await service.ReviewException(User, institutionId, id, input, ct));
    [HttpGet("students/{studentId:guid}/decisions")]
    public async Task<ActionResult<IReadOnlyList<DecisionHistory>>> Decisions(Guid institutionId, Guid studentId, CancellationToken ct, int page = 1) => Ok(await service.Decisions(User, institutionId, studentId, page, ct));
}
