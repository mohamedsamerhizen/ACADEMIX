using Academix.Api.DTOs.Academic;
using Academix.Api.Services.Academic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Academix.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/academic/courses")]
public sealed class CoursesController(AcademicOperations service) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<object>> Create(CourseCreate request, CancellationToken ct)
        => Created("/api/courses", await service.CreateCourse(User, request, ct));

    [HttpGet]
    public async Task<ActionResult<object>> List(Guid institutionId, int? page, CancellationToken ct)
        => Ok(await service.ListCourses(User, institutionId, page ?? 1, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<object>> Get(Guid id, CancellationToken ct)
        => Ok(await service.GetCourse(User, id, ct));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<object>> Update(Guid id, CourseUpdate request, CancellationToken ct)
        => Ok(await service.UpdateCourse(User, id, request, ct));
}
