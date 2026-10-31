using Academix.Api.DTOs.Examinations;
using Academix.Api.Services.Examinations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Academix.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/exam-results")]
public sealed class ExamResultsController(ExamGradingService service) : ControllerBase
{
    [HttpGet("exams/{examId:guid}/my-results")]
    public async Task<ActionResult<IReadOnlyList<ExamGradeSummary>>> MyResults(Guid examId, CancellationToken ct)
        => Ok(await service.MyResults(User, examId, ct));

    [HttpGet("attempts/{attemptId:guid}")]
    public async Task<ActionResult<ExamStudentResult>> Result(Guid attemptId, CancellationToken ct)
        => Ok(await service.Result(User, attemptId, ct));

    [HttpPost("attempts/{attemptId:guid}/appeal")]
    public async Task<ActionResult<ExamAppealView>> Appeal(Guid attemptId,
        ExamAppealRequest request, CancellationToken ct)
        => Ok(await service.Appeal(User, attemptId, request, ct));
}
