using Academix.Api.DTOs.Academic;
using Academix.Api.Services.Academic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Academix.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/academic/programs")]
public sealed class ProgramsController(StructureService structure, AcademicOperations academics) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<object>> Create(ProgramCreate request, CancellationToken ct)
        => Created("/api/academic/programs", await structure.CreateProgram(User, request, ct));

    [HttpGet]
    public async Task<ActionResult<object>> List(Guid institutionId, int? page, CancellationToken ct)
        => Ok(await structure.ListPrograms(User, institutionId, page ?? 1, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<object>> Get(Guid id, CancellationToken ct)
        => Ok(await structure.GetProgram(User, id, ct));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<object>> Update(Guid id, NamedUpdate request, CancellationToken ct)
        => Ok(await structure.UpdateProgram(User, id, request, ct));

    [HttpPost("{programId:guid}/courses/{courseId:guid}")]
    public async Task<IActionResult> AssignCourse(Guid programId, Guid courseId, CancellationToken ct)
    {
        await academics.AssignCourse(User, programId, courseId, ct);
        return NoContent();
    }

    [HttpDelete("{programId:guid}/courses/{courseId:guid}")]
    public async Task<IActionResult> UnassignCourse(Guid programId, Guid courseId, CancellationToken ct)
    {
        await academics.UnassignCourse(User, programId, courseId, ct);
        return NoContent();
    }

    [HttpGet("{programId:guid}/courses")]
    public async Task<ActionResult<object>> ListCourses(Guid programId, CancellationToken ct)
        => Ok(await academics.ListProgramCourses(User, programId, ct));
}
