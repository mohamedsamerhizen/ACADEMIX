using Academix.Api.DTOs.Academic;
using Academix.Api.Services.Academic;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Academix.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/academic/terms")]
public sealed class AcademicTermsController(StructureService service) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<object>> Create(TermCreate request, CancellationToken ct)
        => Created("/api/academic/terms", await service.CreateTerm(User, request, ct));

    [HttpGet]
    public async Task<ActionResult<object>> List(Guid institutionId, int? page, CancellationToken ct)
        => Ok(await service.ListTerms(User, institutionId, page ?? 1, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<object>> Get(Guid id, CancellationToken ct)
        => Ok(await service.GetTerm(User, id, ct));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<object>> Update(Guid id, TermUpdate request, CancellationToken ct)
        => Ok(await service.UpdateTerm(User, id, request, ct));
}
