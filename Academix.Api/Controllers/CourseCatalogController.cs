using Academix.Api.DTOs.Courses;
using Academix.Api.Services.Courses;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Academix.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/courses")]
public sealed class CourseCatalogController(ICourseQueries queries) : ControllerBase
{
    [HttpGet(Name = "ListCourses")]
    public async Task<ActionResult<IReadOnlyList<CourseDto>>> List(CancellationToken ct)
        => Ok(await queries.GetActiveAsync(ct));
}
