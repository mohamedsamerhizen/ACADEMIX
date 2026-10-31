using Academix.Api.DTOs.AcademicOperations;
using Academix.Api.Services.AcademicOperations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Academix.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/academic-operations/student/me")]
public sealed class StudentAcademicOperationsController(StudentAcademicOperationsService service) : ControllerBase
{
    [HttpGet("timetable")]
    public async Task<ActionResult<IReadOnlyList<SlotView>>> Timetable(CancellationToken ct)
        => Ok(await service.Timetable(User, ct));

    [HttpGet("schedule")]
    public async Task<ActionResult<IReadOnlyList<StudentScheduleItem>>> Schedule(
        [FromQuery] DateTime fromUtc, [FromQuery] DateTime toUtc, CancellationToken ct)
        => Ok(await service.Schedule(User, fromUtc, toUtc, ct));

    [HttpGet("sections/{sectionId:guid}/attendance")]
    public async Task<ActionResult<AttendanceSummary>> Attendance(Guid sectionId, CancellationToken ct)
        => Ok(await service.Attendance(User, sectionId, ct));

    [HttpPost("requests")]
    public async Task<ActionResult<AcademicRequestView>> CreateRequest([FromBody] AcademicRequestCreate request,
        CancellationToken ct) => Ok(await service.CreateRequest(User, request, ct));

    [HttpGet("requests")]
    public async Task<ActionResult<IReadOnlyList<AcademicRequestView>>> Requests([FromQuery] int page = 1,
        CancellationToken ct = default) => Ok(await service.MyRequests(User, page, ct));

    [HttpPost("requests/{requestId:guid}/cancel")]
    public async Task<ActionResult<AcademicRequestView>> CancelRequest(Guid requestId,
        [FromQuery] long expectedVersion, CancellationToken ct)
        => Ok(await service.CancelRequest(User, requestId, expectedVersion, ct));
}
