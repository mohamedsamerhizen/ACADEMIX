using Academix.Api.DTOs.Governance;
using Academix.Api.Services.Governance;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Academix.Api.Controllers;

[ApiController, Authorize, Route("api/governance/institutions/{institutionId:guid}")]
public sealed class GovernanceController(GovernanceService service) : ControllerBase
{
    [HttpGet("overview")]
    public async Task<ActionResult<GovernanceOverview>> Overview(Guid institutionId, CancellationToken ct)
        => Ok(await service.Overview(User, institutionId, ct));

    [HttpPost("staff-requests")]
    public async Task<ActionResult<StaffChangeView>> CreateStaffRequest(Guid institutionId, CreateStaffChange request, CancellationToken ct)
        => Ok(await service.CreateStaffRequest(User, institutionId, request, ct));

    [HttpGet("staff-requests")]
    public async Task<ActionResult<IReadOnlyList<StaffChangeView>>> Requests(Guid institutionId,
        [FromQuery] int offset = 0, [FromQuery] int limit = 50, CancellationToken ct = default)
        => Ok(await service.StaffRequests(User, institutionId, offset, limit, ct));

    [HttpGet("staff-requests/{id:guid}")]
    public async Task<ActionResult<StaffChangeView>> GetRequest(Guid institutionId, Guid id, CancellationToken ct)
        => Ok(await service.StaffRequest(User, institutionId, id, ct));

    [HttpPost("staff-requests/{id:guid}/approve")]
    public async Task<ActionResult<StaffChangeView>> Approve(Guid institutionId, Guid id,
        DecideStaffChange request, CancellationToken ct)
        => Ok(await service.DecideStaffRequest(User, institutionId, id, request, true, ct));

    [HttpPost("staff-requests/{id:guid}/reject")]
    public async Task<ActionResult<StaffChangeView>> Reject(Guid institutionId, Guid id,
        DecideStaffChange request, CancellationToken ct)
        => Ok(await service.DecideStaffRequest(User, institutionId, id, request, false, ct));

    [HttpPost("staff-requests/{id:guid}/cancel")]
    public async Task<ActionResult<StaffChangeView>> Cancel(Guid institutionId, Guid id,
        [FromQuery] long expectedVersion, CancellationToken ct)
        => Ok(await service.CancelStaffRequest(User, institutionId, id, expectedVersion, ct));

    [HttpPost("enrollment-batches")]
    public async Task<ActionResult<EnrollmentBatchView>> CreateBatch(Guid institutionId,
        CreateEnrollmentBatch request, CancellationToken ct)
        => Ok(await service.CreateBatch(User, institutionId, request, ct));

    [HttpGet("enrollment-batches")]
    public async Task<ActionResult<IReadOnlyList<EnrollmentBatchView>>> Batches(Guid institutionId,
        [FromQuery] int offset = 0, [FromQuery] int limit = 50, CancellationToken ct = default)
        => Ok(await service.Batches(User, institutionId, offset, limit, ct));

    [HttpGet("enrollment-batches/{id:guid}")]
    public async Task<ActionResult<EnrollmentBatchView>> GetBatch(Guid institutionId, Guid id, CancellationToken ct)
        => Ok(await service.Batch(User, institutionId, id, ct));

    [HttpGet("enrollment-batches/{id:guid}/preview")]
    public async Task<ActionResult<BatchPreview>> Preview(Guid institutionId, Guid id, CancellationToken ct)
        => Ok(await service.PreviewBatch(User, institutionId, id, ct));

    [HttpPost("enrollment-batches/{id:guid}/commit")]
    public async Task<ActionResult<EnrollmentBatchView>> Commit(Guid institutionId, Guid id, CancellationToken ct)
        => Ok(await service.CommitBatch(User, institutionId, id, ct));

    [HttpGet("events")]
    public async Task<ActionResult<IReadOnlyList<GovernanceEventView>>> Events(Guid institutionId,
        [FromQuery] int offset = 0, [FromQuery] int limit = 50, CancellationToken ct = default)
        => Ok(await service.Events(User, institutionId, offset, limit, ct));
}
