using Academix.Api.DTOs.Learning;
using Academix.Api.Services.Learning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Academix.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/lms")]
public sealed class AssignmentsController(LearningOperations service) : ControllerBase
{
    [HttpPost("sections/{sectionId:guid}/assignments")]
    public async Task<ActionResult<object>> Create(Guid sectionId, AssignmentCreate request, CancellationToken ct)
        => Created("/api/lms/assignments", await service.AddAssignment(User, sectionId, request, ct));

    [HttpPut("assignments/{id:guid}/publish")]
    public async Task<ActionResult<object>> Publish(Guid id, AssignmentPublish request, CancellationToken ct)
        => Ok(await service.PublishAssignment(User, id, request, ct));

    [HttpGet("sections/{sectionId:guid}/assignments")]
    public async Task<ActionResult<object>> List(Guid sectionId, CancellationToken ct)
        => Ok(await service.Assignments(User, sectionId, ct));
}
