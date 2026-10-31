using System.Text;
using Academix.Api.DTOs.Quality;
using Academix.Api.Services.Quality;
using Academix.Api.Services.Analytics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Academix.Api.Controllers;

[ApiController, Authorize, Route("api/quality")]
public sealed class QualityEvidenceController(QualityEvidenceService service) : ControllerBase
{
    [HttpGet("institutions/{institutionId:guid}/outcomes/{outcomeId:guid}/criteria")]
    public async Task<IActionResult> Criteria(Guid institutionId, Guid outcomeId, CancellationToken ct)
        => Ok(await service.Criteria(User, institutionId, outcomeId, ct));

    [HttpPost("institutions/{institutionId:guid}/outcomes/{outcomeId:guid}/criteria")]
    public async Task<IActionResult> MapCriterion(Guid institutionId, Guid outcomeId, QualityCriterionMapInput input, CancellationToken ct)
        => Ok(await service.AttachCriterion(User, institutionId, outcomeId, input, ct));

    [HttpGet("institutions/{institutionId:guid}/reviews/{reviewId:guid}/direct-evidence")]
    public async Task<IActionResult> DirectEvidence(Guid institutionId, Guid reviewId, CancellationToken ct)
        => Ok(await service.DirectEvidence(User, institutionId, reviewId, ct));

    [HttpPost("institutions/{institutionId:guid}/reviews/{reviewId:guid}/packs")]
    public async Task<IActionResult> CreatePack(Guid institutionId, Guid reviewId, QualityPackInput input, CancellationToken ct)
        => Ok(await service.CreatePack(User, institutionId, reviewId, input, ct));

    [HttpGet("institutions/{institutionId:guid}/reviews/{reviewId:guid}/packs")]
    public async Task<IActionResult> Packs(Guid institutionId, Guid reviewId, CancellationToken ct)
        => Ok(await service.Packs(User, institutionId, reviewId, ct));

    [HttpGet("institutions/{institutionId:guid}/packs/{packId:guid}")]
    public async Task<IActionResult> Pack(Guid institutionId, Guid packId, CancellationToken ct)
        => Ok(await service.GetPack(User, institutionId, packId, ct));

    [HttpGet("institutions/{institutionId:guid}/packs/{packId:guid}/items")]
    public async Task<IActionResult> Items(Guid institutionId, Guid packId, CancellationToken ct)
        => Ok(await service.Items(User, institutionId, packId, ct));

    [HttpPost("institutions/{institutionId:guid}/packs/{packId:guid}/items")]
    public async Task<IActionResult> AttachItem(Guid institutionId, Guid packId, QualityPackItemInput input, CancellationToken ct)
        => Ok(await service.AttachItem(User, institutionId, packId, input, ct));

    [HttpPost("institutions/{institutionId:guid}/packs/{packId:guid}/seal")]
    public async Task<IActionResult> SealPack(Guid institutionId, Guid packId, CancellationToken ct)
        => Ok(await service.SealPack(User, institutionId, packId, ct));

    [HttpGet("institutions/{institutionId:guid}/actions/{actionId:guid}/history")]
    public async Task<IActionResult> ActionHistory(Guid institutionId, Guid actionId, CancellationToken ct)
        => Ok(await service.ActionHistory(User, institutionId, actionId, ct));

    [HttpGet("institutions/{institutionId:guid}/programs/{programId:guid}/trends")]
    public async Task<IActionResult> Trends(Guid institutionId, Guid programId, CancellationToken ct)
        => Ok(await service.ProgramTrends(User, institutionId, programId, ct));

    [HttpGet("institutions/{institutionId:guid}/programs/{programId:guid}/trends.csv")]
    public async Task<IActionResult> TrendCsv(Guid institutionId, Guid programId, CancellationToken ct)
    {
        var rows = await service.ProgramTrends(User, institutionId, programId, ct);
        var lines = new List<string> {
            AnalyticsPolicy.CsvRow("ProgramId", "TermId", "PublishedAtUtc", "ProxyMapped", "ProxyMet",
                "DirectMapped", "DirectMet", "RubricCriterionObservations", "OpenActions", "OverdueActions")
        };
        lines.AddRange(rows.Select(x => AnalyticsPolicy.CsvRow(x.ProgramId, x.TermId, x.PublishedAtUtc,
            x.ProxyMappedOutcomes, x.ProxyMeetingTargets, x.DirectMappedOutcomes, x.DirectMeetingTargets,
            x.DirectObservations, x.OpenActions, x.OverdueActions)));
        Response.Headers["Cache-Control"] = "no-store";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(new UTF8Encoding(true).GetPreamble().Concat(Encoding.UTF8.GetBytes(string.Join("\r\n", lines) + "\r\n")).ToArray(),
            "text/csv; charset=utf-8", "program-quality-trends.csv");
    }

    [HttpGet("institutions/{institutionId:guid}/packs/{packId:guid}/manifest.json")]
    public async Task<IActionResult> SealedManifest(Guid institutionId, Guid packId, CancellationToken ct)
    {
        var pack = await service.GetPack(User, institutionId, packId, ct);
        if (pack.Status != "Sealed") return Conflict("Pack has not been sealed.");
        Response.Headers["Cache-Control"] = "no-store";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(Encoding.UTF8.GetBytes(pack.ManifestJson), "application/json", "quality-evidence-manifest.json");
    }
}
