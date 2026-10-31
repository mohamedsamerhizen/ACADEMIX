using Academix.Api.DTOs.AdvancedLearning;
using Academix.Api.Services.AdvancedLearning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Academix.Api.Controllers;

[ApiController, Authorize, Route("api/advanced-lms")]
public sealed class AdvancedGroupLearningController(GroupLearningService service) : ControllerBase
{
    [HttpPost("sections/{sectionId:guid}/teams")]
    public async Task<ActionResult<TeamView>> CreateTeam(Guid sectionId, CreateTeam request, CancellationToken ct)
        => Ok(await service.CreateTeam(User, sectionId, request, ct));

    [HttpGet("sections/{sectionId:guid}/teams")]
    public async Task<ActionResult<IReadOnlyList<TeamView>>> Teams(Guid sectionId, CancellationToken ct)
        => Ok(await service.Teams(User, sectionId, ct));

    [HttpPost("teams/{teamId:guid}/members/{studentId:guid}")]
    public async Task<ActionResult<TeamView>> AddMember(Guid teamId, Guid studentId, CancellationToken ct)
        => Ok(await service.AddMember(User, teamId, studentId, ct));

    [HttpDelete("teams/{teamId:guid}/members/{studentId:guid}")]
    public async Task<ActionResult<TeamView>> RemoveMember(Guid teamId, Guid studentId, CancellationToken ct)
        => Ok(await service.RemoveMember(User, teamId, studentId, ct));

    [HttpPost("sections/{sectionId:guid}/rubrics")]
    public async Task<ActionResult<RubricView>> CreateRubric(Guid sectionId, CreateRubric request, CancellationToken ct)
        => Ok(await service.CreateRubric(User, sectionId, request, ct));

    [HttpGet("sections/{sectionId:guid}/rubrics")]
    public async Task<ActionResult<IReadOnlyList<RubricView>>> Rubrics(Guid sectionId, CancellationToken ct)
        => Ok(await service.Rubrics(User, sectionId, ct));

    [HttpPost("sections/{sectionId:guid}/group-projects")]
    public async Task<ActionResult<GroupProjectView>> CreateProject(Guid sectionId, CreateGroupProject request, CancellationToken ct)
        => Ok(await service.CreateProject(User, sectionId, request, ct));

    [HttpPut("group-projects/{id:guid}/publish")]
    public async Task<ActionResult<GroupProjectView>> Publish(Guid id, PublishGroupProject request, CancellationToken ct)
        => Ok(await service.Publish(User, id, request, ct));

    [HttpGet("sections/{sectionId:guid}/group-projects")]
    public async Task<ActionResult<IReadOnlyList<GroupProjectView>>> Projects(Guid sectionId, CancellationToken ct)
        => Ok(await service.Projects(User, sectionId, ct));

    [HttpGet("group-projects/{projectId:guid}/rubric")]
    public async Task<ActionResult<RubricView>> ProjectRubric(Guid projectId, CancellationToken ct)
        => Ok(await service.PublishedProjectRubric(User, projectId, ct));

    [HttpPut("group-projects/{projectId:guid}/teams/{teamId:guid}/draft")]
    public async Task<ActionResult<GroupSubmissionView>> SaveDraft(Guid projectId, Guid teamId, UpdateGroupDraft request, CancellationToken ct)
        => Ok(await service.SaveDraft(User, projectId, teamId, request, ct));

    [HttpPost("group-projects/{projectId:guid}/teams/{teamId:guid}/submit")]
    public async Task<ActionResult<GroupSubmissionView>> Submit(Guid projectId, Guid teamId, CancellationToken ct)
        => Ok(await service.Submit(User, projectId, teamId, ct));

    [HttpGet("group-projects/{projectId:guid}/my-submission")]
    public async Task<ActionResult<GroupSubmissionView>> MySubmission(Guid projectId, CancellationToken ct)
        => Ok(await service.MySubmission(User, projectId, ct));

    [HttpGet("group-projects/{projectId:guid}/submissions")]
    public async Task<ActionResult<IReadOnlyList<GroupSubmissionView>>> Submissions(Guid projectId, CancellationToken ct)
        => Ok(await service.Submissions(User, projectId, ct));

    [HttpPut("group-submissions/{submissionId:guid}/grade")]
    public async Task<ActionResult<GroupSubmissionView>> Grade(Guid submissionId, GradeGroupSubmission request, CancellationToken ct)
        => Ok(await service.Grade(User, submissionId, request, ct));

    [HttpGet("group-projects/{projectId:guid}/my-assessment")]
    public async Task<ActionResult<GroupAssessmentView>> MyAssessment(Guid projectId, CancellationToken ct)
        => Ok(await service.MyAssessment(User, projectId, ct));

    [HttpGet("group-submissions/{submissionId:guid}/assessment")]
    public async Task<ActionResult<GroupAssessmentView>> StaffAssessment(Guid submissionId, CancellationToken ct)
        => Ok(await service.StaffAssessment(User, submissionId, ct));

    [HttpGet("group-submissions/{submissionId:guid}/grade-history")]
    public async Task<ActionResult<IReadOnlyList<GradeAuditView>>> GradeHistory(Guid submissionId, CancellationToken ct)
        => Ok(await service.GradeHistory(User, submissionId, ct));
}
