using System.Security.Claims;
using Academix.Api.Data;
using Academix.Api.Services.Infrastructure;
using Academix.Api.Domain.AcademicOperations;
using Academix.Api.DTOs.AcademicOperations;
using Academix.Api.Middleware;
using Academix.Api.Services.Identity;
using Microsoft.EntityFrameworkCore;

namespace Academix.Api.Services.AcademicOperations;

public sealed partial class RegistrarOperationsService(AcademixDbContext db, IdentitySecurity security, RetryingServiceWork retry)
{
    public async Task<CalendarEventView> CreateEvent(ClaimsPrincipal principal, Guid institutionId,
        CalendarEventCreate input, CancellationToken ct)
    {
        AcademicOperationsPolicy.Calendar(input);
        var actor = await security.RequireAdminAsync(principal, institutionId, ct);
        if (!await db.Institutions.AnyAsync(x => x.Id == institutionId && x.IsActive, ct))
            throw ApiFault.NotFound();
        if (input.TermId is Guid termId && !await db.AcademicTerms.AnyAsync(x =>
            x.Id == termId && x.InstitutionId == institutionId, ct)) throw ApiFault.NotFound();
        if (input.SectionId is Guid sectionId && !await db.CourseSections.AnyAsync(x =>
            x.Id == sectionId && x.InstitutionId == institutionId, ct)) throw ApiFault.NotFound();
        if (input.TermId is Guid term && input.SectionId is Guid section &&
            !await db.CourseSections.AnyAsync(x => x.Id == section &&
                x.InstitutionId == institutionId && x.TermId == term, ct))
            throw ApiFault.Bad("Calendar term does not match the section term.");
        var entity = new AcademicCalendarEvent { InstitutionId = institutionId, TermId = input.TermId,
            SectionId = input.SectionId, Title = input.Title.Trim(), Description = input.Description,
            StartsAtUtc = input.StartsAtUtc, EndsAtUtc = input.EndsAtUtc, Audience = input.Audience,
            IsPublished = input.IsPublished, CreatedBy = actor.Id };
        db.AcademicCalendarEvents.Add(entity);
        await db.SaveChangesAsync(ct);
        return View(entity);
    }

    public async Task<IReadOnlyList<CalendarEventView>> Calendar(ClaimsPrincipal principal, Guid institutionId,
        DateTime fromUtc, DateTime toUtc, CancellationToken ct)
    {
        AcademicOperationsPolicy.TimeRange(fromUtc, toUtc);
        await security.RequireAdminAsync(principal, institutionId, ct);
        return await db.AcademicCalendarEvents.AsNoTracking().Where(x => x.InstitutionId == institutionId &&
            x.StartsAtUtc < toUtc && x.EndsAtUtc > fromUtc)
            .OrderBy(x => x.StartsAtUtc).ThenBy(x => x.Id)
            .Select(x => new CalendarEventView(x.Id, x.TermId, x.SectionId, x.Title,
                x.Description, x.StartsAtUtc, x.EndsAtUtc, x.Audience, x.IsPublished))
            .Take(500).ToListAsync(ct);
    }

    public async Task<IReadOnlyList<AcademicRequestView>> Requests(ClaimsPrincipal principal,
        Guid institutionId, string? status, int page, CancellationToken ct)
    {
        await security.RequireAdminAsync(principal, institutionId, ct);
        if (status is not (null or "Pending" or "Approved" or "Rejected" or "Cancelled" or "UnderReview" or "Completed"))
            throw ApiFault.Bad("Invalid request status.");
        if (page is < 1 or > 10000) throw ApiFault.Bad("Page must be 1–10000.");
        var query = db.StudentAcademicRequests.AsNoTracking().Where(x => x.InstitutionId == institutionId);
        if (status is not null) query = query.Where(x => x.Status == status);
        return await query.OrderByDescending(x => x.SubmittedAtUtc).ThenBy(x => x.Id)
            .Skip((page - 1) * 20).Take(20)
            .Select(x => new AcademicRequestView(x.Id, x.SectionId, x.Kind, x.Subject, x.Details,
                x.Status, x.SubmittedAtUtc, x.DecidedAtUtc, x.DecisionReason, x.Version, x.FulfilledAtUtc, x.FulfillmentResult, x.ExecutionDataJson)).ToListAsync(ct);
    }

    public Task<AcademicRequestView> Decide(ClaimsPrincipal principal, Guid institutionId,
        Guid requestId, AcademicRequestDecision input, CancellationToken ct)
        => retry.ExecuteAsync<RegistrarOperationsService, AcademicRequestView>(service => service.DecideCore(principal, institutionId, requestId, input, ct), ct);

    private async Task<AcademicRequestView> DecideCore(ClaimsPrincipal principal, Guid institutionId,
        Guid requestId, AcademicRequestDecision input, CancellationToken ct)
    {
        AcademicOperationsPolicy.Decision(input);
        var actor = await security.RequireAdminAsync(principal, institutionId, ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var req = await db.StudentAcademicRequests.FromSqlInterpolated(
            $"SELECT * FROM student_academic_requests WHERE \"Id\" = {requestId} AND \"InstitutionId\" = {institutionId} FOR UPDATE")
            .SingleOrDefaultAsync(ct) ?? throw ApiFault.NotFound();
        actor = await security.RequireAdminAsync(principal, institutionId, ct);
        if (req.Status is not ("Pending" or "UnderReview") || req.Version != input.ExpectedVersion)
            throw ApiFault.Conflict("Request is already decided or its version changed.");
        if(req.Status=="Pending"&&req.Kind!="General"&&req.ExecutionDataJson is not null)throw ApiFault.Conflict("Validate typed references in UnderReview before deciding this request.");
        if(actor.Id==req.StudentId)throw ApiFault.Forbidden();
        var previousStatus=req.Status;
        req.Status = input.Decision;
        req.Version++;
        req.DecisionReason = input.Reason.Trim();
        req.DecidedBy = actor.Id;
        req.DecidedAtUtc = DateTime.UtcNow;
        db.AcademicRequestHistories.Add(new AcademicRequestHistory { InstitutionId = institutionId,
            RequestId = req.Id, ActorId = actor.Id, FromStatus = previousStatus, ToStatus = req.Status,
            Reason = req.DecisionReason });
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return StudentAcademicOperationsService.View(req);
    }

    public async Task<IReadOnlyList<AcademicRequestAudit>> RequestHistory(ClaimsPrincipal principal,
        Guid institutionId, Guid requestId, CancellationToken ct)
    {
        await security.RequireAdminAsync(principal, institutionId, ct);
        if (!await db.StudentAcademicRequests.AnyAsync(x => x.InstitutionId == institutionId && x.Id == requestId, ct))
            throw ApiFault.NotFound();
        return await db.AcademicRequestHistories.AsNoTracking().Where(x => x.InstitutionId == institutionId &&
            x.RequestId == requestId).OrderBy(x => x.AtUtc).ThenBy(x => x.Id)
            .Select(x => new AcademicRequestAudit(x.ActorId, x.FromStatus, x.ToStatus,
                x.Reason, x.AtUtc)).ToListAsync(ct);
    }

    private static CalendarEventView View(AcademicCalendarEvent x) => new(x.Id, x.TermId,
        x.SectionId, x.Title, x.Description, x.StartsAtUtc, x.EndsAtUtc, x.Audience, x.IsPublished);
}
