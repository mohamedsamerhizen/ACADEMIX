using Academix.Api.DTOs.Workspaces;
using Academix.Api.Services.Workspaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Academix.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/workspace/instructor")]
public sealed class InstructorWorkspaceController(InstructorWorkspaceService service) : ControllerBase
{
    [HttpGet("sections")]
    public async Task<ActionResult<PageResult<TeachingSection>>> Sections([FromQuery] int page = 1, CancellationToken ct = default)
        => Ok(await service.Sections(User, page, ct));

    [HttpGet("sections/{sectionId:guid}/roster")]
    public async Task<ActionResult<PageResult<SectionStudent>>> Roster(Guid sectionId, [FromQuery] int page = 1,
        CancellationToken ct = default)
        => Ok(await service.Roster(User, sectionId, page, ct));

    [HttpGet("sections/{sectionId:guid}/performance")]
    public async Task<ActionResult<SectionPerformance>> Performance(Guid sectionId, CancellationToken ct)
        => Ok(await service.Performance(User, sectionId, ct));
}
