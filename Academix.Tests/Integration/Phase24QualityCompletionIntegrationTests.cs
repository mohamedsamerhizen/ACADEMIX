using System.Security.Claims;
using Academix.Api.Data;
using Academix.Api.Domain.Academic;
using Academix.Api.Domain.Identity;
using Academix.Api.Domain.Quality;
using Academix.Api.DTOs.Quality;
using Academix.Api.Middleware;
using Academix.Api.Services.Quality;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;

namespace Academix.Tests.Integration;

/// <summary>PostgreSQL-backed quality verification. Requires Docker/Testcontainers; not executed in feature-first development.</summary>
public sealed class Phase24QualityCompletionIntegrationTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private static ClaimsPrincipal Principal(Guid actor, Guid institution)
        => new(new ClaimsIdentity([new(ClaimTypes.NameIdentifier, actor.ToString()),
            new("institution_id", institution.ToString())], "test"));

    private async Task<(Guid Institution, Guid Review, ClaimsPrincipal Admin, Guid UserId)> Setup()
    {
        await using var scope = fixture.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AcademixDbContext>();
        var institution = new Institution { Code = Guid.NewGuid().ToString("N")[..12], Name = "Quality integration" };
        db.Institutions.Add(institution);
        var dept = new Department { InstitutionId = institution.Id, Code = "QA" + Guid.NewGuid().ToString("N")[..6], Name = "QA" };
        db.Departments.Add(dept);
        var program = new AcademicProgram { InstitutionId = institution.Id, DepartmentId = dept.Id,
            Code = "PR" + Guid.NewGuid().ToString("N")[..6], Name = "Computer Science" };
        db.AcademicPrograms.Add(program);
        var term = new AcademicTerm { InstitutionId = institution.Id, Code = "T" + Guid.NewGuid().ToString("N")[..6],
            Name = "Test term", StartsOn = new DateOnly(2026, 1, 1), EndsOn = new DateOnly(2026, 6, 1) };
        db.AcademicTerms.Add(term);
        await db.SaveChangesAsync();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var mail = Guid.NewGuid().ToString("N") + "@example.test";
        var admin = new ApplicationUser { InstitutionId = institution.Id, UserName = mail, Email = mail };
        Assert.True((await users.CreateAsync(admin, "Safe!Quality2026")).Succeeded);
        Assert.True((await users.AddToRoleAsync(admin, AcademixRoles.InstitutionAdministrator)).Succeeded);
        var review = new ProgramQualityReview { InstitutionId = institution.Id, ProgramId = program.Id,
            TermId = term.Id, CreatedBy = admin.Id, Status = "Published", PublishedAtUtc = DateTime.UtcNow };
        db.QualityReviews.Add(review);
        await db.SaveChangesAsync();
        return (institution.Id, review.Id, Principal(admin.Id, institution.Id), admin.Id);
    }

    [Fact]
    public async Task SealedEvidencePackRetainsVerifiedSourceAndRejectsLaterAttachments()
    {
        var (inst, reviewId, admin, _) = await Setup();
        await using var scope = fixture.Services.CreateAsyncScope();
        var svc = scope.ServiceProvider.GetRequiredService<QualityEvidenceService>();
        var pack = await svc.CreatePack(admin, inst, reviewId, new("Program accreditation evidence"), default);
        var evidence = await svc.AttachItem(admin, inst, pack.Id,
            new("ReviewProxy", reviewId, "Published course grade evidence"), default);
        Assert.Equal(64, evidence.EvidenceSha256.Length);
        var sealedPack = await svc.SealPack(admin, inst, pack.Id, default);
        Assert.Equal("Sealed", sealedPack.Status);
        Assert.Equal(64, sealedPack.ManifestSha256.Length);
        Assert.Equal(sealedPack.ManifestSha256, QualityEvidenceService.Digest(sealedPack.ManifestJson));
        await Assert.ThrowsAsync<ApiFault>(() => svc.AttachItem(admin, inst, pack.Id,
            new("ReviewDirect", reviewId, "Additional rubric evidence"), default));
    }

    [Fact]
    public async Task ImprovementActionsRecordAppendOnlyStateHistory()
    {
        var (inst, reviewId, admin, _) = await Setup();
        await using var scope = fixture.Services.CreateAsyncScope();
        var svc = scope.ServiceProvider.GetRequiredService<QualityService>();
        var evidence = scope.ServiceProvider.GetRequiredService<QualityEvidenceService>();
        var action = await svc.AddAction(admin, inst, reviewId,
            new(null, "Improve grading consistency", null), default);
        await svc.DecideAction(admin, inst, action.Id, new("InProgress", null), default);
        await svc.DecideAction(admin, inst, action.Id, new("Completed", "Reviewed all criteria and approved the corrective plan"), default);
        var history = await evidence.ActionHistory(admin, inst, action.Id, default);
        Assert.Equal(new[] { "Open", "InProgress", "Completed" }, history.Select(x => x.ToStatus));
    }

    [Fact]
    public async Task RevokedInstitutionAdminCannotReadSealedQualityEvidence()
    {
        var (inst, reviewId, admin, actor) = await Setup();
        await using var scope = fixture.Services.CreateAsyncScope();
        var svc = scope.ServiceProvider.GetRequiredService<QualityEvidenceService>();
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        _ = await svc.CreatePack(admin, inst, reviewId, new("Quality evidence pack"), default);
        var user = (await users.FindByIdAsync(actor.ToString()))!;
        Assert.True((await users.RemoveFromRoleAsync(user, AcademixRoles.InstitutionAdministrator)).Succeeded);
        await Assert.ThrowsAsync<ApiFault>(() => svc.Packs(admin, inst, reviewId, default));
    }
    [Fact]
    public async Task ConcurrentEvidenceAttachmentsAndSealingHaveOneDurableOutcome()
    {
        var (inst,reviewId,admin,_)=await Setup();await using var scope=fixture.Services.CreateAsyncScope();
        var service=scope.ServiceProvider.GetRequiredService<QualityEvidenceService>();
        var pack=await service.CreatePack(admin,inst,reviewId,new("Concurrent quality evidence"),default);
        async Task<QualityPackItem> Attach(){await using var own=fixture.Services.CreateAsyncScope();return await own.ServiceProvider.GetRequiredService<QualityEvidenceService>().AttachItem(admin,inst,pack.Id,new("ReviewProxy",reviewId,"Published review evidence"),default);}
        var items=await Task.WhenAll(Attach(),Attach());Assert.Equal(items[0].Id,items[1].Id);
        async Task<QualityAccreditationPack> Seal(){await using var own=fixture.Services.CreateAsyncScope();return await own.ServiceProvider.GetRequiredService<QualityEvidenceService>().SealPack(admin,inst,pack.Id,default);}
        var sealedPacks=await Task.WhenAll(Seal(),Seal());Assert.Equal(sealedPacks[0].ManifestSha256,sealedPacks[1].ManifestSha256);
        Assert.Single(await service.Items(admin,inst,pack.Id,default));
    }
    [Fact]
    public async Task SerializationFailureRetriesTheWholeAttachmentWithFreshTracking()
    {
        var (inst,reviewId,admin,_)=await Setup();await using var scope=fixture.Services.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<AcademixDbContext>();
        var service=scope.ServiceProvider.GetRequiredService<QualityEvidenceService>();var pack=await service.CreatePack(admin,inst,reviewId,new("Serialization retry evidence"),default);
        var suffix=Guid.NewGuid().ToString("N");var sequence="remediation_seq_"+suffix;var function="remediation_fault_"+suffix;var trigger="remediation_trigger_"+suffix;
        await using var connection=new Npgsql.NpgsqlConnection(db.Database.GetConnectionString());await connection.OpenAsync();
        // Names and UUID below are generated inside this test, never external input.
        var setup=$"CREATE SEQUENCE {sequence}; CREATE FUNCTION {function}() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN IF NEW.\"PackId\"='{pack.Id}'::uuid AND nextval('{sequence}')=1 THEN RAISE EXCEPTION 'injected serialization retry' USING ERRCODE='40001'; END IF; RETURN NEW; END $$; CREATE TRIGGER {trigger} BEFORE INSERT ON quality_pack_items FOR EACH ROW EXECUTE FUNCTION {function}();";
        await using(var command=new Npgsql.NpgsqlCommand(setup,connection))await command.ExecuteNonQueryAsync();
        try
        {
            var item=await service.AttachItem(admin,inst,pack.Id,new("ReviewProxy",reviewId,"Authoritative retry evidence"),default);
            Assert.Single(await service.Items(admin,inst,pack.Id,default));Assert.NotEqual(Guid.Empty,item.Id);
            await using var count=new Npgsql.NpgsqlCommand($"SELECT last_value FROM {sequence}",connection);Assert.True((long)(await count.ExecuteScalarAsync())!>=2);
            Assert.Equal("Sealed",(await service.SealPack(admin,inst,pack.Id,default)).Status);
        }
        finally{await using var cleanup=new Npgsql.NpgsqlCommand($"DROP TRIGGER {trigger} ON quality_pack_items; DROP FUNCTION {function}(); DROP SEQUENCE {sequence};",connection);await cleanup.ExecuteNonQueryAsync();}
    }

}
