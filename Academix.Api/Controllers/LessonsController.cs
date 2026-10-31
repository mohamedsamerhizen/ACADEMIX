using Academix.Api.DTOs.Learning;
using Academix.Api.Services.Learning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Academix.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/lms")]
public sealed class LessonsController(LearningOperations service) : ControllerBase
{
    [HttpPost("sections/{sectionId:guid}/lessons")]
    public async Task<ActionResult<object>> Create(Guid sectionId, LessonCreate request, CancellationToken ct)
        => Created("/api/lms/lessons", await service.AddLesson(User, sectionId, request, ct));

    [HttpPut("lessons/{id:guid}/publish")]
    public async Task<ActionResult<object>> Publish(Guid id, LessonPublish request, CancellationToken ct)
        => Ok(await service.PublishLesson(User, id, request, ct));

    [HttpGet("sections/{sectionId:guid}/content")]
    public async Task<ActionResult<object>> Content(Guid sectionId, CancellationToken ct)
        => Ok(await service.Catalog(User, sectionId, ct));

    [HttpPut("lessons/{id:guid}/complete")]
    public async Task<ActionResult<object>> Complete(Guid id, CancellationToken ct)
        => Ok(await service.Complete(User, id, ct));

    [HttpGet("sections/{sectionId:guid}/my-progress")]
    public async Task<ActionResult<object>> Progress(Guid sectionId, CancellationToken ct)
        => Ok(await service.Progress(User, sectionId, ct));
}
