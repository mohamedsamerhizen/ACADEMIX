using Academix.Api.Data;
using Academix.Api.Domain.Examinations;
using Academix.Api.Services.Infrastructure;
using Academix.Api.Services.Institutional;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Academix.Tests.Integration;

// Trigger identifiers and literals below come exclusively from generated GUIDs.
#pragma warning disable EF1002
public sealed partial class Phase19CompletionWorkflowTests
{
    private async Task ProcessOutboxUntil(Func<Task<bool>> completed)
    {
        // The shared fixture may contain older events from other scenarios. Do not
        // assume this scenario's event is in the dispatcher's first 25-message batch.
        await using var scope=fixture.Services.CreateAsyncScope();
        var outbox=scope.ServiceProvider.GetRequiredService<OutboxService>();
        for(var batch=0;batch<40&&!await completed();batch++)await outbox.ProcessBatch(default);
        Assert.True(await completed(),"Scenario event did not progress within 40 bounded batches.");
    }

    [Fact]
    public async Task PdfTransientDatabaseFailureRetriesWithFreshTrackedStateAndOneIssuance()
    {
        var s=await Setup();
        await using var scope=fixture.Services.CreateAsyncScope();
        var db=scope.ServiceProvider.GetRequiredService<AcademixDbContext>();
        var docs=scope.ServiceProvider.GetRequiredService<InstitutionalDocumentService>();
        var request=await docs.SubmitApplication(s.StudentUser,new(Guid.NewGuid(),"Enrollment","Transient recovery evidence"),default);
        await docs.Review(s.Admin,request.Id,new(1,true,"Enrollment and identity validated"),default);
        var name="transient_"+s.Inst.ToString("N");
        // PostgreSQL sequences survive rollback, so exactly the first transaction fails.
        await db.Database.ExecuteSqlRawAsync($"""
            CREATE SEQUENCE {name};
            CREATE FUNCTION {name}() RETURNS trigger LANGUAGE plpgsql AS $body$
            BEGIN IF NEW."Id"='{request.Id}'::uuid AND nextval('{name}')=1
              THEN RAISE EXCEPTION 'Injected serialization failure' USING ERRCODE='40001'; END IF; RETURN NEW; END $body$;
            CREATE TRIGGER {name} BEFORE INSERT ON issued_academic_documents FOR EACH ROW EXECUTE FUNCTION {name}();
            """);
        try
        {
            var issued=await docs.Issue(s.Admin,request.Id,new(2),default);
            await ProcessOutboxUntil(()=>db.InstitutionalOutboxEvents
                .Where(x=>x.InstitutionId==s.Inst&&x.Kind=="DocumentArtifactCleanup").AllAsync(x=>x.Status=="Delivered"));
            Assert.True((await docs.Verify(new(issued.ReferenceNumber,issued.VerificationProof),default)).Valid);
            Assert.Equal(1,await db.IssuedAcademicDocuments.CountAsync(x=>x.RequestId==request.Id));
            Assert.Equal(2,await db.InstitutionalOutboxEvents.CountAsync(x=>x.InstitutionId==s.Inst&&x.Kind=="DocumentArtifactCleanup"));
            Assert.Equal(1,await db.InstitutionalDocumentEvents.CountAsync(x=>x.RequestId==request.Id&&x.Action=="issued"));
        }
        finally
        {
            await db.Database.ExecuteSqlRawAsync($"DROP TRIGGER {name} ON issued_academic_documents; DROP FUNCTION {name}(); DROP SEQUENCE {name};");
        }
    }

    [Theory]
    [InlineData(3, 1)]
    [InlineData(103, 100)]
    public async Task WorkerIsolatesPoisonAttemptsDeliversOutboxAndRetriesWithoutDuplicateAudits(int count, int failures)
    {
        var s = await Setup();
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AcademixDbContext>();
        var ids = Enumerable.Range(0, count).Select(_ => Guid.NewGuid()).Order().ToArray();
        foreach (var id in ids)
        {
            var exam = new ExamDefinition { InstitutionId=s.Inst, CourseId=s.Course, SectionId=s.Section,
                CreatedBy=s.AdminId, Title="Timeout isolation", DurationMinutes=10, MaxAttempts=1,
                OpensAtUtc=DateTime.UtcNow.AddHours(-2), ClosesAtUtc=DateTime.UtcNow.AddHours(1) };
            db.Exams.Add(exam);
            db.ExamAttempts.Add(new ExamAttempt { Id=id, ExamId=exam.Id, InstitutionId=s.Inst,
                CourseId=s.Course, SectionId=s.Section, StudentId=s.Student, AttemptNumber=1,
                StartedAtUtc=DateTime.UtcNow.AddHours(-1), DeadlineUtc=DateTime.UtcNow.AddMinutes(-1),
                LastActivityAtUtc=DateTime.UtcNow.AddMinutes(-2) });
        }
        var message=OutboxService.Record(db,s.Inst,"ImportRowApplied",Guid.NewGuid(),new { StudentId=s.Student });
        await db.SaveChangesAsync();
        var name="failure_"+s.Inst.ToString("N");
        var targets=string.Join(",",ids.Take(failures).Select(x=>"'"+x+"'"));
        await db.Database.ExecuteSqlRawAsync($"""
            CREATE FUNCTION {name}() RETURNS trigger LANGUAGE plpgsql AS $body$
            BEGIN IF NEW."Action"='exam.attempt.timeout' AND NEW."Target" IN ({targets})
              THEN RAISE EXCEPTION 'Injected timeout audit failure'; END IF; RETURN NEW; END $body$;
            CREATE TRIGGER {name} BEFORE INSERT ON security_audit_events FOR EACH ROW EXECUTE FUNCTION {name}();
            """);
        var worker=new AcademicBackgroundWorker(fixture.Services.GetRequiredService<IServiceScopeFactory>(),
            new ConfigurationBuilder().Build(),NullLogger<AcademicBackgroundWorker>.Instance);
        try
        {
            await worker.Tick(default);
            for(var batch=0;batch<40&&!await db.InstitutionalOutboxEvents.AnyAsync(x=>x.Id==message.Id&&x.Status=="Delivered");batch++)
                await worker.Tick(default);
            Assert.Equal("Delivered",await db.InstitutionalOutboxEvents.Where(x=>x.Id==message.Id).Select(x=>x.Status).SingleAsync());
            await worker.Tick(default); // Move past a full poison page when count > 100.
            Assert.Equal(count-failures,await db.ExamAttempts.CountAsync(x=>ids.Contains(x.Id)&&x.Status=="Expired"));
            Assert.Equal(failures,await db.ExamAttempts.CountAsync(x=>ids.Contains(x.Id)&&x.Status=="InProgress"&&x.Version==1));
        }
        finally
        {
            await db.Database.ExecuteSqlRawAsync($"DROP TRIGGER {name} ON security_audit_events; DROP FUNCTION {name}();");
        }
        await worker.Tick(default);
        await worker.Tick(default);
        await worker.Tick(default);
        Assert.Equal(count,await db.ExamAttempts.CountAsync(x=>ids.Contains(x.Id)&&x.Status=="Expired"&&x.Version==2));
        var targetsAll=ids.Select(x=>x.ToString()).ToArray();
        Assert.Equal(count,await db.SecurityAuditEvents.CountAsync(x=>x.Action=="exam.attempt.timeout"&&targetsAll.Contains(x.Target)));
    }

