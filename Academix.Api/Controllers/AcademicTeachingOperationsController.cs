using Academix.Api.DTOs.AcademicOperations;
using Academix.Api.Services.AcademicOperations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Academix.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/academic-operations")]
public sealed class AcademicTeachingOperationsController(TeachingOperationsService service) : ControllerBase
{
    [HttpGet("sections/{sectionId:guid}/timetable")]
    public async Task<ActionResult<IReadOnlyList<SlotView>>> Timetable(Guid sectionId, CancellationToken ct)
        => Ok(await service.Slots(User, sectionId, ct));

    [HttpGet("sections/{sectionId:guid}/timetable/{slotId:guid}/history")]
    public async Task<ActionResult<IReadOnlyList<TimetableSlotRevisionView>>> SlotHistory(
        Guid sectionId, Guid slotId, CancellationToken ct)
        => Ok(await service.SlotHistory(User, sectionId, slotId, ct));

    [HttpPost("sections/{sectionId:guid}/timetable")]
    public async Task<ActionResult<SlotView>> CreateSlot(Guid sectionId, [FromBody] SlotUpsert request, CancellationToken ct)
        => Ok(await service.SaveSlot(User, sectionId, null, request, ct));

    [HttpPut("sections/{sectionId:guid}/timetable/{slotId:guid}")]
    public async Task<ActionResult<SlotView>> UpdateSlot(Guid sectionId, Guid slotId, [FromBody] SlotUpsert request, CancellationToken ct)
        => Ok(await service.SaveSlot(User, sectionId, slotId, request, ct));

    [HttpDelete("sections/{sectionId:guid}/timetable/{slotId:guid}")]
    public async Task<IActionResult> DeleteSlot(Guid sectionId, Guid slotId, [FromQuery] long expectedVersion, [FromQuery] string? reason, CancellationToken ct)
    {
        await service.DeactivateSlot(User, sectionId, slotId, expectedVersion, reason, ct);
        return NoContent();
    }

    [HttpGet("sections/{sectionId:guid}/sessions")]
    public async Task<ActionResult<IReadOnlyList<SessionView>>> Sessions(Guid sectionId,
        [FromQuery] DateTime fromUtc, [FromQuery] DateTime toUtc, CancellationToken ct)
        => Ok(await service.Sessions(User, sectionId, fromUtc, toUtc, ct));

    [HttpPost("sections/{sectionId:guid}/sessions")]
    public async Task<ActionResult<SessionView>> CreateSession(Guid sectionId, [FromBody] SessionCreate request, CancellationToken ct)
        => Ok(await service.CreateSession(User, sectionId, request, ct));

    [HttpPut("sessions/{sessionId:guid}/reschedule")]
    public async Task<ActionResult<SessionView>> Reschedule(Guid sessionId,
        [FromBody] SessionRescheduleInput request, CancellationToken ct)
        => Ok(await service.RescheduleSession(User, sessionId, request, ct));

    [HttpGet("sessions/{sessionId:guid}/revisions")]
    public async Task<ActionResult<IReadOnlyList<ClassSessionRevisionView>>> SessionRevisions(
        Guid sessionId, CancellationToken ct)
        => Ok(await service.SessionHistory(User, sessionId, ct));

    [HttpPut("sessions/{sessionId:guid}/status")]
    public async Task<ActionResult<SessionView>> SetStatus(Guid sessionId, [FromQuery] string status,
        [FromQuery] long expectedVersion, CancellationToken ct)
        => Ok(await service.SetSessionStatus(User, sessionId, status, expectedVersion, ct));

    [HttpPut("sessions/{sessionId:guid}/attendance")]
    public async Task<ActionResult<IReadOnlyList<AttendanceView>>> RecordAttendance(Guid sessionId,
        [FromBody] AttendanceBatch request, CancellationToken ct)
        => Ok(await service.RecordAttendance(User, sessionId, request, ct));

    [HttpGet("sessions/{sessionId:guid}/attendance")]
    public async Task<ActionResult<IReadOnlyList<AttendanceView>>> SessionAttendance(Guid sessionId, CancellationToken ct)
        => Ok(await service.SessionAttendance(User, sessionId, ct));

    [HttpGet("sections/{sectionId:guid}/attendance-summary")]
    public async Task<ActionResult<IReadOnlyList<AttendanceSummary>>> AttendanceSummary(Guid sectionId, CancellationToken ct)
        => Ok(await service.SectionAttendance(User, sectionId, ct));
}
