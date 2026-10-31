using Academix.Api.DTOs.Academic;
using Academix.Api.Services.Academic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Academix.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/academic")]
public sealed class EnrollmentsController(AcademicOperations service) : ControllerBase
{
    [HttpPost("sections/{id:guid}/enrollments")]
    public async Task<ActionResult<object>> Enroll(Guid id, EnrollmentCreate request, CancellationToken ct)
        => Created("/api/academic/sections", await service.Enroll(User, id, request, ct));

    [HttpGet("sections/{id:guid}/roster")]
    public async Task<ActionResult<object>> Roster(Guid id, CancellationToken ct, int page = 1)
        => Ok(await service.Roster(User, id, ct, page));

    [HttpPut("sections/{id:guid}/enrollments/{studentId:guid}/withdraw")]
    public async Task<IActionResult> Withdraw(Guid id, Guid studentId, CancellationToken ct)
    {
        await service.Withdraw(User, id, studentId, ct);
        return NoContent();
    }

    [HttpGet("students/{studentId:guid}/enrollments")]
    public async Task<ActionResult<object>> History(Guid studentId, CancellationToken ct, int page = 1)
        => Ok(await service.StudentHistory(User, studentId, ct, page));
}
