using Academix.Api.DTOs.Communications;
using Academix.Api.Services.Communications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Academix.Api.Controllers;

[ApiController, Authorize, Route("api/communications")]
public sealed class CommunicationsController(CommunicationService service) : ControllerBase
{
    [HttpGet("notifications")]
    public async Task<ActionResult<NotificationInbox>> Inbox([FromQuery] int offset = 0,
        [FromQuery] int limit = 50, CancellationToken ct = default)
        => Ok(await service.Inbox(User, offset, limit, ct));

    [HttpPut("notifications/{id:guid}/read")]
    public async Task<IActionResult> ReadNotification(Guid id, CancellationToken ct)
    { await service.MarkNotificationRead(User, id, ct); return NoContent(); }

    [HttpPut("notifications/read-all")]
    public async Task<ActionResult<int>> ReadAll(CancellationToken ct)
        => Ok(await service.ReadAll(User, ct));

    [HttpGet("preferences")]
    public async Task<ActionResult<CommunicationPreferenceView>> Preferences(CancellationToken ct)
        => Ok(await service.GetPreferences(User, ct));

    [HttpPut("preferences")]
    public async Task<ActionResult<CommunicationPreferenceView>> Preferences(UpdateCommunicationPreference input, CancellationToken ct)
        => Ok(await service.SetPreferences(User, input, ct));

    [HttpPost("sections/{sectionId:guid}/threads")]
    public async Task<ActionResult<SectionThreadView>> StartThread(Guid sectionId, StartSectionThread input, CancellationToken ct)
        => Ok(await service.StartThread(User, sectionId, input, ct));

    [HttpGet("sections/{sectionId:guid}/threads")]
    public async Task<ActionResult<IReadOnlyList<SectionThreadView>>> Threads(Guid sectionId,
        [FromQuery] int offset = 0, [FromQuery] int limit = 50, CancellationToken ct = default)
        => Ok(await service.Threads(User, sectionId, offset, limit, ct));

    [HttpGet("threads/{threadId:guid}/messages")]
    public async Task<ActionResult<IReadOnlyList<SectionMessageView>>> Messages(Guid threadId,
        [FromQuery] int offset = 0, [FromQuery] int limit = 50, CancellationToken ct = default)
        => Ok(await service.Messages(User, threadId, offset, limit, ct));

    [HttpPost("threads/{threadId:guid}/messages")]
    public async Task<ActionResult<SectionMessageView>> Send(Guid threadId, SendSectionMessage input, CancellationToken ct)
        => Ok(await service.Send(User, threadId, input, ct));

    [HttpPut("threads/{threadId:guid}/read")]
    public async Task<IActionResult> ReadThread(Guid threadId, CancellationToken ct)
    { await service.MarkThreadRead(User, threadId, ct); return NoContent(); }

    [HttpPost("sections/{sectionId:guid}/announcements")]
    public async Task<ActionResult<SectionAnnouncementView>> Schedule(Guid sectionId,
        ScheduleSectionAnnouncement input, CancellationToken ct)
        => Ok(await service.ScheduleAnnouncement(User, sectionId, input, ct));

    [HttpPut("announcements/{id:guid}")]
    public async Task<ActionResult<SectionAnnouncementView>> Revise(Guid id,
        ReviseSectionAnnouncement input, CancellationToken ct)
        => Ok(await service.ReviseAnnouncement(User, id, input, ct));

    [HttpDelete("announcements/{id:guid}")]
    public async Task<IActionResult> Cancel(Guid id, [FromQuery] long expectedVersion, CancellationToken ct)
    { await service.CancelAnnouncement(User, id, expectedVersion, ct); return NoContent(); }

    [HttpGet("sections/{sectionId:guid}/announcements")]
    public async Task<ActionResult<IReadOnlyList<SectionAnnouncementView>>> Announcements(Guid sectionId,
        [FromQuery] int offset = 0, [FromQuery] int limit = 50, CancellationToken ct = default)
        => Ok(await service.Announcements(User, sectionId, offset, limit, ct));

    [HttpPost("announcements/{id:guid}/dispatch")]
    public async Task<ActionResult<AnnouncementDispatchResult>> Dispatch(Guid id, CancellationToken ct)
        => Ok(await service.DispatchAnnouncement(User, id, ct));
}
