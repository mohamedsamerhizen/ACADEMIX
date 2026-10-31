using Academix.Api.DTOs.AcademicOperations;
using Academix.Api.Services.AcademicOperations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Academix.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/academic-operations/institutions/{institutionId:guid}")]
public sealed class InstitutionAcademicOperationsController(RegistrarOperationsService service) : ControllerBase
{
    [HttpPost("calendar")]
    public async Task<ActionResult<CalendarEventView>> CreateCalendar(Guid institutionId,
        [FromBody] CalendarEventCreate request, CancellationToken ct)
        => Ok(await service.CreateEvent(User, institutionId, request, ct));

    [HttpGet("calendar")]
    public async Task<ActionResult<IReadOnlyList<CalendarEventView>>> Calendar(Guid institutionId,
        [FromQuery] DateTime fromUtc, [FromQuery] DateTime toUtc, CancellationToken ct)
        => Ok(await service.Calendar(User, institutionId, fromUtc, toUtc, ct));

    [HttpGet("requests")]
    public async Task<ActionResult<IReadOnlyList<AcademicRequestView>>> Requests(Guid institutionId,
        [FromQuery] string? status, [FromQuery] int page = 1, CancellationToken ct = default)
        => Ok(await service.Requests(User, institutionId, status, page, ct));

    [HttpPost("requests/{requestId:guid}/decision")]
    public async Task<ActionResult<AcademicRequestView>> Decide(Guid institutionId, Guid requestId,
        [FromBody] AcademicRequestDecision request, CancellationToken ct)
        => Ok(await service.Decide(User, institutionId, requestId, request, ct));

    [HttpGet("requests/{requestId:guid}/history")]
    public async Task<ActionResult<IReadOnlyList<AcademicRequestAudit>>> History(Guid institutionId, Guid requestId,
        CancellationToken ct) => Ok(await service.RequestHistory(User, institutionId, requestId, ct));
}
