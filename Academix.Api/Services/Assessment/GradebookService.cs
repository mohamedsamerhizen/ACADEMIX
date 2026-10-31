using Academix.Api.Services.Infrastructure;
using System.Security.Claims;
using Academix.Api.Data;
using Academix.Api.Domain.Academic;
using Academix.Api.Domain.Assessment;
using Academix.Api.Domain.Identity;
using Academix.Api.DTOs.Assessment;
using Academix.Api.Middleware;
using Academix.Api.Services.Identity;
using Microsoft.EntityFrameworkCore;

namespace Academix.Api.Services.Assessment;

public sealed partial class GradebookService(AcademixDbContext db, IdentitySecurity security, RetryingServiceWork retry)
{
    private static GradebookResultView ResultView(GradebookResult g)
        => new(g.SectionId, g.StudentId, g.Percentage, g.Letter, g.Passed, g.Version, g.PublishedAtUtc,g.RequiresReconciliation,g.IsManualOverride);

    private async Task<(CourseSection Section, ApplicationUser Actor)> Staff(ClaimsPrincipal principal,
        Guid sectionId, bool publish, CancellationToken ct)
    {
        var section = await db.CourseSections.AsNoTracking().SingleOrDefaultAsync(x => x.Id == sectionId, ct)
            ?? throw ApiFault.NotFound();
        var (actor, roles) = await security.CurrentAsync(principal, ct);
        if (roles.Contains(AcademixRoles.SuperAdministrator) ||
            roles.Contains(AcademixRoles.InstitutionAdministrator) && actor.InstitutionId == section.InstitutionId)
            return (section, actor);
        if (actor.InstitutionId != section.InstitutionId) throw ApiFault.NotFound();
        if (publish)
        {
            if (!roles.Contains(AcademixRoles.Instructor) ||
                !await db.TeachingAssignments.AnyAsync(x => x.InstitutionId == section.InstitutionId &&
                    x.SectionId == sectionId && x.UserId == actor.Id && x.Kind == AcademixRoles.Instructor, ct))
                throw ApiFault.Forbidden();
        }
        else if (!await security.IsCurrentSectionTeacherAsync(actor, roles, section.InstitutionId, sectionId, ct))
            throw ApiFault.Forbidden();
        return (section, actor);
    }

    private async Task<(CourseSection Section, ApplicationUser Actor)> Student(ClaimsPrincipal principal,
        Guid sectionId, CancellationToken ct)
    {
        var section = await db.CourseSections.AsNoTracking().SingleOrDefaultAsync(x => x.Id == sectionId, ct)
            ?? throw ApiFault.NotFound();
        var (actor, roles) = await security.CurrentAsync(principal, ct);
        if (actor.InstitutionId != section.InstitutionId || !roles.Contains(AcademixRoles.Student) ||
            !await db.StudentEnrollments.AnyAsync(x => x.InstitutionId == section.InstitutionId &&
                x.SectionId == sectionId && x.StudentId == actor.Id && x.Status == "Active", ct))
            throw ApiFault.NotFound();
        return (section, actor);
    }

    private async Task<SectionGradePolicy> LockPolicy(Guid sectionId, CancellationToken ct)
        => (await db.SectionGradePolicies.FromSqlInterpolated(
            $"SELECT * FROM gradebook_policies WHERE \"SectionId\" = {sectionId} FOR UPDATE")
            .ToListAsync(ct)).SingleOrDefault() ?? throw ApiFault.NotFound();

    private async Task<SectionGradePolicy> OpenPolicy(Guid sectionId, CancellationToken ct)
    {
        var policy = await LockPolicy(sectionId, ct);
        if (policy.PublishedAtUtc.HasValue) throw ApiFault.Conflict("Published gradebooks cannot be edited.");
        return policy;
    }

