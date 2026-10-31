using Academix.Api.DTOs.Academic;
using Academix.Api.Services.Academic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Academix.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/academic/sections")]
public sealed class SectionsController(AcademicOperations service) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<object>> Create(SectionCreate request, CancellationToken ct)
        => Created("/api/academic/sections", await service.CreateSection(User, request, ct));

    [HttpGet]
    public async Task<ActionResult<object>> List(Guid institutionId, int? page, CancellationToken ct)
        => Ok(await service.ListSections(User, institutionId, page ?? 1, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<object>> Get(Guid id, CancellationToken ct)
        => Ok(await service.GetSection(User, id, ct));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<object>> Update(Guid id, SectionUpdate request, CancellationToken ct)
        => Ok(await service.UpdateSection(User, id, request, ct));

    [HttpPost("{id:guid}/staff")]
    public async Task<ActionResult<object>> AssignStaff(Guid id, TeachingCreate request, CancellationToken ct)
        => Created("/api/academic/sections", await service.AssignStaff(User, id, request, ct));

    [HttpGet("{id:guid}/staff")]
    public async Task<ActionResult<object>> ListStaff(Guid id, CancellationToken ct)
        => Ok(await service.ListStaff(User, id, ct));

    [HttpDelete("{id:guid}/staff/{staffId:guid}")]
    public async Task<IActionResult> RemoveStaff(Guid id, Guid staffId, CancellationToken ct)
    {
        await service.RemoveStaff(User, id, staffId, ct);
        return NoContent();
    }
}
