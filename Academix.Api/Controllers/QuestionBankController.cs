using Academix.Api.DTOs.Examinations;
using Academix.Api.Services.Examinations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Academix.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/exams")]
public sealed class QuestionBankController(QuestionBankService questions) : ControllerBase
{
    [HttpPost("courses/{courseId:guid}/questions")]
    public async Task<ActionResult<BankQuestionDetail>> Create(Guid courseId,
        QuestionDraftRequest request, CancellationToken ct)
    {
        var result = await questions.Create(User, courseId, request, ct);
        return CreatedAtAction(nameof(Get), new { id = result.Question.Id }, result);
    }

    [HttpGet("courses/{courseId:guid}/questions")]
    public async Task<ActionResult<IReadOnlyList<BankQuestionSummary>>> List(Guid courseId,
        [FromQuery] int page = 1, [FromQuery] bool includeArchived = false, CancellationToken ct = default)
        => Ok(await questions.List(User, courseId, page, includeArchived, ct));

    [HttpGet("questions/{id:guid}")]
    public async Task<ActionResult<BankQuestionDetail>> Get(Guid id, CancellationToken ct)
        => Ok(await questions.Get(User, id, ct));

    [HttpGet("questions/{id:guid}/revisions/{number:int}")]
    public async Task<ActionResult<QuestionRevisionResponse>> GetRevision(Guid id, int number, CancellationToken ct)
        => Ok(await questions.GetRevision(User, id, number, ct));

    [HttpPut("questions/{id:guid}")]
    public async Task<ActionResult<BankQuestionDetail>> Edit(Guid id,
        QuestionEditRequest request, CancellationToken ct)
        => Ok(await questions.Edit(User, id, request, ct));

    [HttpPut("questions/{id:guid}/archive")]
    public async Task<ActionResult<BankQuestionSummary>> Archive(Guid id,
        QuestionArchiveRequest request, CancellationToken ct)
        => Ok(await questions.Archive(User, id, request, ct));
}
