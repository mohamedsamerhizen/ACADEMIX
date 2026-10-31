using Academix.Api.DTOs.Learning;
using Academix.Api.Services.Learning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Academix.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/lms")]
public sealed class LearningUnitsController(LearningOperations service) : ControllerBase
{
    [HttpPost("sections/{sectionId:guid}/units")]
    public async Task<ActionResult<object>> Create(Guid sectionId, UnitCreate request, CancellationToken ct)
        => Created("/api/lms/units", await service.AddUnit(User, sectionId, request, ct));

    [HttpPut("units/{id:guid}/publish")]
    public async Task<ActionResult<object>> Publish(Guid id, UnitPublish request, CancellationToken ct)
        => Ok(await service.PublishUnit(User, id, request, ct));
}
