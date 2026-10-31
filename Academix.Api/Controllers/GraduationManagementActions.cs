using Academix.Api.DTOs.Lifecycle;
using Microsoft.AspNetCore.Mvc;

namespace Academix.Api.Controllers;

public sealed partial class StudentLifecycleController
{
    [HttpPut("institutions/{institutionId:guid}/departments/{departmentId:guid}/graduation-reviewers")]
    public async Task<IActionResult> DepartmentReviewer(Guid institutionId,Guid departmentId,DepartmentReviewerInput body,CancellationToken ct)
        {await service.SetDepartmentReviewer(User,institutionId,departmentId,body,ct);return NoContent();}
    [HttpGet("institutions/{institutionId:guid}/graduation-queue")]
    public async Task<IActionResult> GraduationQueue(Guid institutionId,[FromQuery]string? status,[FromQuery]int page=1,CancellationToken ct=default)
        =>Ok(await service.GraduationQueue(User,institutionId,status,page,ct));
    [HttpPost("graduations/{id:guid}/review")]
    public async Task<IActionResult> BeginReview(Guid id,IssueGraduation body,CancellationToken ct)=>Ok(await service.BeginGraduationReview(User,id,body,ct));
    [HttpPost("graduations/{id:guid}/department-decision")]
    public async Task<IActionResult> DepartmentDecision(Guid id,DepartmentGraduationDecision body,CancellationToken ct)=>Ok(await service.DepartmentDecision(User,id,body,ct));
    [HttpGet("graduations/{id:guid}/evidence")]
    public async Task<IActionResult> GraduationEvidence(Guid id,CancellationToken ct)=>Ok(await service.GraduationEvidence(User,id,ct));
}
