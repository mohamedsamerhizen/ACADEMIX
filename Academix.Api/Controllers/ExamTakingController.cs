using Academix.Api.DTOs.Examinations;
using Academix.Api.Services.Examinations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Academix.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/exam-taking")]
public sealed class ExamTakingController(ExamTakingService service) : ControllerBase
{
    [HttpGet("sections/{sectionId:guid}/available")]
    public async Task<ActionResult<IReadOnlyList<AvailableExamView>>> Available(Guid sectionId, CancellationToken ct)
        => Ok(await service.Available(User, sectionId, ct));

    [HttpPost("exams/{examId:guid}/attempts")]
    public async Task<ActionResult<ExamAttemptDetail>> Start(Guid examId, CancellationToken ct)
        => Ok(await service.Start(User, examId, ct));

    [HttpGet("exams/{examId:guid}/my-attempts")]
    public async Task<ActionResult<IReadOnlyList<ExamAttemptSummary>>> MyAttempts(Guid examId, CancellationToken ct)
        => Ok(await service.MyAttempts(User, examId, ct));

    [HttpGet("attempts/{attemptId:guid}")]
    public async Task<ActionResult<ExamAttemptDetail>> Resume(Guid attemptId, CancellationToken ct)
        => Ok(await service.Resume(User, attemptId, ct));

    [HttpPut("attempts/{attemptId:guid}/answers/{publishedQuestionId:guid}")]
    public async Task<ActionResult<SavedExamAnswer>> SaveAnswer(Guid attemptId, Guid publishedQuestionId,
        SaveExamAnswerRequest request, CancellationToken ct)
        => Ok(await service.SaveAnswer(User, attemptId, publishedQuestionId, request, ct));

    [HttpPost("attempts/{attemptId:guid}/submit")]
    public async Task<ActionResult<ExamSubmissionReceipt>> Submit(Guid attemptId, CancellationToken ct)
        => Ok(await service.Submit(User, attemptId, ct));
}