    [Fact]
    public async Task PdfDatabaseRollbackLeavesDurableCleanupThatRetriesAndPreservesCommittedReissuance()
    {
        var s=await Setup();
        await using var scope=fixture.Services.CreateAsyncScope();
        var db=scope.ServiceProvider.GetRequiredService<AcademixDbContext>();
        var docs=scope.ServiceProvider.GetRequiredService<InstitutionalDocumentService>();
        var request=await docs.SubmitApplication(s.StudentUser,new(Guid.NewGuid(),"Enrollment","Cleanup regression evidence"),default);
        await docs.Review(s.Admin,request.Id,new(1,true,"Enrollment and identity validated"),default);
        var name="failure_"+s.Inst.ToString("N");
        await db.Database.ExecuteSqlRawAsync($"""
            CREATE FUNCTION {name}() RETURNS trigger LANGUAGE plpgsql AS $body$
            BEGIN IF NEW."Id"='{request.Id}'::uuid THEN RAISE EXCEPTION 'Injected issuance database failure'; END IF; RETURN NEW; END $body$;
            CREATE TRIGGER {name} BEFORE INSERT ON issued_academic_documents FOR EACH ROW EXECUTE FUNCTION {name}();
            """);
        var store=scope.ServiceProvider.GetRequiredService<TestArtifactStore>();
        var key=$"official/{s.Inst:N}/{request.Id:N}.pdf";
        try { await Assert.ThrowsAsync<DbUpdateException>(()=>docs.Issue(s.Admin,request.Id,new(2),default)); }
        finally { await db.Database.ExecuteSqlRawAsync($"DROP TRIGGER {name} ON issued_academic_documents; DROP FUNCTION {name}();"); }
        Assert.True(store.Contains(key));
        Assert.False(await db.IssuedAcademicDocuments.AnyAsync(x=>x.Id==request.Id));
        store.FailRemoveKey=key;
        var outbox=scope.ServiceProvider.GetRequiredService<OutboxService>();
        await ProcessOutboxUntil(()=>db.InstitutionalOutboxEvents.AnyAsync(x=>x.InstitutionId==s.Inst&&x.Kind=="DocumentArtifactCleanup"&&x.Status=="Retry"));
        var cleanup=await db.InstitutionalOutboxEvents.AsNoTracking().SingleAsync(x=>x.InstitutionId==s.Inst&&x.Kind=="DocumentArtifactCleanup");
        Assert.Equal("Retry",cleanup.Status); Assert.True(store.Contains(key));
        store.FailRemoveKey=null;
        await db.InstitutionalOutboxEvents.Where(x=>x.Id==cleanup.Id).ExecuteUpdateAsync(set=>set.SetProperty(x=>x.NextAttemptAtUtc,DateTime.UtcNow.AddSeconds(-1)));
        await ProcessOutboxUntil(()=>db.InstitutionalOutboxEvents.AnyAsync(x=>x.Id==cleanup.Id&&x.Status=="Delivered"));
        Assert.False(store.Contains(key));
        var issued=await docs.Issue(s.Admin,request.Id,new(2),default);
        Assert.Equal(issued.Id,(await docs.Issue(s.Admin,request.Id,new(2),default)).Id);
        await Task.WhenAll(outbox.ProcessBatch(default),docs.Issue(s.Admin,request.Id,new(2),default));
        Assert.True(store.Contains(key));
        Assert.True((await docs.Verify(new(issued.ReferenceNumber,issued.VerificationProof),default)).Valid);
        Assert.Equal(1,await db.IssuedAcademicDocuments.CountAsync(x=>x.RequestId==request.Id));
    }
}
