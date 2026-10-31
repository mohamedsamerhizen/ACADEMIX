using Academix.Api.DTOs.Records;
using Academix.Api.Services.Records;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Academix.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/records")]
public sealed class RecordsController(RecordsService service) : ControllerBase
{
    [HttpPost("programs/{programId:guid}/students/admit")]
    public async Task<ActionResult<MembershipView>> Admit(Guid programId, AdmitStudent body, CancellationToken ct)
        => Ok(await service.Admit(User, programId, body, ct));

    [HttpGet("students/{studentId:guid}/program")]
    public async Task<ActionResult<MembershipView>> Membership(Guid studentId, CancellationToken ct)
        => Ok(await service.Membership(User, studentId, ct));

    [HttpPut("students/{studentId:guid}/program/progress")]
    public async Task<ActionResult<MembershipView>> Advance(Guid studentId, ProgressProgram body, CancellationToken ct)
        => Ok(await service.Advance(User, studentId, body, ct));

    [HttpGet("courses/{courseId:guid}/prerequisites")]
    public async Task<ActionResult<IReadOnlyList<PrerequisiteView>>> Prerequisites(Guid courseId, CancellationToken ct)
        => Ok(await service.Prerequisites(User, courseId, ct));

    [HttpPost("courses/{courseId:guid}/prerequisites")]
    public async Task<ActionResult<IReadOnlyList<PrerequisiteView>>> AddPrerequisite(Guid courseId, AddPrerequisiteRequest body, CancellationToken ct)
        => Ok(await service.AddPrerequisite(User, courseId, body, ct));

    [HttpDelete("courses/{courseId:guid}/prerequisites/{requiredId:guid}")]
    public async Task<IActionResult> RemovePrerequisite(Guid courseId, Guid requiredId, CancellationToken ct)
    {
        await service.RemovePrerequisite(User, courseId, requiredId, ct); return NoContent();
    }

    [HttpGet("sections/{sectionId:guid}/students/{studentId:guid}/eligibility")]
    public async Task<ActionResult<EnrollmentEligibility>> Eligibility(Guid sectionId, Guid studentId, CancellationToken ct)
        => Ok(await service.Eligibility(User, sectionId, studentId, ct));

    [HttpPut("sections/{sectionId:guid}/students/{studentId:guid}/registration")]
    public async Task<ActionResult<RegistrationView>> ChangeRegistration(Guid sectionId, Guid studentId,
        RegistrationChange body, CancellationToken ct)
        => Ok(await service.ChangeRegistration(User, sectionId, studentId, body, ct));

    [HttpPost("sections/{sectionId:guid}/transcript/sync")]
    public async Task<ActionResult<TranscriptSyncResult>> SyncTranscript(Guid sectionId, CancellationToken ct)
        => Ok(await service.SyncTranscript(User, sectionId, ct));

    [HttpGet("students/{studentId:guid}/transcript")]
    public async Task<ActionResult<TranscriptView>> Transcript(Guid studentId, CancellationToken ct)
        => Ok(await service.Transcript(User, studentId, ct));

    [HttpPost("terms/{termId:guid}/students/{studentId:guid}/standing/recalculate")]
    public async Task<ActionResult<StandingView>> RecalculateStanding(Guid termId, Guid studentId, CancellationToken ct)
        => Ok(await service.RecalculateStanding(User, termId, studentId, ct));

    [HttpGet("terms/{termId:guid}/students/{studentId:guid}/standing")]
    public async Task<ActionResult<StandingView>> Standing(Guid termId, Guid studentId, CancellationToken ct)
        => Ok(await service.Standing(User, termId, studentId, ct));

    [HttpGet("institutions/{institutionId:guid}/events")]
    public async Task<ActionResult<IReadOnlyList<LifecycleEventView>>> Events(Guid institutionId,
        [FromQuery] int page = 1, CancellationToken ct = default)
        => Ok(await service.Events(User, institutionId, page, ct));
}
