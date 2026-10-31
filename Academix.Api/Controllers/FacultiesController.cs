using Academix.Api.DTOs.Academic;
using Academix.Api.Services.Academic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Academix.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/academic/faculties")]
public sealed class FacultiesController(StructureService service) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<object>> Create(NamedCreate request, CancellationToken ct)
        => Created("/api/academic/faculties", await service.CreateFaculty(User, request, ct));

    [HttpGet]
    public async Task<ActionResult<object>> List(Guid institutionId, int? page, CancellationToken ct)
        => Ok(await service.ListFaculties(User, institutionId, page ?? 1, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<object>> Get(Guid id, CancellationToken ct)
        => Ok(await service.GetFaculty(User, id, ct));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<object>> Update(Guid id, NamedUpdate request, CancellationToken ct)
        => Ok(await service.UpdateFaculty(User, id, request, ct));
}
