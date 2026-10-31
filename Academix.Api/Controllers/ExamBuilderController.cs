using Academix.Api.DTOs.Examinations;
using Academix.Api.Services.Examinations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Academix.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/exams")]
public sealed class ExamBuilderController(ExamBuilderService service) : ControllerBase
{
    [HttpPost("sections/{sectionId:guid}/exams")]
    public async Task<ActionResult<ExamDetail>> Create(Guid sectionId,
        ExamDraftRequest request, CancellationToken ct)
    {
        var result = await service.Create(User, sectionId, request, ct);
        return CreatedAtAction(nameof(Get), new { id = result.Exam.Id }, result);
    }

    [HttpGet("sections/{sectionId:guid}/exams")]
    public async Task<ActionResult<IReadOnlyList<ExamSummary>>> List(Guid sectionId,
        [FromQuery] int page = 1, CancellationToken ct = default)
        => Ok(await service.List(User, sectionId, page, ct));

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ExamDetail>> Get(Guid id, CancellationToken ct)
        => Ok(await service.Get(User, id, ct));

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ExamDetail>> Edit(Guid id, ExamEditRequest request, CancellationToken ct)
        => Ok(await service.Edit(User, id, request, ct));

    [HttpPut("{id:guid}/questions")]
    public async Task<ActionResult<ExamDetail>> ReplaceQuestions(Guid id,
        ExamReplaceQuestionsRequest request, CancellationToken ct)
        => Ok(await service.ReplaceQuestions(User, id, request, ct));

    [HttpPut("{id:guid}/pools")]
    public async Task<ActionResult<ExamDetail>> ReplacePools(Guid id,
        ExamReplacePoolsRequest request, CancellationToken ct)
        => Ok(await service.ReplacePools(User, id, request, ct));

    [HttpGet("{id:guid}/assembly-check")]
    public async Task<ActionResult<ExamAssemblyCheck>> AssemblyCheck(Guid id, CancellationToken ct)
        => Ok(await service.CheckAssembly(User, id, ct));

    [HttpPost("{id:guid}/publish")]
    public async Task<ActionResult<ExamPublishedPreview>> Publish(Guid id,
        ExamPublishRequest request, CancellationToken ct)
        => Ok(await service.Publish(User, id, request, ct));

    [HttpGet("{id:guid}/published-preview")]
    public async Task<ActionResult<ExamPublishedPreview>> PublishedPreview(Guid id, CancellationToken ct)
        => Ok(await service.GetPublishedPreview(User, id, ct));
}
