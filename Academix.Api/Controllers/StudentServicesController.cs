using Academix.Api.DTOs.StudentServices;
using Academix.Api.Services.StudentServices;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Academix.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/student-services")]
public sealed class StudentServicesController(StudentServiceCaseService service) : ControllerBase
{
    [HttpGet("institutions/{institutionId:guid}/catalog")]
    public async Task<IActionResult> Catalog(Guid institutionId, [FromQuery] bool activeOnly = true,
        CancellationToken ct = default) => Ok(await service.Catalog(User, institutionId, activeOnly, ct));

    [HttpPost("institutions/{institutionId:guid}/catalog")]
    public async Task<IActionResult> CreateCatalog(Guid institutionId, ServiceDefinitionInput input,
        CancellationToken ct) => Ok(await service.SaveDefinition(User, institutionId, null, input, ct));

    [HttpPut("institutions/{institutionId:guid}/catalog/{id:guid}")]
    public async Task<IActionResult> UpdateCatalog(Guid institutionId, Guid id, ServiceDefinitionInput input,
        CancellationToken ct) => Ok(await service.SaveDefinition(User, institutionId, id, input, ct));

    [HttpPost("me/cases")]
    public async Task<IActionResult> Submit(ServiceCaseSubmit input, CancellationToken ct)
        => Ok(await service.Submit(User, input, ct));

    [HttpGet("me/cases")]
    public async Task<IActionResult> Mine([FromQuery] int page = 1, CancellationToken ct = default)
        => Ok(await service.Mine(User, page, ct));

    [HttpGet("me/cases/{id:guid}/timeline")]
    public async Task<IActionResult> MyTimeline(Guid id, CancellationToken ct)
        => Ok(await service.StudentTimeline(User, id, ct));

    [HttpPost("me/cases/{id:guid}/actions")]
    public async Task<IActionResult> MyAction(Guid id, ServiceCaseAction input, CancellationToken ct)
        => Ok(await service.Act(User, GuidFromToken(), id, input, true, ct));

    [HttpGet("institutions/{institutionId:guid}/cases")]
    public async Task<IActionResult> Queue(Guid institutionId, [FromQuery] string? status = null,
        [FromQuery] string? queue = null, [FromQuery] int page = 1, CancellationToken ct = default)
        => Ok(await service.Queue(User, institutionId, status, queue, page, ct));

    [HttpGet("institutions/{institutionId:guid}/cases/{id:guid}/timeline")]
    public async Task<IActionResult> StaffTimeline(Guid institutionId, Guid id, CancellationToken ct)
        => Ok(await service.StaffTimeline(User, institutionId, id, ct));

    [HttpPost("institutions/{institutionId:guid}/cases/{id:guid}/actions")]
    public async Task<IActionResult> StaffAction(Guid institutionId, Guid id, ServiceCaseAction input,
        CancellationToken ct) => Ok(await service.Act(User, institutionId, id, input, false, ct));

    [HttpPut("institutions/{institutionId:guid}/staff-grants/{userId:guid}/{queue}")]
    public async Task<IActionResult> SetGrant(Guid institutionId, Guid userId, string queue,
        ServiceStaffGrantInput input, CancellationToken ct)
        => Ok(await service.SetStaffGrant(User, institutionId, userId, queue, input, ct));

    [HttpGet("institutions/{institutionId:guid}/staff-grants")]
    public async Task<IActionResult> Grants(Guid institutionId, [FromQuery] string? queue = null,
        CancellationToken ct = default)
        => Ok(await service.StaffGrants(User, institutionId, queue, ct));

    [HttpPost("institutions/{institutionId:guid}/cases/{id:guid}/assign")]
    public async Task<IActionResult> Assign(Guid institutionId, Guid id, ServiceCaseAssign input,
        CancellationToken ct) => Ok(await service.Assign(User, institutionId, id, input, ct));

