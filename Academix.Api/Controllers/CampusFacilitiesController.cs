using Academix.Api.DTOs.AcademicOperations;
using Academix.Api.Services.AcademicOperations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Academix.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/campus-facilities")]
public sealed class CampusFacilitiesController(CampusFacilitiesService service,
    CampusReservationService reservations) : ControllerBase
{
    [HttpGet("institutions/{institutionId:guid}/buildings")]
    public async Task<IActionResult> Buildings(Guid institutionId, CancellationToken ct)
        => Ok(await service.Buildings(User, institutionId, ct));

    [HttpPost("institutions/{institutionId:guid}/buildings")]
    public async Task<IActionResult> CreateBuilding(Guid institutionId, BuildingInput request, CancellationToken ct)
        => Ok(await service.SaveBuilding(User, institutionId, null, request, ct));

    [HttpPut("institutions/{institutionId:guid}/buildings/{buildingId:guid}")]
    public async Task<IActionResult> UpdateBuilding(Guid institutionId, Guid buildingId, BuildingInput request, CancellationToken ct)
        => Ok(await service.SaveBuilding(User, institutionId, buildingId, request, ct));

    [HttpGet("institutions/{institutionId:guid}/rooms")]
    public async Task<IActionResult> Rooms(Guid institutionId, [FromQuery] Guid? buildingId,
        [FromQuery] bool activeOnly, CancellationToken ct)
        => Ok(await service.Rooms(User, institutionId, buildingId, activeOnly, ct));

    [HttpGet("institutions/{institutionId:guid}/rooms/available")]
    public async Task<IActionResult> AvailableRooms(Guid institutionId, [FromQuery] int weekday,
        [FromQuery] int startMinute, [FromQuery] int durationMinutes,
        [FromQuery] int requiredCapacity, CancellationToken ct)
        => Ok(await service.AvailableRooms(User, institutionId, weekday, startMinute,
            durationMinutes, requiredCapacity, ct));

    [HttpGet("institutions/{institutionId:guid}/rooms/{roomId:guid}/occupancy")]
    public async Task<IActionResult> RoomOccupancy(Guid institutionId, Guid roomId,
        [FromQuery] DateTime fromUtc, [FromQuery] DateTime toUtc, CancellationToken ct)
        => Ok(await service.RoomOccupancy(User, institutionId, roomId, fromUtc, toUtc, ct));

    [HttpGet("institutions/{institutionId:guid}/rooms/available-at")]
    public async Task<IActionResult> AvailableAt(Guid institutionId, [FromQuery] DateTime fromUtc,
        [FromQuery] DateTime toUtc, [FromQuery] int requiredCapacity, CancellationToken ct)
        => Ok(await service.AvailableAt(User, institutionId, fromUtc, toUtc, requiredCapacity, ct));

    [HttpGet("institutions/{institutionId:guid}/calendar")]
    public async Task<IActionResult> InstitutionCalendar(Guid institutionId, [FromQuery] DateTime fromUtc,
        [FromQuery] DateTime toUtc, CancellationToken ct)
        => Ok(await service.InstitutionCalendar(User, institutionId, fromUtc, toUtc, ct));

    [HttpGet("institutions/{institutionId:guid}/reservations")]
    public async Task<IActionResult> Reservations(Guid institutionId, [FromQuery] Guid? roomId,
        [FromQuery] DateTime fromUtc, [FromQuery] DateTime toUtc, [FromQuery] int page = 1,
        CancellationToken ct = default)
        => Ok(await reservations.List(User, institutionId, roomId, fromUtc, toUtc, page, ct));

    [HttpPost("institutions/{institutionId:guid}/reservations")]
    public async Task<IActionResult> Reserve(Guid institutionId, CampusReservationCreate request, CancellationToken ct)
        => Ok(await reservations.Create(User, institutionId, request, ct));

    [HttpPost("institutions/{institutionId:guid}/reservations/{id:guid}/cancel")]
    public async Task<IActionResult> CancelReservation(Guid institutionId, Guid id,
        CampusReservationCancel request, CancellationToken ct)
        => Ok(await reservations.Cancel(User, institutionId, id, request, ct));

    [HttpGet("me/exam-venues")]
    public async Task<IActionResult> MyExamVenues(CancellationToken ct)
        => Ok(await reservations.MyExamVenues(User, ct));

    [HttpPost("institutions/{institutionId:guid}/rooms")]
    public async Task<IActionResult> CreateRoom(Guid institutionId, RoomInput request, CancellationToken ct)
        => Ok(await service.SaveRoom(User, institutionId, null, request, ct));

    [HttpPut("institutions/{institutionId:guid}/rooms/{roomId:guid}")]
    public async Task<IActionResult> UpdateRoom(Guid institutionId, Guid roomId, RoomInput request, CancellationToken ct)
        => Ok(await service.SaveRoom(User, institutionId, roomId, request, ct));
}
