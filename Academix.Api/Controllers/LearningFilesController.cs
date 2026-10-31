using Academix.Api.Services.Learning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Academix.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/lms")]
public sealed class LearningFilesController(LearningOperations service) : ControllerBase
{
    [HttpPost("sections/{sectionId:guid}/files")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<object>> Upload(Guid sectionId, IFormFile file, CancellationToken ct)
        => Created("/api/lms/files", await service.Upload(User, sectionId, file, ct));

    [HttpGet("files/{id:guid}/download")]
    public async Task<IActionResult> Download(Guid id, CancellationToken ct)
    {
        var (bytes, mime, name) = await service.Download(User, id, ct);
        // The response is an attachment, and browsers must not override its type.
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return File(bytes, mime, name);
    }
}
