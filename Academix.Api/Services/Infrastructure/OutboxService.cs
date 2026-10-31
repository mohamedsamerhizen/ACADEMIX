using System.Security.Claims;
using System.Text.Json;
using Academix.Api.Data;
using Academix.Api.Domain.Registrar;
using Academix.Api.DTOs.Registrar;
using Academix.Api.Middleware;
using Academix.Api.Services.Communications;
using Academix.Api.Services.Identity;
using Microsoft.EntityFrameworkCore;

namespace Academix.Api.Services.Infrastructure;

public static class DeliveryPolicy
{
    public static TimeSpan Backoff(int attempts)=>TimeSpan.FromSeconds(Math.Min(3600,5*Math.Pow(2,Math.Clamp(attempts-1,0,10))));
    public static bool LeaseOwned(string status,Guid? stored,Guid token)=>status=="Processing"&&stored==token;
}
public sealed class OutboxService(AcademixDbContext db,IdentitySecurity security,CommunicationService communications,RetryingServiceWork retry,IServiceProvider services)
{
    public static InstitutionalOutboxEvent Record(AcademixDbContext db,Guid institution,string kind,Guid aggregate,object payload)
    {
        var row=new InstitutionalOutboxEvent{InstitutionId=institution,Kind=kind,DeduplicationKey=kind+":"+aggregate.ToString("N"),PayloadJson=JsonSerializer.Serialize(payload)};
        db.InstitutionalOutboxEvents.Add(row);return row;
    }

