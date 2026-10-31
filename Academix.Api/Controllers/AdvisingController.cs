using Academix.Api.DTOs.Advising;
using Academix.Api.DTOs.Workspaces;
using Academix.Api.Services.Advising;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Academix.Api.Controllers;

[ApiController, Authorize, Route("api/advising")]
public sealed partial class AdvisingController(AdvisingService service) : ControllerBase
{
    [HttpGet("sections/{sectionId:guid}/risks")]
    public async Task<ActionResult<PageResult<StudentRisk>>> Risks(Guid sectionId,
        [FromQuery] int page = 1, CancellationToken ct = default)
        => Ok(await service.Risks(User, sectionId, page, ct));

    [HttpPost("sections/{sectionId:guid}/cases")]
    public async Task<ActionResult<AdvisingCaseView>> OpenCase(Guid sectionId,
        CreateAdvisingCase request, CancellationToken ct)
        => Ok(await service.Open(User, sectionId, request, ct));

    [HttpGet("sections/{sectionId:guid}/cases")]
    public async Task<ActionResult<PageResult<AdvisingCaseView>>> SectionCases(Guid sectionId,
        [FromQuery] int page = 1, CancellationToken ct = default)
        => Ok(await service.SectionCases(User, sectionId, page, ct));

    [HttpGet("cases/{id:guid}")]
    public async Task<ActionResult<AdvisingCaseDetail>> Detail(Guid id, CancellationToken ct)
        => Ok(await service.Detail(User, id, ct));

    [HttpPost("cases/{id:guid}/notes")]
    public async Task<ActionResult<AdvisingNoteView>> AddNote(Guid id, CreateAdvisingNote request, CancellationToken ct)
        => Ok(await service.Note(User, id, request, ct));

    [HttpPost("cases/{id:guid}/tasks")]
    public async Task<ActionResult<AdvisingTaskView>> AddTask(Guid id, CreateAdvisingTask request, CancellationToken ct)
        => Ok(await service.AddTask(User, id, request, ct));

    [HttpPost("tasks/{taskId:guid}/report-done")]
    public async Task<ActionResult<AdvisingTaskView>> ReportDone(Guid taskId, TaskTransition request, CancellationToken ct)
        => Ok(await service.ReportTask(User, taskId, request, ct));

    [HttpPost("tasks/{taskId:guid}/review")]
    public async Task<ActionResult<AdvisingTaskView>> ReviewTask(Guid taskId, ReviewAdvisingTask request, CancellationToken ct)
        => Ok(await service.ReviewTask(User, taskId, request, ct));

    [HttpPut("cases/{id:guid}/status")]
    public async Task<ActionResult<AdvisingCaseView>> ChangeStatus(Guid id, ChangeAdvisingStatus request, CancellationToken ct)
        => Ok(await service.ChangeStatus(User, id, request, ct));

    [HttpGet("me/cases")]
    public async Task<ActionResult<PageResult<StudentCaseView>>> MyCases([FromQuery] int page = 1,
        CancellationToken ct = default)
        => Ok(await service.MyCases(User, page, ct));

    [HttpGet("institutions/{institutionId:guid}/overview")]
    public async Task<ActionResult<AdvisingOverview>> InstitutionOverview(Guid institutionId, CancellationToken ct)
        => Ok(await service.InstitutionOverview(User, institutionId, ct));
}