    private async Task<GradePolicyView> ViewPolicy(Guid sectionId, CancellationToken ct)
    {
        var policy = await db.SectionGradePolicies.AsNoTracking().SingleOrDefaultAsync(x => x.SectionId == sectionId, ct)
            ?? throw ApiFault.NotFound();
        var cats = await db.GradeCategories.AsNoTracking().Where(x => x.SectionId == sectionId)
            .OrderBy(x => x.Name).ToListAsync(ct);
        var comps = await db.GradeComponents.AsNoTracking().Where(x => x.SectionId == sectionId)
            .OrderBy(x => x.Name).ToListAsync(ct);
        return new GradePolicyView(sectionId, policy.PassingPercentage, policy.Version, policy.PublishedAtUtc,
            cats.Select(c => new GradeCategoryView(c.Id, c.Name, c.WeightPercentage,
                comps.Where(x => x.CategoryId == c.Id).Select(x =>
                    new GradeComponentView(x.Id, x.CategoryId, x.Name, x.SourceKind, x.SourceId,
                        x.WeightPercentage, x.MaximumPoints)).ToArray())).ToArray());
    }

    public async Task<GradePolicyView> Policy(ClaimsPrincipal principal, Guid sectionId, CancellationToken ct)
    {
        await Staff(principal, sectionId, false, ct);
        return await ViewPolicy(sectionId, ct);
    }

    public Task<GradePolicyView> Configure(ClaimsPrincipal principal, Guid sectionId,
        ConfigureGradePolicy request, CancellationToken ct)
        => retry.ExecuteAsync<GradebookService, GradePolicyView>(service => service.ConfigureCore(principal, sectionId, request, ct), ct);