    public Task QueueDue(CancellationToken ct)=>retry.ExecuteAsync<OutboxService>(s=>s.QueueDueCore(ct),ct);
    private async Task QueueDueCore(CancellationToken ct)
    {
        // Stable keys and database uniqueness make polling safe across replicas/restarts.
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO institutional_outbox_events ("Id","InstitutionId","ImportRowId","Kind","PayloadJson","CreatedAtUtc","DeduplicationKey","Status","Attempts")
            SELECT gen_random_uuid(),a."InstitutionId",NULL,'ScheduledAnnouncement',jsonb_build_object('AnnouncementId',a."Id"),{DateTime.UtcNow},
                'ScheduledAnnouncement:'||replace(a."Id"::text,'-',''),'Pending',0
            FROM communication_announcements a JOIN institutions i ON i."Id"=a."InstitutionId"
            WHERE NOT a."IsCancelled" AND a."DispatchedAtUtc" IS NULL AND a."ScheduledForUtc"<={DateTime.UtcNow} AND i."IsActive"
            ON CONFLICT ("InstitutionId","DeduplicationKey") DO NOTHING
            """,ct);
    }
    public async Task ProcessBatch(CancellationToken ct)
    {
        for(var i=0;i<25;i++)
        {
            ct.ThrowIfCancellationRequested();
            var claim=await retry.ExecuteAsync<OutboxService,(Guid Id,Guid Token)?>(s=>s.Claim(ct),ct);
            if(claim is null)return;
            try { await retry.ExecuteAsync<OutboxService>(s=>s.Deliver(claim.Value.Id,claim.Value.Token,ct),ct); }
            catch(OperationCanceledException)when(ct.IsCancellationRequested){throw;}
            catch(Exception ex)
            {
                // Keep the failure durable without storing credentials, bodies or database errors.
                await retry.ExecuteAsync<OutboxService>(s=>s.Fail(claim.Value.Id,claim.Value.Token,ex.GetType().Name,ct),ct);
            }
        }
    }
    private async Task<(Guid,Guid)?> Claim(CancellationToken ct)
    {
        await using var tx=await db.Database.BeginTransactionAsync(ct);var now=DateTime.UtcNow;
        var row=(await db.InstitutionalOutboxEvents.FromSqlInterpolated($"""
            SELECT * FROM institutional_outbox_events WHERE
            (("Status" IN ('Pending','Retry') AND ("NextAttemptAtUtc" IS NULL OR "NextAttemptAtUtc"<={now}))
             OR ("Status"='Processing' AND "LeaseUntilUtc"<={now}))
            ORDER BY "CreatedAtUtc","Id" LIMIT 1 FOR UPDATE SKIP LOCKED
            """).ToListAsync(ct)).SingleOrDefault();
        if(row is null)return null;
        row.Status="Processing";row.LeaseToken=Guid.NewGuid();row.LeaseUntilUtc=now.AddMinutes(2);row.Attempts++;
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return(row.Id,row.LeaseToken.Value);
    }
    private async Task Deliver(Guid id,Guid token,CancellationToken ct)
    {
        var initial=await db.InstitutionalOutboxEvents.AsNoTracking().SingleAsync(x=>x.Id==id,ct);
        if(!DeliveryPolicy.LeaseOwned(initial.Status,initial.LeaseToken,token))return;
        if(initial.Kind=="DocumentArtifactCleanup")
        {
            var document=JsonSerializer.Deserialize<JsonElement>(initial.PayloadJson).GetProperty("DocumentId").GetGuid();
            await Academix.Api.Services.Institutional.DocumentArtifactCleanup.Reconcile(db,
                services.GetRequiredService<Academix.Api.Storage.IPrivateFileStore>(),initial.InstitutionId,document,ct);
        }
        else if(initial.Kind=="LearningArtifactCleanup")
        {
            var payload=JsonSerializer.Deserialize<JsonElement>(initial.PayloadJson);
            await Academix.Api.Services.Learning.LearningArtifactCleanup.Reconcile(db,
                services.GetRequiredService<Academix.Api.Storage.IPrivateFileStore>(),initial.InstitutionId,payload.GetProperty("SectionId").GetGuid(),payload.GetProperty("FileId").GetGuid(),ct);
        }
        else if(!await db.Institutions.AnyAsync(x=>x.Id==initial.InstitutionId&&x.IsActive,ct))throw ApiFault.Conflict("Institution inactive.");
        if(initial.Kind=="ScheduledAnnouncement")
        {
            var announcement=JsonSerializer.Deserialize<JsonElement>(initial.PayloadJson).GetProperty("AnnouncementId").GetGuid();
            var source=await db.ScheduledSectionAnnouncements.AsNoTracking().Where(x=>x.Id==announcement&&x.InstitutionId==initial.InstitutionId)
                .Select(x=>new{x.IsCancelled,x.DispatchedAtUtc}).SingleOrDefaultAsync(ct)??throw ApiFault.NotFound();
            // A restart after the notification transaction committed only needs an
            // acknowledgment. Later author revocation must not replay that effect.
            if(!source.IsCancelled&&source.DispatchedAtUtc is null)await communications.DispatchScheduled(announcement,ct);
        }
        if(initial.Kind=="CurriculumTransitionApproved")
        {
            var transition=JsonSerializer.Deserialize<JsonElement>(initial.PayloadJson).GetProperty("TransitionId").GetGuid();
            if(!await db.Set<CurriculumTransition>().AnyAsync(x=>x.Id==transition&&x.InstitutionId==initial.InstitutionId,ct))throw ApiFault.NotFound();
            await services.GetRequiredService<Academix.Api.Services.Curriculum.CurriculumService>().ApplyApprovedTransition(initial.InstitutionId,transition,ct);
        }
        await using var tx=await db.Database.BeginTransactionAsync(ct);
        var row=(await db.InstitutionalOutboxEvents.FromSqlInterpolated($"SELECT * FROM institutional_outbox_events WHERE \"Id\"={id} FOR UPDATE").ToListAsync(ct)).Single();
        if(!DeliveryPolicy.LeaseOwned(row.Status,row.LeaseToken,token))return;
        if(!await db.Set<InstitutionalEventReceipt>().AnyAsync(x=>x.EventId==id,ct))
            db.Set<InstitutionalEventReceipt>().Add(new(){EventId=id,InstitutionId=row.InstitutionId,Kind=row.Kind,PayloadJson=row.PayloadJson});
        if(row.Kind is "AdvisorAssigned" or "AdvisorAssignmentEnded" or "AdvisingSessionScheduled" or "AdvisingSessionCompleted" or "StudyPlanReviewed" or
            "RequirementExceptionReviewed" or "GraduationSubmitted" or "GraduationDepartmentReviewed" or "GraduationReviewed" or "GraduationFinalized" or
            "TimetableChanged" or "ClassSessionRescheduled" or "PhysicalExamVenueChanged" or "FinanceInvoicePosted" or "FinanceInvoiceReversed" or "FinanceInvoiceAdjusted" or "FinancePaymentPosted" or "FinancePaymentReversed" or "FinanceRefundRecorded" or "FinancePlanReviewed" or "FinanceAwardReviewed" or "StudentServiceCaseChanged")
        {
            var student=JsonSerializer.Deserialize<JsonElement>(row.PayloadJson).GetProperty("StudentId").GetGuid();
            await communications.RecordAcademicUpdate(row.InstitutionId,student,row.Id,ct,financial:row.Kind.StartsWith("Finance",StringComparison.Ordinal));
        }
        row.Status="Delivered";row.DeliveredAtUtc=DateTime.UtcNow;row.LeaseToken=null;row.LeaseUntilUtc=null;row.NextAttemptAtUtc=null;row.LastError=null;
        await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);
    }
    private async Task Fail(Guid id,Guid token,string error,CancellationToken ct)
    {
        var attempts=await db.InstitutionalOutboxEvents.AsNoTracking().Where(x=>x.Id==id).Select(x=>x.Attempts).SingleAsync(ct);
        await db.InstitutionalOutboxEvents.Where(x=>x.Id==id&&x.Status=="Processing"&&x.LeaseToken==token).ExecuteUpdateAsync(s=>s
            .SetProperty(x=>x.Status,"Retry").SetProperty(x=>x.LastError,error).SetProperty(x=>x.NextAttemptAtUtc,DateTime.UtcNow.Add(DeliveryPolicy.Backoff(attempts)))
            .SetProperty(x=>x.LeaseToken,(Guid?)null).SetProperty(x=>x.LeaseUntilUtc,(DateTime?)null),ct);
    }
    public async Task<IReadOnlyList<EventDeliveryView>> Deliveries(ClaimsPrincipal user,Guid inst,int page,CancellationToken ct)
    {
        await security.RequireAdminAsync(user,inst,ct);if(page is <1 or >100000)throw ApiFault.Bad("Invalid page.");
        return await db.InstitutionalOutboxEvents.AsNoTracking().Where(x=>x.InstitutionId==inst).OrderByDescending(x=>x.CreatedAtUtc).ThenBy(x=>x.Id).Skip((page-1)*50).Take(50)
            .Select(x=>new EventDeliveryView(x.Id,x.Kind,x.Status,x.Attempts,x.NextAttemptAtUtc,x.LastError,x.DeliveredAtUtc)).ToArrayAsync(ct);
    }
    public async Task<IReadOnlyList<InstitutionalEventView>> Feed(ClaimsPrincipal user,Guid inst,int page,CancellationToken ct)
    {
        await security.RequireAdminAsync(user,inst,ct);if(page is <1 or >100000)throw ApiFault.Bad("Invalid page.");
        return await db.Set<InstitutionalEventReceipt>().AsNoTracking().Where(x=>x.InstitutionId==inst).OrderBy(x=>x.ReceivedAtUtc).ThenBy(x=>x.EventId).Skip((page-1)*50).Take(50)
            .Select(x=>new InstitutionalEventView(x.EventId,x.Kind,x.PayloadJson,x.ReceivedAtUtc)).ToArrayAsync(ct);
    }
    public Task Control(ClaimsPrincipal user,Guid inst,Guid id,bool cancel,CancellationToken ct)
        =>retry.ExecuteAsync<OutboxService>(s=>s.ControlCore(user,inst,id,cancel,ct),ct);
    private async Task ControlCore(ClaimsPrincipal user,Guid inst,Guid id,bool cancel,CancellationToken ct)
    {
        await using var tx=await db.Database.BeginTransactionAsync(ct);
        var actor=await security.RequireAdminAsync(user,inst,ct);var now=DateTime.UtcNow;
        var count=await db.InstitutionalOutboxEvents.Where(x=>x.Id==id&&x.InstitutionId==inst&&x.Status!="Delivered"&&
            (x.Status!="Processing"||x.LeaseUntilUtc<=now)).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.Status,cancel?"Cancelled":"Pending")
                .SetProperty(x=>x.NextAttemptAtUtc,(DateTime?)null).SetProperty(x=>x.LeaseToken,(Guid?)null).SetProperty(x=>x.LeaseUntilUtc,(DateTime?)null),ct);
        if(count==0)throw ApiFault.Conflict("Event is absent, delivered or currently leased.");
        security.Audit(actor.Id,inst,cancel?"outbox.cancelled":"outbox.retried",id.ToString());await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);
    }
}
