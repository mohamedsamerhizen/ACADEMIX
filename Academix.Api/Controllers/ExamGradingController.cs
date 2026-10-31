using Academix.Api.DTOs.Examinations;
using Academix.Api.Services.Examinations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Academix.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/exam-grading")]
public sealed class ExamGradingController(ExamGradingService service) : ControllerBase
{
    [HttpGet("sections/{sectionId:guid}/queue")]
    public async Task<ActionResult<IReadOnlyList<ExamReviewQueueItem>>> Queue(Guid sectionId,
        [FromQuery] int page = 1, CancellationToken ct = default)
        => Ok(await service.Queue(User, sectionId, page, ct));

    [HttpPost("attempts/{attemptId:guid}/initialize")]
    public async Task<ActionResult<ExamStaffGradeView>> Initialize(Guid attemptId, CancellationToken ct)
        => Ok(await service.Initialize(User, attemptId, ct));

    [HttpGet("attempts/{attemptId:guid}")]
    public async Task<ActionResult<ExamStaffGradeView>> Get(Guid attemptId, CancellationToken ct)
        => Ok(await service.GetStaff(User, attemptId, ct));

    [HttpPut("attempts/{attemptId:guid}/questions/{publishedQuestionId:guid}/grade")]
    public async Task<ActionResult<ExamStaffGradeView>> Grade(Guid attemptId, Guid publishedQuestionId,
        ExamManualGradeRequest request, CancellationToken ct)
        => Ok(await service.Grade(User, attemptId, publishedQuestionId, request, ct));

    [HttpGet("attempts/{attemptId:guid}/history")]
    public async Task<ActionResult<IReadOnlyList<ExamGradeAuditView>>> History(Guid attemptId, CancellationToken ct)
        => Ok(await service.History(User, attemptId, ct));

    [HttpPost("attempts/{attemptId:guid}/publish")]
    public async Task<ActionResult<ExamGradeSummary>> Publish(Guid attemptId,
        ExamPublishGradesRequest request, CancellationToken ct)
        => Ok(await service.Publish(User, attemptId, request, ct));

    [HttpGet("exams/{examId:guid}/appeals")]
    public async Task<ActionResult<IReadOnlyList<ExamAppealView>>> Appeals(Guid examId, CancellationToken ct)
        => Ok(await service.Appeals(User, examId, ct));

    [HttpPost("appeals/{appealId:guid}/resolve")]
    public async Task<ActionResult<ExamAppealView>> Resolve(Guid appealId,
        ExamResolveAppealRequest request, CancellationToken ct)
        => Ok(await service.Resolve(User, appealId, request, ct));
}
