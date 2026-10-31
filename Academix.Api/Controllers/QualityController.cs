using System.Text;
using Academix.Api.DTOs.Quality;
using Academix.Api.Services.Quality;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Academix.Api.Controllers;

[ApiController, Authorize, Route("api/quality")]
public sealed class QualityController(QualityService service) : ControllerBase
{
    [HttpGet("institutions/{institutionId:guid}/programs/{programId:guid}/outcomes")]
    public async Task<IActionResult> Outcomes(Guid institutionId, Guid programId, CancellationToken ct)
        => Ok(await service.Outcomes(User, institutionId, programId, ct));

    [HttpPost("institutions/{institutionId:guid}/programs/{programId:guid}/outcomes")]
    public async Task<IActionResult> CreateOutcome(Guid institutionId, Guid programId, QualityOutcomeInput input, CancellationToken ct)
        => Ok(await service.SaveOutcome(User, institutionId, programId, null, input, ct));

    [HttpPut("institutions/{institutionId:guid}/programs/{programId:guid}/outcomes/{outcomeId:guid}")]
    public async Task<IActionResult> UpdateOutcome(Guid institutionId, Guid programId, Guid outcomeId,
        QualityOutcomeInput input, CancellationToken ct)
        => Ok(await service.SaveOutcome(User, institutionId, programId, outcomeId, input, ct));

    [HttpGet("institutions/{institutionId:guid}/outcomes/{outcomeId:guid}/courses")]
    public async Task<IActionResult> Maps(Guid institutionId, Guid outcomeId, CancellationToken ct)
        => Ok(await service.CourseMaps(User, institutionId, outcomeId, ct));

    [HttpPost("institutions/{institutionId:guid}/outcomes/{outcomeId:guid}/courses")]
    public async Task<IActionResult> MapCourse(Guid institutionId, Guid outcomeId, QualityCourseMapInput input, CancellationToken ct)
        => Ok(await service.MapCourse(User, institutionId, outcomeId, input, ct));

    [HttpPost("institutions/{institutionId:guid}/programs/{programId:guid}/terms/{termId:guid}/reviews")]
    public async Task<IActionResult> CreateReview(Guid institutionId, Guid programId, Guid termId, CancellationToken ct)
        => Ok(await service.StartReview(User, institutionId, programId, termId, ct));

    [HttpGet("institutions/{institutionId:guid}/reviews/{reviewId:guid}")]
    public async Task<IActionResult> Preview(Guid institutionId, Guid reviewId, CancellationToken ct)
        => Ok(await service.Preview(User, institutionId, reviewId, ct));

    [HttpPost("institutions/{institutionId:guid}/reviews/{reviewId:guid}/publish")]
    public async Task<IActionResult> Publish(Guid institutionId, Guid reviewId, CancellationToken ct)
        => Ok(await service.Publish(User, institutionId, reviewId, ct));

    [HttpGet("institutions/{institutionId:guid}/reviews/{reviewId:guid}/evidence.csv")]
    public async Task<IActionResult> Export(Guid institutionId, Guid reviewId, CancellationToken ct)
    {
        var content = await service.ExportReview(User, institutionId, reviewId, ct);
        Response.Headers["Cache-Control"] = "no-store";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(new UTF8Encoding(true).GetPreamble().Concat(Encoding.UTF8.GetBytes(content)).ToArray(),
            "text/csv; charset=utf-8", "quality-review-evidence.csv");
    }

    [HttpPost("institutions/{institutionId:guid}/reviews/{reviewId:guid}/actions")]
    public async Task<IActionResult> AddAction(Guid institutionId, Guid reviewId, QualityActionInput input, CancellationToken ct)
        => Ok(await service.AddAction(User, institutionId, reviewId, input, ct));

    [HttpGet("institutions/{institutionId:guid}/reviews/{reviewId:guid}/actions")]
    public async Task<IActionResult> Actions(Guid institutionId, Guid reviewId, CancellationToken ct)
        => Ok(await service.Actions(User, institutionId, reviewId, ct));

    [HttpPost("institutions/{institutionId:guid}/actions/{actionId:guid}/decision")]
    public async Task<IActionResult> DecideAction(Guid institutionId, Guid actionId, QualityActionDecision input, CancellationToken ct)
        => Ok(await service.DecideAction(User, institutionId, actionId, input, ct));

    [HttpGet("institutions/{institutionId:guid}/dashboard")]
    public async Task<IActionResult> Dashboard(Guid institutionId, CancellationToken ct)
        => Ok(await service.Dashboard(User, institutionId, ct));
}
