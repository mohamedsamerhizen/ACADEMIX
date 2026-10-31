using Academix.Api.DTOs.Learning;
using Academix.Api.Services.Learning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Academix.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/lms")]
public sealed class SubmissionsController(LearningOperations service) : ControllerBase
{
    [HttpPut("assignments/{id:guid}/my-draft")]
    public async Task<ActionResult<object>> SaveDraft(Guid id, SubmissionDraft request, CancellationToken ct)
        => Ok(await service.SaveDraft(User, id, request, ct));

    [HttpPost("assignments/{id:guid}/submit")]
    public async Task<ActionResult<object>> Submit(Guid id, CancellationToken ct)
        => Ok(await service.Submit(User, id, ct));

    [HttpGet("assignments/{id:guid}/my-submission")]
    public async Task<ActionResult<object>> MySubmission(Guid id, CancellationToken ct)
        => Ok(await service.MySubmission(User, id, ct));

    [HttpGet("assignments/{id:guid}/submissions")]
    public async Task<ActionResult<object>> Roster(Guid id, CancellationToken ct)
        => Ok(await service.Roster(User, id, ct));

    [HttpPut("submissions/{id:guid}/grade")]
    public async Task<ActionResult<object>> Grade(Guid id, GradeSubmission request, CancellationToken ct)
        => Ok(await service.Grade(User, id, request, ct));
}
