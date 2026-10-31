using Microsoft.AspNetCore.Mvc;

namespace Academix.Api.Controllers;

[ApiController]
[Route("")]
public sealed class ApiInfoController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(new { name = "ACADEMIX", phase = "Phase 2" });
}
