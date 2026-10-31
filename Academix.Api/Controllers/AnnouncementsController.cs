using Academix.Api.DTOs.Learning;
using Academix.Api.Services.Learning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Academix.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/lms/sections/{sectionId:guid}/announcements")]
public sealed class AnnouncementsController(LearningOperations service) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<object>> Create(Guid sectionId, AnnouncementCreate request, CancellationToken ct)
        => Created("/api/lms/announcements", await service.Announce(User, sectionId, request, ct));

    [HttpGet]
    public async Task<ActionResult<object>> List(Guid sectionId, CancellationToken ct)
        => Ok(await service.Announcements(User, sectionId, ct));
}
