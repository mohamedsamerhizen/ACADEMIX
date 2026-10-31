using Academix.Api.DTOs.Advising;
using Microsoft.AspNetCore.Mvc;

namespace Academix.Api.Controllers;

public sealed partial class AdvisingController
{
    [HttpGet("institutions/{institutionId:guid}/policy")]
    public async Task<IActionResult> ProgressPolicy(Guid institutionId,CancellationToken ct)=>Ok(await service.ProgressPolicy(User,institutionId,ct));
    [HttpPut("institutions/{institutionId:guid}/policy")]
    public async Task<IActionResult> ConfigureProgress(Guid institutionId,AdvisingPolicyInput body,CancellationToken ct)=>Ok(await service.ConfigureProgress(User,institutionId,body,ct));
    [HttpPost("students/{studentId:guid}/advisor")]
    public async Task<IActionResult> AssignAdvisor(Guid studentId,AssignAdvisor body,CancellationToken ct)=>Ok(await service.AssignAcademicAdvisor(User,studentId,body,ct));
    [HttpGet("students/{studentId:guid}/advisors")]
    public async Task<IActionResult> AdvisorHistory(Guid studentId,[FromQuery]int page=1,CancellationToken ct=default)=>Ok(await service.AdvisorHistory(User,studentId,page,ct));
    [HttpDelete("students/{studentId:guid}/advisors/{assignmentId:guid}")]
    public async Task<IActionResult> EndAssignment(Guid studentId,Guid assignmentId,CancellationToken ct)
        {await service.EndAdvisorAssignment(User,studentId,assignmentId,ct);return NoContent();}
    [HttpGet("institutions/{institutionId:guid}/advisees")]
    public async Task<IActionResult> Advisees(Guid institutionId,[FromQuery]int page=1,CancellationToken ct=default)=>Ok(await service.MyAdvisees(User,institutionId,page,ct));
    [HttpPost("students/{studentId:guid}/sessions")]
    public async Task<IActionResult> ScheduleSession(Guid studentId,ScheduleAdvisingSession body,CancellationToken ct)=>Ok(await service.ScheduleSession(User,studentId,body,ct));
    [HttpGet("students/{studentId:guid}/sessions")]
    public async Task<IActionResult> StaffSessions(Guid studentId,[FromQuery]string? status,[FromQuery]int page=1,CancellationToken ct=default)=>Ok(await service.StaffSessions(User,studentId,status,page,ct));
    [HttpPut("sessions/{id:guid}")]
    public async Task<IActionResult> CompleteSession(Guid id,CompleteAdvisingSession body,CancellationToken ct)=>Ok(await service.CompleteSession(User,id,body,ct));
    [HttpPost("sessions/{id:guid}/acknowledge")]
    public async Task<IActionResult> AcknowledgeSession(Guid id,TaskTransition body,CancellationToken ct)=>Ok(await service.AcknowledgeSession(User,id,body,ct));
    [HttpGet("me/sessions")]
    public async Task<IActionResult> MySessions([FromQuery]int page=1,CancellationToken ct=default)=>Ok(await service.MyAcademicSessions(User,page,ct));
    [HttpPut("students/{studentId:guid}/study-plan")]
    public async Task<IActionResult> RevisePlan(Guid studentId,ReviseStudyPlan body,CancellationToken ct)=>Ok(await service.RevisePlan(User,studentId,body,ct));
    [HttpGet("students/{studentId:guid}/study-plan")]
    public async Task<IActionResult> StudyPlan(Guid studentId,[FromQuery]int page=1,CancellationToken ct=default)=>Ok(await service.StudyPlan(User,studentId,page,ct));
    [HttpPost("students/{studentId:guid}/study-plan/review")]
    public async Task<IActionResult> ReviewPlan(Guid studentId,ReviewStudyPlan body,CancellationToken ct)=>Ok(await service.ReviewPlan(User,studentId,body,ct));
    [HttpPost("students/{studentId:guid}/study-plan/acknowledge")]
    public async Task<IActionResult> AcknowledgePlan(Guid studentId,TaskTransition body,CancellationToken ct)=>Ok(await service.AcknowledgePlan(User,studentId,body,ct));
}
