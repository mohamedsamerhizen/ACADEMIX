using System.Text;
using Academix.Api.DTOs.Analytics;
using Academix.Api.DTOs.Workspaces;
using Academix.Api.Services.Analytics;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Academix.Api.Controllers;

[ApiController, Authorize, Route("api/analytics")]
public sealed class AnalyticsController(AnalyticsService service) : ControllerBase
{
    [HttpGet("institutions/{institutionId:guid}/overview")]
    public async Task<ActionResult<InstitutionAnalytics>> InstitutionOverview(Guid institutionId,
        [FromQuery] Guid? termId, CancellationToken ct)
        => Ok(await service.InstitutionOverview(User, institutionId, termId, ct));

    [HttpGet("institutions/{institutionId:guid}/sections")]
    public async Task<ActionResult<PageResult<SectionAnalyticsLine>>> InstitutionSections(Guid institutionId,
        [FromQuery] Guid? termId, [FromQuery] int page = 1, CancellationToken ct = default)
        => Ok(await service.InstitutionSections(User, institutionId, termId, page, ct));

    [HttpGet("institutions/{institutionId:guid}/attendance")]
    public async Task<ActionResult<AttendanceAnalytics>> InstitutionAttendance(Guid institutionId,
        [FromQuery] Guid? termId, CancellationToken ct)
        => Ok(await service.InstitutionAttendance(User, institutionId, termId, ct));

    [HttpGet("institutions/{institutionId:guid}/outcomes")]
    public async Task<ActionResult<OutcomesAnalytics>> InstitutionOutcomes(Guid institutionId,
        [FromQuery] Guid? termId, CancellationToken ct)
        => Ok(await service.InstitutionOutcomes(User, institutionId, termId, ct));

    [HttpGet("institutions/{institutionId:guid}/sections.csv")]
    public async Task<IActionResult> ExportInstitutionSections(Guid institutionId,
        [FromQuery] Guid? termId, CancellationToken ct)
        => Csv(await service.ExportInstitutionSections(User, institutionId, termId, ct), "institution-sections.csv");

    [HttpGet("sections/{sectionId:guid}/overview")]
    public async Task<ActionResult<SectionAnalytics>> SectionOverview(Guid sectionId, CancellationToken ct)
        => Ok(await service.SectionOverview(User, sectionId, ct));

    [HttpGet("sections/{sectionId:guid}/attendance")]
    public async Task<ActionResult<AttendanceAnalytics>> SectionAttendance(Guid sectionId, CancellationToken ct)
        => Ok(await service.SectionAttendance(User, sectionId, ct));

    [HttpGet("sections/{sectionId:guid}/engagement")]
    public async Task<ActionResult<EngagementAnalytics>> SectionEngagement(Guid sectionId, CancellationToken ct)
        => Ok(await service.SectionEngagement(User, sectionId, ct));

    [HttpGet("sections/{sectionId:guid}/outcomes")]
    public async Task<ActionResult<OutcomesAnalytics>> SectionOutcomes(Guid sectionId, CancellationToken ct)
        => Ok(await service.SectionOutcomes(User, sectionId, ct));

    [HttpGet("sections/{sectionId:guid}/students")]
    public async Task<ActionResult<PageResult<StudentAnalyticsLine>>> SectionStudents(Guid sectionId,
        [FromQuery] int page = 1, CancellationToken ct = default)
        => Ok(await service.SectionStudents(User, sectionId, page, ct));

    [HttpGet("sections/{sectionId:guid}/students.csv")]
    public async Task<IActionResult> ExportSectionStudents(Guid sectionId, CancellationToken ct)
        => Csv(await service.ExportSectionStudents(User, sectionId, ct), "section-students.csv");

    [HttpGet("platform/overview")]
    public async Task<ActionResult<PlatformAnalytics>> PlatformOverview(CancellationToken ct)
        => Ok(await service.PlatformOverview(User, ct));

    [HttpGet("platform/institutions")]
    public async Task<ActionResult<PageResult<PlatformInstitutionAnalytics>>> PlatformInstitutions(
        [FromQuery] int page = 1, CancellationToken ct = default)
        => Ok(await service.PlatformInstitutions(User, page, ct));

    [HttpGet("me/summary")]
    public async Task<ActionResult<MyAnalytics>> MySummary(CancellationToken ct)
        => Ok(await service.MySummary(User, ct));

    private FileContentResult Csv(string csv, string fileName)
    {
        // BOM supports common spreadsheet clients; all fields are CSV-escaped.
        var bytes = new UTF8Encoding(true).GetPreamble().Concat(Encoding.UTF8.GetBytes(csv)).ToArray();
        Response.Headers["Cache-Control"] = "no-store";
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(bytes, "text/csv; charset=utf-8", fileName);
    }
}
