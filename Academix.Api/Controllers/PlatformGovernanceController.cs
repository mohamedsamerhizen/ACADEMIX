using Academix.Api.DTOs.Workspaces;
using Academix.Api.Services.Workspaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Academix.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/platform")]
public sealed class PlatformGovernanceController(PlatformGovernanceService service) : ControllerBase
{
    [HttpGet("overview")]
    public async Task<ActionResult<PlatformOverview>> Overview(CancellationToken ct)
        => Ok(await service.Overview(User, ct));

    [HttpGet("institutions")]
    public async Task<ActionResult<PageResult<InstitutionDirectoryItem>>> Institutions([FromQuery] int page = 1,
        CancellationToken ct = default)
        => Ok(await service.Institutions(User, page, ct));

    [HttpPost("institutions")]
    public async Task<ActionResult<InstitutionDirectoryItem>> CreateInstitution(
        InstitutionCreateRequest request, CancellationToken ct)
    {
        var created = await service.Create(User, request, ct);
        return Created($"/api/academic/institutions/{created.Id}", created);
    }
}
