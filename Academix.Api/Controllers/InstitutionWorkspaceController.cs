using Academix.Api.DTOs.Workspaces;
using Academix.Api.Services.Workspaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Academix.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/workspace/institutions/{institutionId:guid}")]
public sealed class InstitutionWorkspaceController(InstitutionWorkspaceService service) : ControllerBase
{
    [HttpGet("overview")]
    public async Task<ActionResult<InstitutionOverview>> Overview(Guid institutionId, CancellationToken ct)
        => Ok(await service.Overview(User, institutionId, ct));

    [HttpGet("users")]
    public async Task<ActionResult<PageResult<DirectoryUser>>> Users(Guid institutionId,
        [FromQuery] int page = 1, CancellationToken ct = default)
        => Ok(await service.Users(User, institutionId, page, ct));

    [HttpGet("courses/{courseId:guid}/performance")]
    public async Task<ActionResult<InstitutionCoursePerformance>> CoursePerformance(Guid institutionId,
        Guid courseId, CancellationToken ct)
        => Ok(await service.CoursePerformance(User, institutionId, courseId, ct));
}
