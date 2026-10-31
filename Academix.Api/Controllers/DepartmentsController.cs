using Academix.Api.DTOs.Academic;
using Academix.Api.Services.Academic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Academix.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/academic/departments")]
public sealed class DepartmentsController(StructureService service) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<object>> Create(DepartmentCreate request, CancellationToken ct)
        => Created("/api/academic/departments", await service.CreateDepartment(User, request, ct));

    [HttpGet]
    public async Task<ActionResult<object>> List(Guid institutionId, int? page, CancellationToken ct)
        => Ok(await service.ListDepartments(User, institutionId, page ?? 1, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<object>> Get(Guid id, CancellationToken ct)
        => Ok(await service.GetDepartment(User, id, ct));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<object>> Update(Guid id, NamedUpdate request, CancellationToken ct)
        => Ok(await service.UpdateDepartment(User, id, request, ct));
}
