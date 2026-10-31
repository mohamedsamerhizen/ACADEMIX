using System.Security.Claims;
using Academix.Api.Data;
using Academix.Api.Domain.Advising;
using Academix.Api.Domain.Identity;
using Academix.Api.DTOs.Advising;
using Academix.Api.DTOs.Workspaces;
using Academix.Api.Middleware;
using Academix.Api.Services.Identity;
using Microsoft.EntityFrameworkCore;

namespace Academix.Api.Services.Advising;

/// <summary>Scoped student-success interventions. Internal notes are never exposed to students.</summary>
public sealed partial class AdvisingService(AcademixDbContext db, IdentitySecurity security,
    AcademicProgressAccess progressAccess, Academix.Api.Services.Infrastructure.RetryingServiceWork retry)
{
    private static AdvisingCaseView Map(AdvisingCase c) => new(c.Id, c.InstitutionId, c.SectionId,
        c.StudentId, c.Title, c.InternalSummary, c.Status, c.Version, c.CreatedAtUtc, c.UpdatedAtUtc);
    private static AdvisingTaskView Map(AdvisingTask t) => new(t.Id, t.Description, t.DueAtUtc,
        t.Status, t.Version, t.ReportedAtUtc, t.ReviewedAtUtc);
    private static AdvisingEventView Map(AdvisingEvent e) => new(e.Id, e.ActorId, e.Kind, e.Description, e.AtUtc);

    private async Task<(Guid InstitutionId, Guid SectionId)> StaffSection(ClaimsPrincipal user, Guid sectionId, CancellationToken ct)
    {
        var s = await db.CourseSections.AsNoTracking().Where(x => x.Id == sectionId)
            .Select(x => new { x.InstitutionId, x.Id }).SingleOrDefaultAsync(ct) ?? throw ApiFault.NotFound();
        await security.RequireSectionStaffAsync(user, s.InstitutionId, s.Id, ct);
        return (s.InstitutionId, s.Id);
    }

    private async Task<AdvisingCase> Case(Guid id, CancellationToken ct)
        => await db.AdvisingCases.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw ApiFault.NotFound();

    private async Task<AdvisingCase> StaffCase(ClaimsPrincipal user, Guid id, CancellationToken ct)
    {
        var item = await Case(id, ct);
        await security.RequireSectionStaffAsync(user, item.InstitutionId, item.SectionId, ct);
        return item;
    }

    private async Task<AdvisingCase> LockCase(Guid id, CancellationToken ct)
        => (await db.AdvisingCases.FromSqlInterpolated(
            $"SELECT * FROM advising_cases WHERE \"Id\" = {id} FOR UPDATE").ToListAsync(ct))
            .SingleOrDefault() ?? throw ApiFault.NotFound();

    private void Event(AdvisingCase item, Guid actorId, string kind, string description)
    {
        db.AdvisingEvents.Add(new AdvisingEvent { InstitutionId = item.InstitutionId,
            CaseId = item.Id, ActorId = actorId, Kind = kind, Description = description });
        security.Audit(actorId, item.InstitutionId, "advising." + kind, item.Id.ToString());
    }

    public async Task<PageResult<StudentRisk>> Risks(ClaimsPrincipal user, Guid sectionId, int page, CancellationToken ct)
    {
        var offset = AdvisingPolicy.Offset(page);
        var (institutionId, _) = await StaffSection(user, sectionId, ct);
        var registrations = db.StudentEnrollments.AsNoTracking().Where(x =>
            x.InstitutionId == institutionId && x.SectionId == sectionId && x.Status == "Active");
        var count = await registrations.CountAsync(ct);
        var students = await (from e in registrations
            join u in db.Users.AsNoTracking() on e.StudentId equals u.Id
            orderby u.Email, u.Id
            select new { e.StudentId, u.Email })
            .Skip(offset).Take(AdvisingPolicy.PageSize).ToListAsync(ct);
        var ids = students.Select(x => x.StudentId).ToArray();
        var attendance = await db.AttendanceRecords.AsNoTracking().Where(x => x.InstitutionId == institutionId &&
            x.SectionId == sectionId && ids.Contains(x.StudentId) &&
            (x.Status == "Present" || x.Status == "Late" || x.Status == "Absent"))
            .GroupBy(x => x.StudentId).Select(g => new { StudentId = g.Key, Marks = g.Count(),
                Attended = g.Count(x => x.Status == "Present" || x.Status == "Late") }).ToListAsync(ct);
        var grades = await db.GradebookResults.AsNoTracking().Where(x => x.InstitutionId == institutionId &&
            x.SectionId == sectionId && ids.Contains(x.StudentId))
            .Select(x => new { x.StudentId, x.Percentage,x.RequiresReconciliation }).ToListAsync(ct);
        var cases = await db.AdvisingCases.AsNoTracking().Where(x => x.InstitutionId == institutionId &&
            x.SectionId == sectionId && ids.Contains(x.StudentId) && x.Status != "Resolved")
            .GroupBy(x => x.StudentId).Select(g => new { StudentId = g.Key, Count = g.Count() }).ToListAsync(ct);
        var passing = await db.SectionGradePolicies.AsNoTracking().Where(x => x.InstitutionId == institutionId &&
            x.SectionId == sectionId).Select(x => (decimal?)x.PassingPercentage).SingleOrDefaultAsync(ct) ?? 50m;
        var a = attendance.ToDictionary(x => x.StudentId);
        var g = grades.ToDictionary(x => x.StudentId);
        var c = cases.ToDictionary(x => x.StudentId, x => x.Count);
        return new PageResult<StudentRisk>(page, AdvisingPolicy.PageSize, count,
            students.Select(s =>
            {
                var marks = a.GetValueOrDefault(s.StudentId);
                var n = marks?.Marks ?? 0;
                var present = marks?.Attended ?? 0;
                var pending=g.TryGetValue(s.StudentId,out var value)&&value.RequiresReconciliation;
                var grade=value is not null&&!pending?(decimal?)value.Percentage:null;
                return new StudentRisk(s.StudentId, s.Email, n,
                    AdvisingPolicy.AttendanceRate(n, present), grade, passing,
                    pending?"AssessmentReconciliationRequired":AdvisingPolicy.RiskIndicator(n, present, grade, passing), c.GetValueOrDefault(s.StudentId),pending);
            }).ToArray());
    }

    public async Task<AdvisingCaseView> Open(ClaimsPrincipal user, Guid sectionId, CreateAdvisingCase request, CancellationToken ct)
    {
        var (institutionId, _) = await StaffSection(user, sectionId, ct);
        var actor = (await security.CurrentAsync(user, ct)).User;
        if (request.ClientRequestId == Guid.Empty || request.StudentId == Guid.Empty)
            throw ApiFault.Bad("ClientRequestId and StudentId are required.");
        var title = AdvisingPolicy.Required(request.Title, 160, "title");
        var summary = AdvisingPolicy.Required(request.InternalSummary, 3000, "internal summary");
        var prior = await db.AdvisingCases.AsNoTracking().SingleOrDefaultAsync(x =>
            x.InstitutionId == institutionId && x.ClientRequestId == request.ClientRequestId, ct);
        if (prior is not null)
        {
            if (prior.SectionId != sectionId || prior.StudentId != request.StudentId ||
                prior.OpenedBy != actor.Id || prior.Title != title || prior.InternalSummary != summary)
                throw ApiFault.Conflict("Request ID was used with different details.");
            return Map(prior);
        }
        if (!await db.StudentEnrollments.AsNoTracking().AnyAsync(x => x.InstitutionId == institutionId &&
            x.SectionId == sectionId && x.StudentId == request.StudentId && x.Status == "Active", ct))
            throw ApiFault.NotFound();
        var item = new AdvisingCase { InstitutionId = institutionId, SectionId = sectionId,
            StudentId = request.StudentId, OpenedBy = actor.Id, ClientRequestId = request.ClientRequestId,
            Title = title, InternalSummary = summary };
        db.AdvisingCases.Add(item);
        Event(item, actor.Id, "case.opened", "Case opened");
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateException) { throw ApiFault.Conflict("Case request was already processed; retrieve the existing case."); }
        return Map(item);
    }

    public async Task<PageResult<AdvisingCaseView>> SectionCases(ClaimsPrincipal user,
        Guid sectionId, int page, CancellationToken ct)
    {
        var offset = AdvisingPolicy.Offset(page);
        var (institutionId, _) = await StaffSection(user, sectionId, ct);
        var query = db.AdvisingCases.AsNoTracking().Where(x => x.InstitutionId == institutionId && x.SectionId == sectionId);
        var count = await query.CountAsync(ct);
        var items = await query.OrderByDescending(x => x.CreatedAtUtc).ThenBy(x => x.Id)
            .Skip(offset).Take(AdvisingPolicy.PageSize).ToListAsync(ct);
        return new PageResult<AdvisingCaseView>(page, AdvisingPolicy.PageSize, count, items.Select(Map).ToArray());
    }

    public async Task<AdvisingCaseDetail> Detail(ClaimsPrincipal user, Guid id, CancellationToken ct)
    {
        var item = await StaffCase(user, id, ct);
        var notes = await db.AdvisingNotes.AsNoTracking().Where(x => x.CaseId == id && x.InstitutionId == item.InstitutionId)
            .OrderByDescending(x => x.AtUtc).Take(200).ToListAsync(ct);
        var tasks = await db.AdvisingTasks.AsNoTracking().Where(x => x.CaseId == id && x.InstitutionId == item.InstitutionId)
            .OrderBy(x => x.DueAtUtc).ThenBy(x => x.Id).Take(200).ToListAsync(ct);
        var events = await db.AdvisingEvents.AsNoTracking().Where(x => x.CaseId == id && x.InstitutionId == item.InstitutionId)
            .OrderByDescending(x => x.AtUtc).Take(200).ToListAsync(ct);
        return new AdvisingCaseDetail(Map(item), notes.Select(x => new AdvisingNoteView(x.Id, x.AuthorId, x.Body, x.AtUtc)).ToArray(),
            tasks.Select(Map).ToArray(), events.Select(Map).ToArray());
    }

    public async Task<AdvisingNoteView> Note(ClaimsPrincipal user, Guid id, CreateAdvisingNote request, CancellationToken ct)
    {
        await StaffCase(user, id, ct);
        var body = AdvisingPolicy.Required(request.Body, 4000, "note");
        var actor = (await security.CurrentAsync(user, ct)).User;
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var item = await LockCase(id, ct);
            await security.RequireSectionStaffAsync(user, item.InstitutionId, item.SectionId, ct);
            if (item.Status == "Resolved") throw ApiFault.Conflict("Reopen the case before adding notes.");
            var note = new AdvisingNote { CaseId = id, InstitutionId = item.InstitutionId, AuthorId = actor.Id, Body = body };
            db.AdvisingNotes.Add(note);
            Event(item, actor.Id, "note.added", "Internal note added");
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
            return new AdvisingNoteView(note.Id, note.AuthorId, note.Body, note.AtUtc);
        });
    }

    public async Task<AdvisingTaskView> AddTask(ClaimsPrincipal user, Guid id, CreateAdvisingTask request, CancellationToken ct)
    {
        await StaffCase(user, id, ct);
        var description = AdvisingPolicy.Required(request.Description, 1000, "action");
        if (request.DueAtUtc.HasValue && request.DueAtUtc.Value.Kind != DateTimeKind.Utc)
            throw ApiFault.Bad("DueAtUtc must be a UTC timestamp.");
        var actor = (await security.CurrentAsync(user, ct)).User;
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var item = await LockCase(id, ct);
            await security.RequireSectionStaffAsync(user, item.InstitutionId, item.SectionId, ct);
            if (item.Status == "Resolved") throw ApiFault.Conflict("Reopen the case before adding tasks.");
            var task = new AdvisingTask { InstitutionId = item.InstitutionId, CaseId = item.Id,
                CreatedBy = actor.Id, Description = description, DueAtUtc = request.DueAtUtc };
            db.AdvisingTasks.Add(task);
            Event(item, actor.Id, "task.created", "Follow-up action created");
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
            return Map(task);
        });
    }

    public async Task<AdvisingTaskView> ReportTask(ClaimsPrincipal user, Guid taskId, TaskTransition request, CancellationToken ct)
    {
        var (actor, roles) = await security.CurrentAsync(user, ct);
        if (!roles.Contains(AcademixRoles.Student) || actor.InstitutionId is null) throw ApiFault.Forbidden();
        var reference = await db.AdvisingTasks.AsNoTracking().SingleOrDefaultAsync(x => x.Id == taskId, ct)
            ?? throw ApiFault.NotFound();
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var item = await LockCase(reference.CaseId, ct);
            if (item.StudentId != actor.Id || item.InstitutionId != actor.InstitutionId) throw ApiFault.NotFound();
            if (item.Status == "Resolved") throw ApiFault.Conflict("Case is resolved.");
            var task = (await db.AdvisingTasks.FromSqlInterpolated(
                $"SELECT * FROM advising_tasks WHERE \"Id\" = {taskId} FOR UPDATE").ToListAsync(ct)).SingleOrDefault()
                ?? throw ApiFault.NotFound();
            if (task.CaseId != item.Id || task.InstitutionId != item.InstitutionId) throw ApiFault.NotFound();
            if (task.Version != request.ExpectedVersion || task.Status != "Pending")
                throw ApiFault.Conflict("Task was already changed.");
            task.Status = "ReportedDone"; task.ReportedAtUtc = DateTime.UtcNow; task.Version++;
            Event(item, actor.Id, "task.reported", "Student reported action complete");
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
            return Map(task);
        });
    }

    public async Task<AdvisingTaskView> ReviewTask(ClaimsPrincipal user, Guid taskId, ReviewAdvisingTask request, CancellationToken ct)
    {
        var reference = await db.AdvisingTasks.AsNoTracking().SingleOrDefaultAsync(x => x.Id == taskId, ct)
            ?? throw ApiFault.NotFound();
        await StaffCase(user, reference.CaseId, ct);
        var reason = AdvisingPolicy.Required(request.Reason, 1000, "review reason");
        var actor = (await security.CurrentAsync(user, ct)).User;
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var item = await LockCase(reference.CaseId, ct);
            await security.RequireSectionStaffAsync(user, item.InstitutionId, item.SectionId, ct);
            if (item.Status == "Resolved") throw ApiFault.Conflict("Case is resolved.");
            var task = (await db.AdvisingTasks.FromSqlInterpolated(
                $"SELECT * FROM advising_tasks WHERE \"Id\" = {taskId} FOR UPDATE").ToListAsync(ct)).SingleOrDefault()
                ?? throw ApiFault.NotFound();
            if (task.CaseId != item.Id || task.InstitutionId != item.InstitutionId) throw ApiFault.NotFound();
            if (task.Version != request.ExpectedVersion || task.Status != "ReportedDone")
                throw ApiFault.Conflict("Task is not awaiting review or version is stale.");
            task.Status = request.Accept ? "Completed" : "Pending";
            task.ReviewedBy = actor.Id; task.ReviewedAtUtc = DateTime.UtcNow; task.Version++;
            Event(item, actor.Id, request.Accept ? "task.approved" : "task.returned", reason);
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
            return Map(task);
        });
    }

    public async Task<AdvisingCaseView> ChangeStatus(ClaimsPrincipal user, Guid id,
        ChangeAdvisingStatus request, CancellationToken ct)
    {
        await StaffCase(user, id, ct);
        var reason = AdvisingPolicy.Required(request.Reason, 1000, "reason");
        var actor = (await security.CurrentAsync(user, ct)).User;
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var item = await LockCase(id, ct);
            await security.RequireSectionStaffAsync(user, item.InstitutionId, item.SectionId, ct);
            if (item.Version != request.ExpectedVersion) throw ApiFault.Conflict("Case version is stale.");
            var remaining = await db.AdvisingTasks.CountAsync(x => x.InstitutionId == item.InstitutionId &&
                x.CaseId == item.Id && x.Status != "Completed", ct);
            AdvisingPolicy.CanChangeStatus(item.Status, request.Status, remaining);
            item.Status = request.Status; item.Version++; item.UpdatedAtUtc = DateTime.UtcNow;
            Event(item, actor.Id, "case.status", request.Status + ": " + reason);
            await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
            return Map(item);
        });
    }

    public async Task<PageResult<StudentCaseView>> MyCases(ClaimsPrincipal user, int page, CancellationToken ct)
    {
        var offset = AdvisingPolicy.Offset(page);
        var (actor, roles) = await security.CurrentAsync(user, ct);
        if (!roles.Contains(AcademixRoles.Student) || actor.InstitutionId is not Guid institutionId)
            throw ApiFault.Forbidden();
        var query = db.AdvisingCases.AsNoTracking().Where(x => x.StudentId == actor.Id &&
            x.InstitutionId == institutionId);
        var count = await query.CountAsync(ct);
        var cases = await query.OrderByDescending(x => x.CreatedAtUtc).ThenBy(x => x.Id)
            .Skip(offset).Take(AdvisingPolicy.PageSize).ToListAsync(ct);
        var ids = cases.Select(x => x.Id).ToArray();
        var tasks = await db.AdvisingTasks.AsNoTracking().Where(x => ids.Contains(x.CaseId) &&
            x.InstitutionId == institutionId).OrderBy(x => x.DueAtUtc).ThenBy(x => x.Id).ToListAsync(ct);
        var byCase = tasks.GroupBy(x => x.CaseId).ToDictionary(x => x.Key,
            x => (IReadOnlyList<AdvisingTaskView>)x.Select(Map).ToArray());
        return new PageResult<StudentCaseView>(page, AdvisingPolicy.PageSize, count,
            cases.Select(x => new StudentCaseView(x.Id, x.SectionId, x.Title, x.Status,
                x.CreatedAtUtc, byCase.GetValueOrDefault(x.Id) ?? Array.Empty<AdvisingTaskView>())).ToArray());
    }

    public async Task<AdvisingOverview> InstitutionOverview(ClaimsPrincipal user, Guid institutionId, CancellationToken ct)
    {
        await security.RequireAdminAsync(user, institutionId, ct);
        if (!await db.Institutions.AsNoTracking().AnyAsync(x => x.Id == institutionId, ct)) throw ApiFault.NotFound();
        var cases = db.AdvisingCases.AsNoTracking().Where(x => x.InstitutionId == institutionId);
        var tasks = db.AdvisingTasks.AsNoTracking().Where(x => x.InstitutionId == institutionId);
        return new AdvisingOverview(institutionId,
            await cases.CountAsync(x => x.Status == "Open", ct),
            await cases.CountAsync(x => x.Status == "InProgress", ct),
            await cases.CountAsync(x => x.Status == "Resolved", ct),
            await tasks.CountAsync(x => x.Status == "Pending", ct),
            await tasks.CountAsync(x => x.Status == "ReportedDone", ct), DateTime.UtcNow);
    }
}
