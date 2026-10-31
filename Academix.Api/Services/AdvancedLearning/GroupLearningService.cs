using System.Security.Claims;
using Academix.Api.Data;
using Academix.Api.Services.Infrastructure;
using Academix.Api.Domain.Academic;
using Academix.Api.Domain.AdvancedLearning;
using Academix.Api.Domain.Identity;
using Academix.Api.DTOs.AdvancedLearning;
using Academix.Api.Middleware;
using Academix.Api.Services.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace Academix.Api.Services.AdvancedLearning;

public sealed class GroupLearningService(AcademixDbContext db, AdvancedLearningAccess access, RetryingServiceWork retry)
{
    private static GroupProjectView Map(GroupProject x) => new(x.Id, x.SectionId, x.Title, x.Instructions,
        x.DueAtUtc, x.MaxScore, x.IsPublished, x.RubricId, x.Version);
    private static GroupSubmissionView Map(GroupProjectSubmission x) => new(x.Id, x.ProjectId, x.TeamId,
        x.Content, x.Status, x.Score, x.Feedback, x.Version, x.SubmittedAtUtc);

    private async Task<GroupProject> Project(Guid id, CancellationToken ct)
        => await db.GroupProjects.SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw ApiFault.NotFound();
    private async Task<ProjectTeam> Team(Guid id, CancellationToken ct)
        => await db.ProjectTeams.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct) ?? throw ApiFault.NotFound();

    private async Task<Guid> RequireTeamMember(ClaimsPrincipal principal, ProjectTeam team, CancellationToken ct)
    {
        var id = await access.Student(principal, await access.Section(team.SectionId, ct), ct);
        if (!await db.ProjectTeamMembers.AnyAsync(x => x.TeamId == team.Id && x.StudentId == id, ct))
            throw ApiFault.Forbidden();
        return id;
    }

    public async Task<TeamView> CreateTeam(ClaimsPrincipal principal, Guid sectionId, CreateTeam request, CancellationToken ct)
    {
        var section = await access.Section(sectionId, ct);
        var staffId = await access.Staff(principal, section, ct);
        var name = AdvancedLearningAccess.Text(request.Name, 120, "team name");
        if (await db.ProjectTeams.AnyAsync(x => x.SectionId == sectionId && x.Name == name, ct))
            throw ApiFault.Conflict("Team name already exists.");
        var team = new ProjectTeam { InstitutionId = section.InstitutionId, SectionId = sectionId,
            CreatedBy = staffId, Name = name };
        db.ProjectTeams.Add(team);
        await db.SaveChangesAsync(ct);
        return new TeamView(team.Id, sectionId, name, []);
    }

    public Task<TeamView> AddMember(ClaimsPrincipal principal, Guid teamId, Guid studentId, CancellationToken ct)
        => retry.ExecuteAsync<GroupLearningService, TeamView>(service => service.AddMemberCore(principal, teamId, studentId, ct), ct);

    private async Task<TeamView> AddMemberCore(ClaimsPrincipal principal, Guid teamId, Guid studentId, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await LockTeam(teamId, ct);
        var team = await Team(teamId, ct);
        await access.Staff(principal, await access.Section(team.SectionId, ct), ct);
        if (!await db.Users.AnyAsync(x => x.Id == studentId && x.InstitutionId == team.InstitutionId && x.IsActive, ct) ||
            !await db.UserRoles.Join(db.Roles, link => link.RoleId, role => role.Id,
                (link, role) => new { link.UserId, role.Name })
                .AnyAsync(x => x.UserId == studentId && x.Name == AcademixRoles.Student, ct) ||
            !await db.StudentEnrollments.AnyAsync(x => x.SectionId == team.SectionId && x.StudentId == studentId && x.Status == "Active", ct))
            throw ApiFault.Bad("Student must be active, have the Student role, and be enrolled in the section.");
        if (await db.GroupProjectSubmissions.AnyAsync(x => x.TeamId == teamId, ct))
            throw ApiFault.Conflict("Team membership is frozen after its first project draft.");
        if (await db.ProjectTeamMembers.AnyAsync(x => x.SectionId == team.SectionId && x.StudentId == studentId, ct))
            throw ApiFault.Conflict("Student already belongs to a team in this section.");
        db.ProjectTeamMembers.Add(new ProjectTeamMember { TeamId = team.Id, SectionId = team.SectionId,
            InstitutionId = team.InstitutionId, StudentId = studentId });
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { throw ApiFault.Conflict("Concurrent team membership change."); }
        await tx.CommitAsync(ct);
        return await ReadTeam(team.Id, ct);
    }

    public Task<TeamView> RemoveMember(ClaimsPrincipal principal, Guid teamId, Guid studentId, CancellationToken ct)
        => retry.ExecuteAsync<GroupLearningService, TeamView>(service => service.RemoveMemberCore(principal, teamId, studentId, ct), ct);

    private async Task<TeamView> RemoveMemberCore(ClaimsPrincipal principal, Guid teamId, Guid studentId, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await LockTeam(teamId, ct);
        var team = await Team(teamId, ct);
        await access.Staff(principal, await access.Section(team.SectionId, ct), ct);
        if (await db.GroupProjectSubmissions.AnyAsync(x => x.TeamId == teamId, ct))
            throw ApiFault.Conflict("Team membership is frozen after its first project draft.");
        var member = await db.ProjectTeamMembers.SingleOrDefaultAsync(x => x.TeamId == teamId && x.StudentId == studentId, ct)
            ?? throw ApiFault.NotFound();
        db.ProjectTeamMembers.Remove(member);
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return await ReadTeam(team.Id, ct);
    }

    private async Task<TeamView> ReadTeam(Guid teamId, CancellationToken ct)
    {
        var team = await Team(teamId, ct);
        var members = await db.ProjectTeamMembers.AsNoTracking().Where(x => x.TeamId == teamId)
            .OrderBy(x => x.StudentId).Select(x => x.StudentId).ToArrayAsync(ct);
        return new TeamView(team.Id, team.SectionId, team.Name, members);
    }

    public async Task<IReadOnlyList<TeamView>> Teams(ClaimsPrincipal principal, Guid sectionId, CancellationToken ct)
    {
        var section = await access.Section(sectionId, ct);
        var (actor, staff) = await access.Viewer(principal, section, ct);
        var teams = await db.ProjectTeams.AsNoTracking().Where(x => x.SectionId == sectionId &&
                (staff || db.ProjectTeamMembers.Any(m => m.TeamId == x.Id && m.StudentId == actor)))
            .OrderBy(x => x.Name).ToArrayAsync(ct);
        var ids = teams.Select(x => x.Id).ToArray();
        var members = await db.ProjectTeamMembers.AsNoTracking().Where(x => ids.Contains(x.TeamId)).ToListAsync(ct);
        return teams.Select(x => new TeamView(x.Id, sectionId, x.Name,
            members.Where(m => m.TeamId == x.Id).Select(m => m.StudentId).Order().ToArray())).ToArray();
    }

    public async Task<RubricView> CreateRubric(ClaimsPrincipal principal, Guid sectionId, CreateRubric request, CancellationToken ct)
    {
        var section = await access.Section(sectionId, ct);
        var staffId = await access.Staff(principal, section, ct);
        var name = AdvancedLearningAccess.Text(request.Name, 160, "rubric name");
        RubricPolicy.Validate(request.Criteria);
        var rubric = new GradingRubric { InstitutionId = section.InstitutionId, SectionId = sectionId,
            CreatedBy = staffId, Name = name };
        db.GradingRubrics.Add(rubric);
        foreach (var part in request.Criteria)
            db.RubricCriteria.Add(new RubricCriterion
            {
                InstitutionId = section.InstitutionId, SectionId = sectionId, RubricId = rubric.Id,
                Position = part.Position, Name = part.Name.Trim(), Description = part.Description?.Trim() ?? "",
                MaxPoints = part.MaxPoints
            });
        await db.SaveChangesAsync(ct);
        return await ReadRubric(rubric.Id, ct);
    }

    private async Task<RubricView> ReadRubric(Guid id, CancellationToken ct)
    {
        var rubric = await db.GradingRubrics.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw ApiFault.NotFound();
        var criteria = await db.RubricCriteria.AsNoTracking().Where(x => x.RubricId == id)
            .OrderBy(x => x.Position).Select(x => new RubricCriterionView(x.Id, x.Position, x.Name,
                x.Description, x.MaxPoints)).ToListAsync(ct);
        return new RubricView(rubric.Id, rubric.SectionId, rubric.Name, criteria.Sum(x => x.MaxPoints), criteria);
    }

    public async Task<IReadOnlyList<RubricView>> Rubrics(ClaimsPrincipal principal, Guid sectionId, CancellationToken ct)
    {
        var section = await access.Section(sectionId, ct);
        await access.Staff(principal, section, ct);
        var ids = await db.GradingRubrics.AsNoTracking().Where(x => x.SectionId == sectionId)
            .OrderBy(x => x.Name).Select(x => x.Id).Take(200).ToArrayAsync(ct);
        var result = new List<RubricView>();
        foreach (var id in ids) result.Add(await ReadRubric(id, ct));
        return result;
    }

    public async Task<GroupProjectView> CreateProject(ClaimsPrincipal principal, Guid sectionId,
        CreateGroupProject request, CancellationToken ct)
    {
        var section = await access.Section(sectionId, ct);
        await access.Staff(principal, section, ct);
        if (request.DueAtUtc.Kind != DateTimeKind.Utc || request.DueAtUtc <= DateTime.UtcNow ||
            (request.MaxScore is <= 0 or > 10000) || request.MaxScore != decimal.Round(request.MaxScore, 2))
            throw ApiFault.Bad("Due date must be future UTC, score must be 0-10000.");
        if (request.RubricId is Guid rubricId)
        {
            var rubric = await db.GradingRubrics.AsNoTracking().SingleOrDefaultAsync(x => x.Id == rubricId &&
                x.SectionId == sectionId && x.InstitutionId == section.InstitutionId, ct) ?? throw ApiFault.NotFound();
            var total = await db.RubricCriteria.AsNoTracking().Where(x => x.RubricId == rubric.Id).SumAsync(x => x.MaxPoints, ct);
            if (total != request.MaxScore) throw ApiFault.Bad("Project maximum must equal rubric criteria total.");
        }
        var project = new GroupProject { InstitutionId = section.InstitutionId, SectionId = sectionId,
            Title = AdvancedLearningAccess.Text(request.Title, 200, "project title"),
            Instructions = AdvancedLearningAccess.Text(request.Instructions, 20000, "project instructions"),
            DueAtUtc = request.DueAtUtc, MaxScore = request.MaxScore, RubricId = request.RubricId };
        db.GroupProjects.Add(project);
        await db.SaveChangesAsync(ct);
        return Map(project);
    }

    public async Task<GroupProjectView> Publish(ClaimsPrincipal principal, Guid id, PublishGroupProject request, CancellationToken ct)
    {
        var project = await Project(id, ct);
        await access.Staff(principal, await access.Section(project.SectionId, ct), ct);
        if (project.Version != request.ExpectedVersion) throw ApiFault.Conflict("Project changed.");
        if (request.IsPublished && project.DueAtUtc <= DateTime.UtcNow)
            throw ApiFault.Conflict("Cannot publish a project past its deadline.");
        project.IsPublished = request.IsPublished;
        project.Version++;
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { throw ApiFault.Conflict("Concurrent project update."); }
        return Map(project);
    }

    public async Task<IReadOnlyList<GroupProjectView>> Projects(ClaimsPrincipal principal, Guid sectionId, CancellationToken ct)
    {
        var section = await access.Section(sectionId, ct);
        var (_, staff) = await access.Viewer(principal, section, ct);
        return (await db.GroupProjects.AsNoTracking().Where(x => x.SectionId == sectionId && (staff || x.IsPublished))
            .OrderBy(x => x.DueAtUtc).Take(200).ToArrayAsync(ct)).Select(Map).ToArray();
    }

    public async Task<RubricView> PublishedProjectRubric(ClaimsPrincipal principal, Guid projectId, CancellationToken ct)
    {
        var project = await Project(projectId, ct);
        var (_, staff) = await access.Viewer(principal, await access.Section(project.SectionId, ct), ct);
        if (!staff && !project.IsPublished) throw ApiFault.NotFound();
        if (project.RubricId is not Guid id) throw ApiFault.NotFound();
        return await ReadRubric(id, ct);
    }

    private async Task LockTeam(Guid teamId, CancellationToken ct)
    {
        await db.ProjectTeams.FromSqlInterpolated(
            $"SELECT * FROM lms_project_teams WHERE \"Id\" = {teamId} FOR UPDATE").ToListAsync(ct);
    }

    private async Task LockProject(Guid projectId, CancellationToken ct)
    {
        var tx = db.Database.CurrentTransaction ?? throw new InvalidOperationException("Project transaction expected.");
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.Transaction = tx.GetDbTransaction();
        command.CommandText = "SELECT \"Id\" FROM lms_group_projects WHERE \"Id\" = @id FOR UPDATE";
        var p = command.CreateParameter(); p.ParameterName = "id"; p.Value = projectId;
        command.Parameters.Add(p);
        if (await command.ExecuteScalarAsync(ct) is null) throw ApiFault.NotFound();
    }

    public Task<GroupSubmissionView> SaveDraft(ClaimsPrincipal principal, Guid projectId, Guid teamId,
        UpdateGroupDraft request, CancellationToken ct)
        => retry.ExecuteAsync<GroupLearningService, GroupSubmissionView>(service => service.SaveDraftCore(principal, projectId, teamId, request, ct), ct);

    private async Task<GroupSubmissionView> SaveDraftCore(ClaimsPrincipal principal, Guid projectId, Guid teamId,
        UpdateGroupDraft request, CancellationToken ct)
    {
        var project = await Project(projectId, ct);
        var team = await Team(teamId, ct);
        if (team.SectionId != project.SectionId || team.InstitutionId != project.InstitutionId) throw ApiFault.NotFound();
        var student = await RequireTeamMember(principal, team, ct);
        var content = AdvancedLearningAccess.Text(request.Content, 20000, "group draft");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await LockTeam(teamId, ct);
        await RequireTeamMember(principal, team, ct);
        await LockProject(projectId, ct);
        var current = await db.GroupProjects.AsNoTracking().SingleAsync(x => x.Id == projectId, ct);
        if (!current.IsPublished || current.DueAtUtc <= DateTime.UtcNow) throw ApiFault.Conflict("Project closed.");
        var existing = await db.GroupProjectSubmissions.SingleOrDefaultAsync(x => x.ProjectId == projectId && x.TeamId == teamId, ct);
        if (existing is null)
        {
            if (request.ExpectedVersion is not null) throw ApiFault.Conflict("No draft exists yet.");
            existing = new GroupProjectSubmission { InstitutionId = team.InstitutionId, SectionId = team.SectionId,
                ProjectId = projectId, TeamId = teamId, LastEditedBy = student, Content = content };
            db.GroupProjectSubmissions.Add(existing);
        }
        else
        {
            if (existing.Status != "Draft" || request.ExpectedVersion != existing.Version)
                throw ApiFault.Conflict("Draft has been submitted or changed by another team member.");
            existing.Content = content; existing.LastEditedBy = student; existing.Version++;
        }
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { throw ApiFault.Conflict("Concurrent draft update."); }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: "23505" })
        { throw ApiFault.Conflict("Concurrent draft creation."); }
        await tx.CommitAsync(ct);
        return Map(existing);
    }

    public Task<GroupSubmissionView> Submit(ClaimsPrincipal principal, Guid projectId, Guid teamId, CancellationToken ct)
        => retry.ExecuteAsync<GroupLearningService, GroupSubmissionView>(service => service.SubmitCore(principal, projectId, teamId, ct), ct);

    private async Task<GroupSubmissionView> SubmitCore(ClaimsPrincipal principal, Guid projectId, Guid teamId, CancellationToken ct)
    {
        var project = await Project(projectId, ct);
        var team = await Team(teamId, ct);
        if (project.SectionId != team.SectionId || project.InstitutionId != team.InstitutionId) throw ApiFault.NotFound();
        await RequireTeamMember(principal, team, ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await LockProject(projectId, ct);
        var submission = await db.GroupProjectSubmissions.SingleOrDefaultAsync(x => x.ProjectId == projectId && x.TeamId == teamId, ct)
            ?? throw ApiFault.Conflict("Save a draft before submitting.");
        if (submission.Status is "Submitted" or "Graded") return Map(submission);
        var current = await db.GroupProjects.AsNoTracking().SingleAsync(x => x.Id == projectId, ct);
        if (!current.IsPublished || current.DueAtUtc <= DateTime.UtcNow) throw ApiFault.Conflict("Project closed.");
        submission.Status = "Submitted"; submission.SubmittedAtUtc = DateTime.UtcNow; submission.Version++;
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { throw ApiFault.Conflict("Concurrent submission."); }
        await tx.CommitAsync(ct);
        return Map(submission);
    }

    public async Task<GroupSubmissionView> MySubmission(ClaimsPrincipal principal, Guid projectId, CancellationToken ct)
    {
        var project = await Project(projectId, ct);
        if (!project.IsPublished) throw ApiFault.NotFound();
        var actor = await access.Student(principal, await access.Section(project.SectionId, ct), ct);
        var teamId = await db.ProjectTeamMembers.AsNoTracking().Where(x => x.SectionId == project.SectionId && x.StudentId == actor)
            .Select(x => (Guid?)x.TeamId).SingleOrDefaultAsync(ct);
        if (!teamId.HasValue) throw ApiFault.NotFound();
        var submission = await db.GroupProjectSubmissions.AsNoTracking().SingleOrDefaultAsync(x => x.ProjectId == projectId && x.TeamId == teamId, ct)
            ?? throw ApiFault.NotFound();
        return Map(submission);
    }

    public async Task<IReadOnlyList<GroupSubmissionView>> Submissions(ClaimsPrincipal principal, Guid projectId, CancellationToken ct)
    {
        var project = await Project(projectId, ct);
        await access.Staff(principal, await access.Section(project.SectionId, ct), ct);
        return (await db.GroupProjectSubmissions.AsNoTracking().Where(x => x.ProjectId == projectId)
            .OrderBy(x => x.SubmittedAtUtc).Take(300).ToArrayAsync(ct)).Select(Map).ToArray();
    }

    public Task<GroupSubmissionView> Grade(ClaimsPrincipal principal, Guid submissionId,
        GradeGroupSubmission request, CancellationToken ct)
        => retry.ExecuteAsync<GroupLearningService, GroupSubmissionView>(service => service.GradeCore(principal, submissionId, request, ct), ct);

    private async Task<GroupSubmissionView> GradeCore(ClaimsPrincipal principal, Guid submissionId,
        GradeGroupSubmission request, CancellationToken ct)
    {
        var initial=await db.GroupProjectSubmissions.AsNoTracking().SingleOrDefaultAsync(x=>x.Id==submissionId,ct)??throw ApiFault.NotFound();
        await using var tx=await db.Database.BeginTransactionAsync(ct);
        await Academix.Api.Services.Academic.RegistrationCoordination.Lock(db,initial.InstitutionId,ct);
        var submission = await db.GroupProjectSubmissions.SingleOrDefaultAsync(x => x.Id == submissionId, ct) ?? throw ApiFault.NotFound();
        var project = await Project(submission.ProjectId, ct);
        var actor = await access.Staff(principal, await access.Section(project.SectionId, ct), ct);
        if (submission.Status is not ("Submitted" or "Graded") || submission.Version != request.ExpectedVersion)
            throw ApiFault.Conflict("Submission cannot be graded in this version or state.");
        var reason = AdvancedLearningAccess.Text(request.Reason, 1000, "grading reason");
        var feedback = request.Feedback?.Trim() ?? "";
        if (feedback.Length > 5000) throw ApiFault.Bad("Feedback too long.");
        decimal score;
        IReadOnlyList<RubricCriterion> criteria = [];
        if (project.RubricId is Guid rubricId)
        {
            criteria = await db.RubricCriteria.AsNoTracking().Where(x => x.RubricId == rubricId)
                .OrderBy(x => x.Position).ToArrayAsync(ct);
            if (request.ManualScore is not null) throw ApiFault.Bad("Manual score not allowed for rubric project.");
            score = RubricPolicy.Total(criteria.Select(x => (x.Id, x.MaxPoints)).ToArray(), request.CriterionScores);
        }
        else
        {
            if (request.CriterionScores is { Count: > 0 } || request.ManualScore is not decimal manual ||
                manual < 0 || manual > project.MaxScore || manual != decimal.Round(manual, 2)) throw ApiFault.Bad("Invalid manual score.");
            score = manual;
        }
        if (score > project.MaxScore) throw ApiFault.Bad("Grade exceeds project maximum.");
        var original = submission.Score;
        if (criteria.Count > 0)
        {
            var existing = await db.RubricCriterionScores.Where(x => x.SubmissionId == submissionId).ToListAsync(ct);
            foreach (var part in request.CriterionScores!)
            {
                var item = existing.SingleOrDefault(x => x.CriterionId == part.CriterionId);
                if (item is null)
                    db.RubricCriterionScores.Add(new RubricCriterionScore { SubmissionId = submissionId,
                        CriterionId = part.CriterionId, Points = part.Points, Comment = part.Comment?.Trim() ?? "" });
                else { item.Points = part.Points; item.Comment = part.Comment?.Trim() ?? ""; }
            }
        }
        submission.Score = score; submission.Status = "Graded"; submission.GradedBy = actor;
        submission.GradedAtUtc = DateTime.UtcNow; submission.Feedback = feedback; submission.Version++;
        db.ProjectGradeAudits.Add(new ProjectGradeAudit { SubmissionId = submissionId, ActorId = actor,
            PreviousScore = original, NewScore = score, Reason = reason });
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { throw ApiFault.Conflict("Grade updated concurrently."); }
        var students=await db.ProjectTeamMembers.Where(x=>x.InstitutionId==submission.InstitutionId&&x.TeamId==submission.TeamId).Select(x=>x.StudentId).ToArrayAsync(ct);
        await AssessmentFreshness.Changed(db,submission.InstitutionId,submission.SectionId,"GroupProject",submission.ProjectId,students,actor,ct);
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);
        return Map(submission);
    }

    private async Task<GroupAssessmentView> AssessmentFor(GroupProjectSubmission submission, CancellationToken ct)
    {
        var scores = await db.RubricCriterionScores.AsNoTracking().Where(x => x.SubmissionId == submission.Id)
            .OrderBy(x => x.CriterionId)
            .Select(x => new CriterionScoreView(x.CriterionId, x.Points, x.Comment)).ToArrayAsync(ct);
        return new GroupAssessmentView(Map(submission), scores);
    }

    public async Task<GroupAssessmentView> MyAssessment(ClaimsPrincipal principal, Guid projectId, CancellationToken ct)
    {
        var project = await Project(projectId, ct);
        if (!project.IsPublished) throw ApiFault.NotFound();
        var user = await access.Student(principal, await access.Section(project.SectionId, ct), ct);
        var team = await db.ProjectTeamMembers.AsNoTracking().Where(x => x.SectionId == project.SectionId && x.StudentId == user)
            .Select(x => (Guid?)x.TeamId).SingleOrDefaultAsync(ct);
        if (team is null) throw ApiFault.NotFound();
        var submission = await db.GroupProjectSubmissions.AsNoTracking()
            .SingleOrDefaultAsync(x => x.TeamId == team && x.ProjectId == projectId && x.Status == "Graded", ct)
            ?? throw ApiFault.NotFound();
        return await AssessmentFor(submission, ct);
    }

    public async Task<GroupAssessmentView> StaffAssessment(ClaimsPrincipal principal, Guid submissionId, CancellationToken ct)
    {
        var submission = await db.GroupProjectSubmissions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == submissionId, ct)
            ?? throw ApiFault.NotFound();
        await access.Staff(principal, await access.Section(submission.SectionId, ct), ct);
        return await AssessmentFor(submission, ct);
    }

    public async Task<IReadOnlyList<GradeAuditView>> GradeHistory(ClaimsPrincipal principal, Guid submissionId, CancellationToken ct)
    {
        var submission = await db.GroupProjectSubmissions.AsNoTracking().SingleOrDefaultAsync(x => x.Id == submissionId, ct)
            ?? throw ApiFault.NotFound();
        await access.Staff(principal, await access.Section(submission.SectionId, ct), ct);
        return await db.ProjectGradeAudits.AsNoTracking().Where(x => x.SubmissionId == submissionId)
            .OrderBy(x => x.AtUtc).Select(x => new GradeAuditView(x.PreviousScore, x.NewScore, x.ActorId,
                x.Reason, x.AtUtc)).ToArrayAsync(ct);
    }
}
