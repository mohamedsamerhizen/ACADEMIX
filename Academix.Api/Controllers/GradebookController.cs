using Academix.Api.DTOs.Assessment;
using Academix.Api.Services.Assessment;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Academix.Api.Controllers;

[ApiController, Authorize, Route("api/gradebook")]
public sealed class GradebookController(GradebookService service) : ControllerBase
{
    [HttpGet("sections/{sectionId:guid}/policy")]
    public async Task<ActionResult<GradePolicyView>> Policy(Guid sectionId, CancellationToken ct)
        => Ok(await service.Policy(User, sectionId, ct));

    [HttpPut("sections/{sectionId:guid}/policy")]
    public async Task<ActionResult<GradePolicyView>> Configure(Guid sectionId, ConfigureGradePolicy request, CancellationToken ct)
        => Ok(await service.Configure(User, sectionId, request, ct));

    [HttpPost("sections/{sectionId:guid}/categories")]
    public async Task<ActionResult<GradePolicyView>> AddCategory(Guid sectionId, GradeCategoryInput request, CancellationToken ct)
        => Ok(await service.AddCategory(User, sectionId, request, ct));

    [HttpDelete("categories/{id:guid}")]
    public async Task<ActionResult<GradePolicyView>> DeleteCategory(Guid id, CancellationToken ct)
        => Ok(await service.DeleteCategory(User, id, ct));

    [HttpPost("sections/{sectionId:guid}/components")]
    public async Task<ActionResult<GradePolicyView>> AddComponent(Guid sectionId, GradeComponentInput request, CancellationToken ct)
        => Ok(await service.AddComponent(User, sectionId, request, ct));

    [HttpDelete("components/{id:guid}")]
    public async Task<ActionResult<GradePolicyView>> DeleteComponent(Guid id, CancellationToken ct)
        => Ok(await service.DeleteComponent(User, id, ct));

    [HttpPut("components/{id:guid}/students/{studentId:guid}/grade")]
    public async Task<ActionResult<ManualGradeView>> ManualGrade(Guid id, Guid studentId, ManualGradeRequest request, CancellationToken ct)
        => Ok(await service.ManualGrade(User, id, studentId, request, ct));

    [HttpGet("sections/{sectionId:guid}/preview")]
    public async Task<ActionResult<GradebookPreview>> Preview(Guid sectionId, CancellationToken ct)
        => Ok(await service.Preview(User, sectionId, ct));

    [HttpPost("sections/{sectionId:guid}/publish")]
    public async Task<ActionResult<IReadOnlyList<GradebookResultView>>> Publish(Guid sectionId,
        PublishGradebookRequest request, CancellationToken ct)
        => Ok(await service.Publish(User, sectionId, request, ct));

    [HttpGet("sections/{sectionId:guid}/results")]
    public async Task<ActionResult<IReadOnlyList<GradebookResultView>>> Results(Guid sectionId, CancellationToken ct)
        => Ok(await service.Results(User, sectionId, ct));

    [HttpGet("sections/{sectionId:guid}/my-result")]
    public async Task<ActionResult<GradebookResultView>> MyResult(Guid sectionId, CancellationToken ct)
        => Ok(await service.MyResult(User, sectionId, ct));

    [HttpPut("sections/{sectionId:guid}/students/{studentId:guid}/correction")]
    public async Task<ActionResult<GradebookResultView>> Correct(Guid sectionId, Guid studentId,
        CorrectGradebookResult request, CancellationToken ct)
        => Ok(await service.Correct(User, sectionId, studentId, request, ct));

    [HttpPost("sections/{sectionId:guid}/students/{studentId:guid}/reconciliation")]
    public async Task<ActionResult<GradebookResultView>> Reconcile(Guid sectionId,Guid studentId,ReconcileGradebookResult input,CancellationToken ct)
        =>Ok(await service.Reconcile(User,sectionId,studentId,input,ct));

    [HttpGet("sections/{sectionId:guid}/audit")]
    public async Task<ActionResult<IReadOnlyList<GradebookAuditView>>> Audit(Guid sectionId, CancellationToken ct)
        => Ok(await service.Audit(User, sectionId, ct));

    [HttpGet("terms/{termId:guid}/my-summary")]
    public async Task<ActionResult<TermGradeSummary>> MyTerm(Guid termId, CancellationToken ct)
        => Ok(await service.MyTerm(User, termId, ct));
}
