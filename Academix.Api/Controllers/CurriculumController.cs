using Academix.Api.DTOs.Curriculum;
using Academix.Api.Services.Curriculum;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Academix.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/curriculum")]
public sealed partial class CurriculumController(CurriculumService service, Academix.Api.Services.Curriculum.DegreeAuditService degreeAudit) : ControllerBase
{
    [HttpPost("programs/{programId:guid}/plans")]
    public async Task<ActionResult<CurriculumPlanView>> Create(Guid programId, CreateCurriculumPlan body, CancellationToken ct)
        => Ok(await service.Create(User, programId, body, ct));

    [HttpGet("programs/{programId:guid}/plans")]
    public async Task<ActionResult<IReadOnlyList<CurriculumPlanView>>> List(Guid programId, CancellationToken ct)
        => Ok(await service.List(User, programId, ct));

    [HttpGet("plans/{planId:guid}")]
    public async Task<ActionResult<CurriculumPlanView>> Get(Guid planId, CancellationToken ct)
        => Ok(await service.Get(User, planId, ct));

    [HttpPut("plans/{planId:guid}/courses")]
    public async Task<ActionResult<CurriculumPlanView>> Courses(Guid planId, ReplaceCurriculumCourses body, CancellationToken ct)
        => Ok(await service.ReplaceCourses(User, planId, body, ct));

    [HttpPut("plans/{planId:guid}/grade-scale")]
    public async Task<ActionResult<CurriculumPlanView>> GradeScale(Guid planId, ReplaceCurriculumScale body, CancellationToken ct)
        => Ok(await service.ReplaceScale(User, planId, body, ct));

    [HttpPost("plans/{planId:guid}/publish")]
    public async Task<ActionResult<CurriculumPlanView>> Publish(Guid planId, PublishCurriculumPlan body, CancellationToken ct)
        => Ok(await service.Publish(User, planId, body, ct));

    [HttpPost("students/{studentId:guid}/assign")]
    public async Task<ActionResult<StudentCurriculumView>> Assign(Guid studentId, AssignStudentCurriculum body, CancellationToken ct)
        => Ok(await service.Assign(User, studentId, body, ct));

    [HttpGet("students/{studentId:guid}/plan")]
    public async Task<ActionResult<StudentCurriculumView>> StudentPlan(Guid studentId, CancellationToken ct)
        => Ok(await service.StudentPlan(User, studentId, ct));

    [HttpGet("students/{studentId:guid}/plan/details")]
    public async Task<ActionResult<CurriculumPlanView>> StudentPlanDetails(Guid studentId, CancellationToken ct)
        => Ok(await service.StudentPlanDetails(User, studentId, ct));

    [HttpGet("students/{studentId:guid}/graduation-audit")]
    public async Task<ActionResult<StudentCurriculumAudit>> GraduationAudit(Guid studentId, CancellationToken ct)
        => Ok(await service.Audit(User, studentId, ct));

    [HttpGet("students/{studentId:guid}/gpa")]
    public async Task<ActionResult<StudentGpaReport>> Gpa(Guid studentId, CancellationToken ct)
        => Ok(await service.Gpa(User, studentId, ct));
}
