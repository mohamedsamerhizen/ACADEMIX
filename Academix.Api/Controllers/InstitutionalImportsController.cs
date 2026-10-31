using System.Text;
using System.Text.Json;
using Academix.Api.DTOs.Registrar;
using Academix.Api.Services.Registrar;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Academix.Api.Controllers;

[ApiController, Authorize, Route("api/institutions/{institutionId:guid}/imports")]
public sealed class InstitutionalImportsController(InstitutionalImportService service) : ControllerBase
{
    [HttpPost("csv"), RequestSizeLimit(2 * 1024 * 1024)]
    public async Task<ActionResult<ImportReport>> Csv(Guid institutionId,IFormFile file,[FromForm] Guid clientRequestId,[FromForm] string sourceSystem,[FromForm] string fieldsJson,CancellationToken ct)
        =>Ok(await service.UploadCsv(User,institutionId,file,clientRequestId,sourceSystem,fieldsJson,ct));
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<ImportJobView>>> History(Guid institutionId,CancellationToken ct,int page=1)=>Ok(await service.History(User,institutionId,page,ct));
    [HttpGet("{id:guid}/preview")]
    public async Task<ActionResult<IReadOnlyList<ImportProposal>>> Preview(Guid institutionId,Guid id,CancellationToken ct)=>Ok(await service.Preview(User,institutionId,id,ct));
    [HttpPost, RequestSizeLimit(2 * 1024 * 1024)]
    public async Task<ActionResult<ImportReport>> Upload(Guid institutionId, ImportUpload input, CancellationToken ct) => Ok(await service.Upload(User, institutionId, input, ct));
    [HttpPost("file"), RequestSizeLimit(2 * 1024 * 1024)]
    public async Task<ActionResult<ImportReport>> FileUpload(Guid institutionId, IFormFile file, CancellationToken ct)
        => Ok(await service.UploadFile(User, institutionId, file, ct));
    [HttpPost("{id:guid}/dry-run")]
    public async Task<ActionResult<ImportReport>> DryRun(Guid institutionId, Guid id, CancellationToken ct) => Ok(await service.DryRun(User, institutionId, id, ct));
    [HttpPost("{id:guid}/approve")]
    public async Task<ActionResult<ImportReport>> Approve(Guid institutionId, Guid id, CancellationToken ct) => Ok(await service.Approve(User, institutionId, id, ct));
    [HttpPost("{id:guid}/apply")]
    public async Task<ActionResult<ImportReport>> Apply(Guid institutionId, Guid id, CancellationToken ct) => Ok(await service.Apply(User, institutionId, id, ct));
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ImportReport>> Report(Guid institutionId, Guid id, CancellationToken ct) => Ok(await service.Report(User, institutionId, id, ct));
    [HttpGet("{id:guid}/reconciliation")]
    public async Task<ActionResult<ImportReport>> Reconcile(Guid institutionId, Guid id, CancellationToken ct) => Ok(await service.Reconcile(User, institutionId, id, ct));
    [HttpGet("{id:guid}/results")]
    public async Task<FileContentResult> Results(Guid institutionId, Guid id, CancellationToken ct)
        => File(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(await service.Report(User, institutionId, id, ct))), "application/json", $"import-{id:N}-results.json");
}