    private async Task<GradePolicyView> ConfigureCore(ClaimsPrincipal principal, Guid sectionId,
        ConfigureGradePolicy request, CancellationToken ct)
    {
        var (section, actor) = await Staff(principal, sectionId, true, ct);
        if (request.PassingPercentage is < 0 or > 100 ||
            request.PassingPercentage != decimal.Round(request.PassingPercentage, 2))
            throw ApiFault.Bad("Passing percentage must be between 0 and 100.");
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            // Section row serializes first-time policy creation.
            await db.CourseSections.FromSqlInterpolated(
                $"SELECT * FROM course_sections WHERE \"Id\" = {sectionId} FOR UPDATE")
                .ToListAsync(ct);
            var policy = await db.SectionGradePolicies.SingleOrDefaultAsync(x => x.SectionId == sectionId, ct);
            if (policy is null)
            {
                if (request.ExpectedVersion != 0) throw ApiFault.Conflict("Policy must be created with expected version 0.");
                db.SectionGradePolicies.Add(new SectionGradePolicy { SectionId = sectionId,
                    InstitutionId = section.InstitutionId, PassingPercentage = request.PassingPercentage,
                    UpdatedBy = actor.Id });
            }
            else
            {
                if (policy.PublishedAtUtc.HasValue || policy.Version != request.ExpectedVersion)
                    throw ApiFault.Conflict("Policy is published or changed concurrently.");
                policy.PassingPercentage = request.PassingPercentage; policy.Version++;
                policy.UpdatedBy = actor.Id;
            }
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        });
        return await ViewPolicy(sectionId, ct);
    }

    public Task<GradePolicyView> AddCategory(ClaimsPrincipal principal, Guid sectionId,
        GradeCategoryInput request, CancellationToken ct)
        => retry.ExecuteAsync<GradebookService, GradePolicyView>(service => service.AddCategoryCore(principal, sectionId, request, ct), ct);

    private async Task<GradePolicyView> AddCategoryCore(ClaimsPrincipal principal, Guid sectionId,
        GradeCategoryInput request, CancellationToken ct)
    {
        var (section, actor) = await Staff(principal, sectionId, true, ct);
        GradebookPolicy.Weight(request.WeightPercentage);
        var name = Required(request.Name, 120);
        await MutateDraft(sectionId, actor.Id, async () =>
        {
            if (await db.GradeCategories.AnyAsync(x => x.SectionId == sectionId && x.Name == name, ct))
                throw ApiFault.Conflict("Category name already exists.");
            db.GradeCategories.Add(new GradeCategory { InstitutionId = section.InstitutionId,
                SectionId = sectionId, Name = name, WeightPercentage = request.WeightPercentage });
        }, ct);
        return await ViewPolicy(sectionId, ct);
    }

    public Task<GradePolicyView> DeleteCategory(ClaimsPrincipal principal, Guid id, CancellationToken ct)
        => retry.ExecuteAsync<GradebookService, GradePolicyView>(service => service.DeleteCategoryCore(principal, id, ct), ct);

    private async Task<GradePolicyView> DeleteCategoryCore(ClaimsPrincipal principal, Guid id, CancellationToken ct)
    {
        var category = await db.GradeCategories.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw ApiFault.NotFound();
        var (_, actor) = await Staff(principal, category.SectionId, true, ct);
        await MutateDraft(category.SectionId, actor.Id, async () =>
        {
            if (await db.GradeComponents.AnyAsync(x => x.CategoryId == id, ct))
                throw ApiFault.Conflict("Remove category components first.");
            db.GradeCategories.Remove(await db.GradeCategories.SingleAsync(x => x.Id == id, ct));
        }, ct);
        return await ViewPolicy(category.SectionId, ct);
    }

    public Task<GradePolicyView> AddComponent(ClaimsPrincipal principal, Guid sectionId,
        GradeComponentInput request, CancellationToken ct)
        => retry.ExecuteAsync<GradebookService, GradePolicyView>(service => service.AddComponentCore(principal, sectionId, request, ct), ct);

    private async Task<GradePolicyView> AddComponentCore(ClaimsPrincipal principal, Guid sectionId,
        GradeComponentInput request, CancellationToken ct)
    {
        var (section, actor) = await Staff(principal, sectionId, true, ct);
        GradebookPolicy.Weight(request.WeightPercentage);
        GradebookPolicy.ValidateSource(request.SourceKind, request.SourceId);
        var name = Required(request.Name, 160);
        if (request.MaximumPoints <= 0 || request.MaximumPoints > 100000 ||
            request.MaximumPoints != decimal.Round(request.MaximumPoints, 2))
            throw ApiFault.Bad("Invalid component maximum.");
        await MutateDraft(sectionId, actor.Id, async () =>
        {
            if (!await db.GradeCategories.AnyAsync(x => x.Id == request.CategoryId &&
                x.SectionId == sectionId && x.InstitutionId == section.InstitutionId, ct))
                throw ApiFault.Bad("Category belongs to a different section.");
            if (await db.GradeComponents.AnyAsync(x => x.CategoryId == request.CategoryId && x.Name == name, ct))
                throw ApiFault.Conflict("Component name already exists in category.");
            if (request.SourceId is Guid source && await db.GradeComponents.AnyAsync(x => x.SectionId == sectionId &&
                x.SourceKind == request.SourceKind && x.SourceId == source, ct))
                throw ApiFault.Conflict("Source is already included in this gradebook.");
            if (!await ValidSource(sectionId, request.SourceKind, request.SourceId, request.MaximumPoints, ct))
                throw ApiFault.Bad("Source missing, unpublished or maximum score does not match this section.");
            db.GradeComponents.Add(new GradeComponent { InstitutionId = section.InstitutionId,
                SectionId = sectionId, CategoryId = request.CategoryId, Name = name,
                SourceKind = request.SourceKind, SourceId = request.SourceId,
                WeightPercentage = request.WeightPercentage, MaximumPoints = request.MaximumPoints });
        }, ct);
        return await ViewPolicy(sectionId, ct);
    }

    private async Task<bool> ValidSource(Guid sectionId, string kind, Guid? id, decimal maximum, CancellationToken ct)
        => kind switch
        {
            "Manual" => true,
            "Assignment" => await db.LearningAssignments.AnyAsync(x => x.Id == id && x.SectionId == sectionId && x.IsPublished && x.MaxScore == maximum, ct),
            "Exam" => await db.Exams.AnyAsync(x => x.Id == id && x.SectionId == sectionId && x.Status == "Published", ct),
            "GroupProject" => await db.GroupProjects.AnyAsync(x => x.Id == id && x.SectionId == sectionId && x.IsPublished && x.MaxScore == maximum, ct),
            _ => false
        };

    public Task<GradePolicyView> DeleteComponent(ClaimsPrincipal principal, Guid id, CancellationToken ct)
        => retry.ExecuteAsync<GradebookService, GradePolicyView>(service => service.DeleteComponentCore(principal, id, ct), ct);

    private async Task<GradePolicyView> DeleteComponentCore(ClaimsPrincipal principal, Guid id, CancellationToken ct)
    {
        var component = await db.GradeComponents.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw ApiFault.NotFound();
        var (_, actor) = await Staff(principal, component.SectionId, true, ct);
        await MutateDraft(component.SectionId, actor.Id, async () =>
        {
            if (await db.GradeManualEntries.AnyAsync(x => x.ComponentId == id, ct))
                throw ApiFault.Conflict("Component has recorded manual grades.");
            db.GradeComponents.Remove(await db.GradeComponents.SingleAsync(x => x.Id == id, ct));
        }, ct);
        return await ViewPolicy(component.SectionId, ct);
    }

    private async Task MutateDraft(Guid sectionId, Guid actor, Func<Task> change, CancellationToken ct)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var policy = await OpenPolicy(sectionId, ct);
            await change();
            policy.Version++; policy.UpdatedBy = actor;
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        });
    }

    public Task<ManualGradeView> ManualGrade(ClaimsPrincipal principal, Guid componentId,
        Guid studentId, ManualGradeRequest request, CancellationToken ct)
        => retry.ExecuteAsync<GradebookService, ManualGradeView>(service => service.ManualGradeCore(principal, componentId, studentId, request, ct), ct);

    private async Task<ManualGradeView> ManualGradeCore(ClaimsPrincipal principal, Guid componentId,
        Guid studentId, ManualGradeRequest request, CancellationToken ct)
    {
        var component = await db.GradeComponents.AsNoTracking().SingleOrDefaultAsync(x => x.Id == componentId, ct)
            ?? throw ApiFault.NotFound();
        var (section, actor) = await Staff(principal, component.SectionId, false, ct);
        if (component.SourceKind != "Manual" || request.Points < 0 ||
            request.Points > component.MaximumPoints || request.Points != decimal.Round(request.Points, 2))
            throw ApiFault.Bad("Invalid manual grade.");
        if (!await db.StudentEnrollments.AnyAsync(x => x.SectionId == section.Id &&
            x.InstitutionId == section.InstitutionId && x.StudentId == studentId && x.Status == "Active", ct))
            throw ApiFault.Bad("Student not actively enrolled.");
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await OpenPolicy(section.Id, ct);
            var entry = await db.GradeManualEntries.SingleOrDefaultAsync(x => x.ComponentId == componentId && x.StudentId == studentId, ct);
            if (entry is null)
            {
                if (request.ExpectedVersion is not null and not 0) throw ApiFault.Conflict("Expected version 0 for new grade.");
                entry = new GradeManualEntry { SectionId = section.Id, InstitutionId = section.InstitutionId,
                    ComponentId = componentId, StudentId = studentId, Points = request.Points, GradedBy = actor.Id };
                db.GradeManualEntries.Add(entry);
            }
            else
            {
                if (request.ExpectedVersion != entry.Version)
                    throw ApiFault.Conflict("Stale manual grade version.");
                entry.Points = request.Points; entry.Version++; entry.GradedBy = actor.Id;
                entry.UpdatedAtUtc = DateTime.UtcNow;
            }
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return new ManualGradeView(componentId, studentId, entry.Points, entry.Version);
        });
    }

    private static string Required(string? text, int max)
    {
        var cleaned = text?.Trim() ?? "";
        if (cleaned.Length == 0 || cleaned.Length > max) throw ApiFault.Bad("Invalid text length.");
        return cleaned;
    }

    private async Task<GradebookPreview> Compute(Guid sectionId, CancellationToken ct)
    {
        var policy = await db.SectionGradePolicies.AsNoTracking().SingleOrDefaultAsync(x => x.SectionId == sectionId, ct)
            ?? throw ApiFault.NotFound();
        var cats = await db.GradeCategories.AsNoTracking().Where(x => x.SectionId == sectionId).ToArrayAsync(ct);
        var items = await db.GradeComponents.AsNoTracking().Where(x => x.SectionId == sectionId).ToArrayAsync(ct);
        if (cats.Length == 0 || cats.Sum(x => x.WeightPercentage) != 100m ||
            cats.Any(x => items.Where(i => i.CategoryId == x.Id).Sum(i => i.WeightPercentage) != 100m))
            throw ApiFault.Conflict("Gradebook weights are incomplete: categories and their components must sum to 100%.");
        var students = await db.StudentEnrollments.AsNoTracking().Where(x => x.SectionId == sectionId &&
            x.InstitutionId == policy.InstitutionId && x.Status == "Active")
            .Select(x => x.StudentId).Distinct().OrderBy(x => x).ToArrayAsync(ct);
        if (students.Length == 0) throw ApiFault.Conflict("No active enrolled students to grade.");

        var scores = await LoadScores(sectionId, items, ct);
        var previews = new List<GradeStudentPreview>();
        foreach (var studentId in students)
        {
            var lines = new List<GradeLineView>();
            var weighted = new List<(decimal CategoryWeight, IReadOnlyList<(decimal Weight, decimal Earned, decimal Maximum)> Items)>();
            foreach (var cat in cats)
            {
                var catItems = new List<(decimal Weight, decimal Earned, decimal Maximum)>();
                foreach (var item in items.Where(x => x.CategoryId == cat.Id))
                {
                    decimal? earned = scores.GetValueOrDefault((item.Id, studentId));
                    if (earned.HasValue && (earned.Value < 0 || earned.Value > item.MaximumPoints))
                        throw ApiFault.Conflict("Grade exceeds component maximum; verify source configuration.");
                    lines.Add(new GradeLineView(item.Id, item.Name, item.SourceKind, earned,
                        item.MaximumPoints, item.WeightPercentage, !earned.HasValue));
                    if (earned.HasValue) catItems.Add((item.WeightPercentage, earned.Value, item.MaximumPoints));
                }
                weighted.Add((cat.WeightPercentage, catItems));
            }
            bool complete = lines.Count == items.Length && lines.All(x => !x.IsMissing);
            decimal? percent = complete ? GradebookPolicy.Calculate(weighted) : null;
            previews.Add(new GradeStudentPreview(studentId, percent, complete, lines));
        }
        return new GradebookPreview(sectionId, students.Length, previews.Count(x => !x.IsComplete), previews);
    }

    private async Task<Dictionary<(Guid Component, Guid Student), decimal?>> LoadScores(
        Guid sectionId, GradeComponent[] items, CancellationToken ct)
    {
        var manual = await db.GradeManualEntries.AsNoTracking().Where(x => x.SectionId == sectionId).ToListAsync(ct);
        var assignments = await db.LearningSubmissions.AsNoTracking().Where(x => x.SectionId == sectionId && x.Status == "Graded").ToListAsync(ct);
        var exams = await db.ExamGradeReports.AsNoTracking().Where(x => x.SectionId == sectionId && x.Status == "Published").ToListAsync(ct);
        var teams = await db.ProjectTeamMembers.AsNoTracking().Where(x => x.SectionId == sectionId).ToListAsync(ct);
        var projects = await db.GroupProjectSubmissions.AsNoTracking().Where(x => x.SectionId == sectionId && x.Status == "Graded").ToListAsync(ct);
        var result = new Dictionary<(Guid, Guid), decimal?>();
        foreach (var item in items)
        {
            switch (item.SourceKind)
            {
                case "Manual":
                    foreach (var x in manual.Where(x => x.ComponentId == item.Id)) result[(item.Id, x.StudentId)] = x.Points;
                    break;
                case "Assignment":
                    foreach (var x in assignments.Where(x => x.AssignmentId == item.SourceId)) result[(item.Id, x.StudentId)] = x.Score;
                    break;
                case "Exam":
                    foreach (var group in exams.Where(x => x.ExamId == item.SourceId).GroupBy(x => x.StudentId))
                        result[(item.Id, group.Key)] = decimal.Round(group.Max(x => GradebookPolicy.Percent(x.EarnedPoints, x.MaximumPoints)) * item.MaximumPoints / 100m, 2, MidpointRounding.AwayFromZero);
                    break;
                case "GroupProject":
                    var byTeam = projects.Where(x => x.ProjectId == item.SourceId).ToDictionary(x => x.TeamId);
                    foreach (var member in teams)
                        if (byTeam.TryGetValue(member.TeamId, out var submission)) result[(item.Id, member.StudentId)] = submission.Score;
                    break;
                default: throw ApiFault.Bad("Unsupported grade component.");
            }
        }
        return result;
    }

    public async Task<GradebookPreview> Preview(ClaimsPrincipal principal, Guid sectionId, CancellationToken ct)
    {
        await Staff(principal, sectionId, false, ct);
        return await Compute(sectionId, ct);
    }

    public Task<IReadOnlyList<GradebookResultView>> Publish(ClaimsPrincipal principal,
        Guid sectionId, PublishGradebookRequest request, CancellationToken ct)
        => retry.ExecuteAsync<GradebookService, IReadOnlyList<GradebookResultView>>(service => service.PublishCore(principal, sectionId, request, ct), ct);

    private async Task<IReadOnlyList<GradebookResultView>> PublishCore(ClaimsPrincipal principal,
        Guid sectionId, PublishGradebookRequest request, CancellationToken ct)
    {
        var (section, actor) = await Staff(principal, sectionId, true, ct);
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await Academix.Api.Services.Academic.RegistrationCoordination.Lock(db, section.InstitutionId, ct);
            await Staff(principal,sectionId,true,ct);
            // Lock section first to serialize publication against enrollment.
            await db.CourseSections.FromSqlInterpolated(
                $"SELECT * FROM course_sections WHERE \"Id\" = {sectionId} FOR UPDATE")
                .ToListAsync(ct);
            var policy = await OpenPolicy(sectionId, ct);
            if (policy.InstitutionId != section.InstitutionId || policy.Version != request.ExpectedVersion)
                throw ApiFault.Conflict("Gradebook version changed.");
            var preview = await Compute(sectionId, ct);
            if (preview.IncompleteStudents > 0)
                throw ApiFault.Conflict("Cannot publish while student grades are incomplete.");
            var evidence=await AssessmentFreshness.Capture(db,section.InstitutionId,sectionId,preview.Students.Select(x=>x.StudentId).ToArray(),ct);
            var now = DateTime.UtcNow;
            foreach (var student in preview.Students)
            {
                var percent = student.Percentage!.Value;
                db.GradebookResults.Add(new GradebookResult { SectionId = sectionId,
                    InstitutionId = section.InstitutionId, StudentId = student.StudentId,
                    Percentage = percent, Passed = percent >= policy.PassingPercentage,
                    Letter = GradebookPolicy.Letter(percent), PublishedAtUtc = now, SourceEvidenceJson=evidence[student.StudentId] });
                db.GradebookAudits.Add(new GradebookAudit { SectionId = sectionId,
                    InstitutionId = section.InstitutionId, StudentId = student.StudentId,
                    ActorId = actor.Id, Action = "Published", NewPercentage = percent,
                    Reason = "Initial gradebook publication", AtUtc = now, EvidenceJson=evidence[student.StudentId] });
            }
            policy.PublishedAtUtc = now; policy.Version++; policy.UpdatedBy = actor.Id;
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return await db.GradebookResults.AsNoTracking().Where(x => x.SectionId == sectionId)
                .OrderBy(x => x.StudentId).Select(x => new GradebookResultView(x.SectionId, x.StudentId,
                    x.Percentage, x.Letter, x.Passed, x.Version, x.PublishedAtUtc,x.RequiresReconciliation,x.IsManualOverride)).ToArrayAsync(ct);
        }
    }

    public async Task<IReadOnlyList<GradebookResultView>> Results(ClaimsPrincipal principal,
        Guid sectionId, CancellationToken ct)
    {
        await Staff(principal, sectionId, false, ct);
        return await db.GradebookResults.AsNoTracking().Where(x => x.SectionId == sectionId)
            .OrderBy(x => x.StudentId).Select(x => new GradebookResultView(x.SectionId, x.StudentId,
                x.Percentage, x.Letter, x.Passed, x.Version, x.PublishedAtUtc,x.RequiresReconciliation,x.IsManualOverride)).ToArrayAsync(ct);
    }

    public async Task<GradebookResultView> MyResult(ClaimsPrincipal principal, Guid sectionId, CancellationToken ct)
    {
        var (_, student) = await Student(principal, sectionId, ct);
        var result = await db.GradebookResults.AsNoTracking().SingleOrDefaultAsync(x =>
            x.SectionId == sectionId && x.StudentId == student.Id, ct) ?? throw ApiFault.NotFound();
        return ResultView(result);
    }

    public Task<GradebookResultView> Correct(ClaimsPrincipal principal, Guid sectionId, Guid studentId,
        CorrectGradebookResult request, CancellationToken ct)
        => retry.ExecuteAsync<GradebookService, GradebookResultView>(service => service.CorrectCore(principal, sectionId, studentId, request, ct), ct);

    private async Task<GradebookResultView> CorrectCore(ClaimsPrincipal principal, Guid sectionId, Guid studentId,
        CorrectGradebookResult request, CancellationToken ct)
    {
        var (_, actor) = await Staff(principal, sectionId, true, ct);
        if (request.Percentage is < 0 or > 100 || request.Percentage != decimal.Round(request.Percentage, 2))
            throw ApiFault.Bad("Invalid percentage.");
        var reason = Required(request.Reason, 1000);
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            await Academix.Api.Services.Academic.RegistrationCoordination.Lock(db, (await db.CourseSections.AsNoTracking().SingleAsync(x => x.Id == sectionId, ct)).InstitutionId, ct);
            await Staff(principal,sectionId,true,ct);
            var policy = await LockPolicy(sectionId, ct);
            if (!policy.PublishedAtUtc.HasValue) throw ApiFault.Conflict("Gradebook is not published.");
            var result = await db.GradebookResults.SingleOrDefaultAsync(x =>
                x.SectionId == sectionId && x.StudentId == studentId, ct) ?? throw ApiFault.NotFound();
            if (result.Version != request.ExpectedVersion) throw ApiFault.Conflict("Stale result version.");
            var previous = result.Percentage;
            var previousEvidence=result.SourceEvidenceJson;
            result.SourceEvidenceJson=(await AssessmentFreshness.Capture(db,policy.InstitutionId,sectionId,[studentId],ct))[studentId];
            result.RequiresReconciliation=false;result.IsManualOverride=true;
            result.Percentage = request.Percentage;
            result.Passed = request.Percentage >= policy.PassingPercentage;
            result.Letter = GradebookPolicy.Letter(request.Percentage);
            result.Version++;
            db.GradebookAudits.Add(new GradebookAudit { SectionId = sectionId,
                InstitutionId = policy.InstitutionId, StudentId = studentId, ActorId = actor.Id,
                Action = "Corrected", PreviousPercentage = previous, NewPercentage = result.Percentage,
                Reason = reason, EvidenceJson=System.Text.Json.JsonSerializer.Serialize(new{Previous=previousEvidence,Current=result.SourceEvidenceJson,ManualOverride=true}) });
            await AcademicFreshness.Invalidate(db,policy.InstitutionId,studentId,false,ct);
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return ResultView(result);
        }
    }

    public async Task<IReadOnlyList<GradebookAuditView>> Audit(ClaimsPrincipal principal,
        Guid sectionId, CancellationToken ct)
    {
        await Staff(principal, sectionId, true, ct);
        return await db.GradebookAudits.AsNoTracking().Where(x => x.SectionId == sectionId)
            .OrderByDescending(x => x.AtUtc).Take(500).Select(x => new GradebookAuditView(x.StudentId,
                x.ActorId, x.Action, x.PreviousPercentage, x.NewPercentage, x.Reason, x.AtUtc,x.EvidenceJson))
            .ToArrayAsync(ct);
    }

    public async Task<TermGradeSummary> MyTerm(ClaimsPrincipal principal, Guid termId, CancellationToken ct)
    {
        var (actor, roles) = await security.CurrentAsync(principal, ct);
        if (!roles.Contains(AcademixRoles.Student) || actor.InstitutionId is not Guid inst)
            throw ApiFault.Forbidden();
        if (!await db.AcademicTerms.AnyAsync(x => x.Id == termId && x.InstitutionId == inst, ct))
            throw ApiFault.NotFound();
        var result = await (from g in db.GradebookResults.AsNoTracking()
            join s in db.CourseSections.AsNoTracking() on g.SectionId equals s.Id
            where g.StudentId == actor.Id && g.InstitutionId == inst && s.TermId == termId &&
                db.StudentEnrollments.Any(e => e.SectionId == g.SectionId && e.StudentId == actor.Id && e.Status == "Active")
            orderby s.Code
            select new GradebookResultView(g.SectionId, g.StudentId, g.Percentage,
                g.Letter, g.Passed, g.Version, g.PublishedAtUtc,g.RequiresReconciliation,g.IsManualOverride)).ToArrayAsync(ct);
        var average = result.Length == 0 ? 0m : decimal.Round(result.Average(x => x.Percentage), 2);
        // This endpoint deliberately returns the unweighted section percentage average, not GPA.
        return new TermGradeSummary(termId, actor.Id, result.Length, average, result);
    }
}