    [HttpPost("institutions/{institutionId:guid}/cases/{id:guid}/refer")]
    public async Task<IActionResult> Refer(Guid institutionId, Guid id, ServiceCaseRefer input,
        CancellationToken ct) => Ok(await service.Refer(User, institutionId, id, input, ct));

    [HttpGet("institutions/{institutionId:guid}/cases/{id:guid}/referrals")]
    public async Task<IActionResult> Referrals(Guid institutionId, Guid id, CancellationToken ct)
        => Ok(await service.Referrals(User, institutionId, id, ct));

    [HttpPost("institutions/{institutionId:guid}/cases/{id:guid}/escalate")]
    public async Task<IActionResult> Escalate(Guid institutionId, Guid id, ServiceCaseEscalate input,
        CancellationToken ct) => Ok(await service.Escalate(User, institutionId, id, input, ct));

    [HttpGet("institutions/{institutionId:guid}/reports")]
    public async Task<IActionResult> Report(Guid institutionId, CancellationToken ct)
        => Ok(await service.Report(User, institutionId, ct));

    [HttpGet("institutions/{institutionId:guid}/reports/csv")]
    public async Task<IActionResult> ReportCsv(Guid institutionId, CancellationToken ct)
    {
        var result = await service.Report(User, institutionId, ct);
        var csv = new System.Text.StringBuilder("Queue,Open,Overdue,Unassigned\r\n");
        foreach (var queue in result.Queues)
            csv.Append(queue.Queue).Append(',').Append(queue.Open).Append(',')
                .Append(queue.Overdue).Append(',').Append(queue.Unassigned).Append("\r\n");
        return File(System.Text.Encoding.UTF8.GetBytes(csv.ToString()), "text/csv", "student-service-report.csv");
    }

    [HttpGet("me/cases/{id:guid}/attachments")]
    public async Task<IActionResult> MyAttachments(Guid id, CancellationToken ct)
        => Ok(await service.Attachments(User, GuidFromToken(), id, true, ct));

    [HttpPost("me/cases/{id:guid}/attachments")]
    [RequestSizeLimit(2097152 + 65536)]
    public async Task<IActionResult> MyUpload(Guid id, [FromForm] IFormFile file, CancellationToken ct)
        => Ok(await service.Upload(User, GuidFromToken(), id, file, "StudentVisible", true, ct));

    [HttpGet("me/cases/{id:guid}/attachments/{attachmentId:guid}")]
    public async Task<IActionResult> MyDownload(Guid id, Guid attachmentId, CancellationToken ct)
    {
        var doc = await service.Download(User, GuidFromToken(), id, attachmentId, true, ct);
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(doc.Bytes, "application/octet-stream", doc.FileName);
    }

    [HttpGet("institutions/{institutionId:guid}/cases/{id:guid}/attachments")]
    public async Task<IActionResult> StaffAttachments(Guid institutionId, Guid id, CancellationToken ct)
        => Ok(await service.Attachments(User, institutionId, id, false, ct));

    [HttpPost("institutions/{institutionId:guid}/cases/{id:guid}/attachments")]
    [RequestSizeLimit(2097152 + 65536)]
    public async Task<IActionResult> StaffUpload(Guid institutionId, Guid id, [FromForm] IFormFile file,
        [FromForm] string visibility, CancellationToken ct)
        => Ok(await service.Upload(User, institutionId, id, file, visibility, false, ct));

    [HttpGet("institutions/{institutionId:guid}/cases/{id:guid}/attachments/{attachmentId:guid}")]
    public async Task<IActionResult> StaffDownload(Guid institutionId, Guid id, Guid attachmentId, CancellationToken ct)
    {
        var doc = await service.Download(User, institutionId, id, attachmentId, false, ct);
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(doc.Bytes, "application/octet-stream", doc.FileName);
    }

    private Guid GuidFromToken()
    {
        // IdentitySecurity independently verifies this claim against the persisted account.
        var text = User.FindFirst("institution_id")?.Value;
        if (!Guid.TryParse(text, out var id))
            throw Academix.Api.Middleware.ApiFault.Forbidden();
        return id;
    }
}
