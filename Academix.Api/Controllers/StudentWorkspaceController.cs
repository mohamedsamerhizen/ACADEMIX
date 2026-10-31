using Academix.Api.DTOs.Workspaces;
using Academix.Api.Services.Workspaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Academix.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/workspace/student")]
public sealed class StudentWorkspaceController(StudentWorkspaceService service) : ControllerBase
{
    [HttpGet("overview")]
    public async Task<ActionResult<StudentOverview>> Overview(CancellationToken ct)
        => Ok(await service.Overview(User, ct));

    [HttpGet("courses")]
    public async Task<ActionResult<PageResult<StudentCourse>>> Courses([FromQuery] int page = 1, CancellationToken ct = default)
        => Ok(await service.Courses(User, page, ct));

    [HttpGet("courses/{sectionId:guid}/progress")]
    public async Task<ActionResult<StudentCourseProgress>> Progress(Guid sectionId, CancellationToken ct)
        => Ok(await service.Progress(User, sectionId, ct));

    [HttpGet("calendar")]
    public async Task<ActionResult<IReadOnlyList<StudentCalendarItem>>> Calendar([FromQuery] int days = 30, CancellationToken ct = default)
        => Ok(await service.Calendar(User, days, ct));
}
