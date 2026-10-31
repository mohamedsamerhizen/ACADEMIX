using Academix.Api.DTOs.Advising;
using Microsoft.AspNetCore.Mvc;

namespace Academix.Api.Controllers;

public sealed partial class CurriculumController
{
    [HttpGet("students/{studentId:guid}/degree-audit")]
    public async Task<IActionResult> DegreeAudit(Guid studentId,CancellationToken ct)=>Ok(await degreeAudit.Evaluate(User,studentId,ct));
    [HttpPut("plans/{planId:guid}/elective-groups")]
    public async Task<IActionResult> ReplaceGroups(Guid planId,ReplaceElectiveGroups body,CancellationToken ct)=>Ok(await service.ReplaceGroups(User,planId,body,ct));
    [HttpGet("students/{studentId:guid}/elective-groups")]
    public async Task<IActionResult> StudentGroups(Guid studentId,CancellationToken ct)=>Ok(await service.Groups(User,studentId,ct));
    [HttpPost("students/{studentId:guid}/requirement-exceptions")]
    public async Task<IActionResult> RequestException(Guid studentId,RequirementExceptionInput body,CancellationToken ct)=>Ok(await service.RequestException(User,studentId,body,ct));
    [HttpGet("students/{studentId:guid}/requirement-exceptions")]
    public async Task<IActionResult> Exceptions(Guid studentId,[FromQuery]int page=1,CancellationToken ct=default)=>Ok(await service.Exceptions(User,studentId,page,ct));
    [HttpPost("requirement-exceptions/{id:guid}/decision")]
    public async Task<IActionResult> DecideException(Guid id,ReviewRequirementException body,CancellationToken ct)=>Ok(await service.DecideException(User,id,body,ct));
}
