using Academix.Api.DTOs.AdvancedLearning;
using Academix.Api.Services.AdvancedLearning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Academix.Api.Controllers;

[ApiController, Authorize, Route("api/advanced-lms")]
public sealed class AdvancedDiscussionsController(DiscussionService service) : ControllerBase
{
    [HttpPost("sections/{sectionId:guid}/discussions")]
    public async Task<ActionResult<DiscussionTopicView>> Create(Guid sectionId, DiscussionTopicCreate request, CancellationToken ct)
        => Ok(await service.Create(User, sectionId, request, ct));

    [HttpGet("sections/{sectionId:guid}/discussions")]
    public async Task<ActionResult<IReadOnlyList<DiscussionTopicView>>> List(Guid sectionId,
        [FromQuery] int offset = 0, [FromQuery] int limit = 50, CancellationToken ct = default)
        => Ok(await service.List(User, sectionId, offset, limit, ct));

    [HttpGet("discussions/{id:guid}")]
    public async Task<ActionResult<DiscussionPage>> Read(Guid id, [FromQuery] int offset = 0,
        [FromQuery] int limit = 50, CancellationToken ct = default)
        => Ok(await service.Read(User, id, offset, limit, ct));

    [HttpPost("discussions/{id:guid}/replies")]
    public async Task<ActionResult<DiscussionPostView>> Reply(Guid id, DiscussionReplyCreate request, CancellationToken ct)
        => Ok(await service.Reply(User, id, request, ct));

    [HttpPut("discussions/{id:guid}/state")]
    public async Task<ActionResult<DiscussionTopicView>> State(Guid id, DiscussionStateUpdate request, CancellationToken ct)
        => Ok(await service.UpdateState(User, id, request, ct));

    [HttpPut("discussion-posts/{id:guid}/visibility")]
    public async Task<ActionResult<DiscussionPostView>> Visibility(Guid id, [FromBody] bool hidden, CancellationToken ct)
        => Ok(await service.Moderate(User, id, hidden, ct));
}
