using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Academix.Api.Data;
using Academix.Api.Domain.Identity;
using Academix.Api.Domain.Registrar;
using Academix.Api.DTOs.Registrar;
using Academix.Api.DTOs.Records;
using Academix.Api.DTOs.Curriculum;
using Academix.Api.Middleware;
using Academix.Api.Services.Identity;
using Academix.Api.Services.Records;
using Academix.Api.Services.Curriculum;
using Academix.Api.Services.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Academix.Api.Services.Registrar;

public sealed class InstitutionalImportService(AcademixDbContext db, IdentitySecurity security,
    RecordsService records, CurriculumService curriculum, RetryingServiceWork retry, ApplicantService applicants)
{
    public async Task<ImportReport> UploadFile(ClaimsPrincipal user, Guid inst, IFormFile file, CancellationToken ct)
    {
        await security.RequireAdminAsync(user, inst, ct);
        if (file is null || file.Length is <= 0 or > 2 * 1024 * 1024) throw ApiFault.Bad("JSON import file must be 1 byte to 2 MiB.");
        await using var stream = file.OpenReadStream();
        ImportUpload? input;
        try { input = await JsonSerializer.DeserializeAsync<ImportUpload>(stream, new JsonSerializerOptions(JsonSerializerDefaults.Web) { MaxDepth = 8 }, ct); }
        catch (JsonException) { throw ApiFault.Bad("Invalid import JSON."); }
        if (input is null) throw ApiFault.Bad("Import file required.");
        return await Upload(user, inst, input, ct);
    }
    public Task<ImportReport> Upload(ClaimsPrincipal user, Guid inst, ImportUpload input, CancellationToken ct)
        => retry.ExecuteAsync<InstitutionalImportService, ImportReport>(s => s.UploadCore(user, inst, input, ct), ct);
    private async Task<ImportReport> UploadCore(ClaimsPrincipal user, Guid inst, ImportUpload input, CancellationToken ct)
    {
        var actor = await security.RequireAdminAsync(user, inst, ct);
        var rows = ImportValidation.Parse(input);
        var digest = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(rows))));
        var source = input.SourceSystem.Trim();
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await Academix.Api.Services.Academic.RegistrationCoordination.Lock(db, inst, ct);
        var existing = await db.ImportJobs.AsNoTracking().SingleOrDefaultAsync(x => x.InstitutionId == inst && x.ClientRequestId == input.ClientRequestId, ct);
        if (existing is not null)
        {
            var sameRows=existing.Digest==digest;
            if(!sameRows)
            {
                var stored=await db.ImportRows.AsNoTracking().Where(x=>x.JobId==existing.Id&&x.InstitutionId==inst).OrderBy(x=>x.Number).Select(x=>x.PayloadJson).ToListAsync(ct);
                sameRows=stored.Select(x=>JsonSerializer.Deserialize<ImportStudentRecord>(x)!).SequenceEqual(rows);
            }
            if (!sameRows || existing.SourceSystem != source || existing.CreatedBy != actor.Id) throw ApiFault.Conflict("Import idempotency key reused with different input.");
            return await Report(user, inst, existing.Id, ct);
        }
        var job = new ImportJob { InstitutionId = inst, SourceSystem = source, Digest = digest, CreatedBy = actor.Id, ClientRequestId = input.ClientRequestId };
        db.ImportJobs.Add(job);
        db.ImportRows.AddRange(rows.Select((x, index) => new ImportRow { InstitutionId = inst, JobId = job.Id, Number = index + 1, PayloadJson = JsonSerializer.Serialize(x) }));
        security.Audit(actor.Id, inst, "import.uploaded", job.Id.ToString());
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return await Report(user, inst, job.Id, ct);
    }
    private async Task<(Guid? StudentId, string? Issue)> Validate(Guid inst, string source, ImportStudentRecord row, CancellationToken ct)
    {
        var student = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.InstitutionId == inst && x.NormalizedEmail == row.Email && x.IsActive, ct);
        if(student is null)
        {
            if(await db.Users.AnyAsync(x=>x.NormalizedEmail==row.Email,ct))return(null,"IdentityInactiveOrWrongInstitution");
            if(!row.InviteIfMissing||row.FullName is null)return(null,"ActiveStudentAccountRequired");
            if(row.PublishedSectionId.HasValue)return(null,"HistoryRequiresVerifiedStudentIdentity");
            if(!await ValidAcademicMapping(inst,row,false,ct))return(null,"InvalidInstitutionAcademicMapping");
            var candidate=await db.Set<Applicant>().AsNoTracking().SingleOrDefaultAsync(x=>x.InstitutionId==inst&&x.NormalizedEmail==row.Email,ct);
            if(candidate is not null&&(candidate.ProgramId!=row.ProgramId||candidate.TermId!=row.AdmissionTermId||candidate.FullName!=row.FullName||candidate.Status is "Rejected" or "Converted"))return(null,"ConflictingApplicant");
            if(await db.ExternalIdentityMaps.AnyAsync(x=>x.InstitutionId==inst&&x.SourceSystem==source&&x.ExternalId==row.ExternalId,ct))return(null,"ExternalIdentityAlreadyMapped");
            return(null,null);
        }
        if (!await db.UserRoles.Join(db.Roles, u => u.RoleId, r => r.Id, (u, r) => new { u.UserId, r.Name })
            .AnyAsync(x => x.UserId == student.Id && x.Name == AcademixRoles.Student, ct)) return (null, "ActiveStudentAccountRequired");
        if(!await ValidAcademicMapping(inst,row,true,ct))return(student.Id,"InvalidInstitutionAcademicMapping");
        var membership = await db.StudentProgramMemberships.AsNoTracking().SingleOrDefaultAsync(x => x.InstitutionId == inst && x.StudentId == student.Id, ct);
        if (membership is not null && (membership.ProgramId != row.ProgramId || membership.AdmissionTermId != row.AdmissionTermId || membership.Status != "Active")) return (student.Id, "ConflictingProgramMembership");
        var assignment = await db.StudentCurriculumAssignments.AsNoTracking().SingleOrDefaultAsync(x => x.InstitutionId == inst && x.StudentId == student.Id, ct);
        if (assignment is not null && assignment.PlanId != row.PlanId) return (student.Id, "ConflictingCurriculumAssignment");
        var mapping = await db.ExternalIdentityMaps.AsNoTracking().SingleOrDefaultAsync(x => x.InstitutionId == inst && x.SourceSystem == source && x.ExternalId == row.ExternalId, ct);
        if (mapping is not null && mapping.StudentId != student.Id) return (student.Id, "ExternalIdentityAlreadyMapped");
        if(row.PublishedSectionId is Guid history && (!await db.GradebookResults.AnyAsync(x=>x.SectionId==history&&x.InstitutionId==inst&&x.StudentId==student.Id,ct)||
            !await db.SectionGradePolicies.AnyAsync(x=>x.SectionId==history&&x.InstitutionId==inst&&x.PublishedAtUtc!=null,ct)))return(student.Id,"PublishedStudentHistoryRequired");
        return (student.Id, null);
    }
    private async Task<bool> ValidAcademicMapping(Guid inst,ImportStudentRecord row,bool historical,CancellationToken ct)
        =>await db.AcademicPrograms.AnyAsync(x=>x.Id==row.ProgramId&&x.InstitutionId==inst&&x.IsActive,ct)&&
            await db.AcademicTerms.AnyAsync(x=>x.Id==row.AdmissionTermId&&x.InstitutionId==inst&&(historical||x.Status!="Closed"),ct)&&
            await db.CurriculumPlans.AnyAsync(x=>x.Id==row.PlanId&&x.InstitutionId==inst&&x.ProgramId==row.ProgramId&&x.Status=="Published",ct);
    private async Task<ImportJob> LockJob(Guid inst, Guid id, CancellationToken ct)
        => (await db.ImportJobs.FromSqlInterpolated($"SELECT * FROM institutional_import_jobs WHERE \"Id\" = {id} AND \"InstitutionId\" = {inst} FOR UPDATE")
            .ToListAsync(ct)).SingleOrDefault() ?? throw ApiFault.NotFound();
    public Task<ImportReport> DryRun(ClaimsPrincipal user, Guid inst, Guid id, CancellationToken ct)
        => retry.ExecuteAsync<InstitutionalImportService, ImportReport>(s => s.DryRunCore(user, inst, id, ct), ct);
    private async Task<ImportReport> DryRunCore(ClaimsPrincipal user, Guid inst, Guid id, CancellationToken ct)
    {
        await security.RequireAdminAsync(user, inst, ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var job = await LockJob(inst, id, ct);
        if (job.Status != "Draft") throw ApiFault.Conflict("Only draft imports can be validated.");
        foreach (var row in await db.ImportRows.Where(x => x.JobId == id && x.InstitutionId == inst).ToListAsync(ct))
        {
            var check = await Validate(inst, job.SourceSystem, JsonSerializer.Deserialize<ImportStudentRecord>(row.PayloadJson)!, ct);
            row.StudentId = check.StudentId; row.Issue = check.Issue; row.Status = check.Issue is null ? "Valid" : "Invalid";
        }
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        return await Report(user, inst, id, ct);
    }
    public Task<ImportReport> Approve(ClaimsPrincipal user, Guid inst, Guid id, CancellationToken ct)
        => retry.ExecuteAsync<InstitutionalImportService, ImportReport>(s => s.ApproveCore(user, inst, id, ct), ct);
    private async Task<ImportReport> ApproveCore(ClaimsPrincipal user, Guid inst, Guid id, CancellationToken ct)
    {
        var actor = await security.RequireAdminAsync(user, inst, ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var job = await LockJob(inst, id, ct);
        if (job.CreatedBy == actor.Id) throw ApiFault.Forbidden();
        if (job.Status == "Approved") return await Report(user, inst, id, ct);
        if (job.Status != "Draft" || await db.ImportRows.AnyAsync(x => x.JobId == id && x.Status != "Valid", ct)) throw ApiFault.Conflict("A successful dry run is required before approval.");
        job.Status = "Approved"; job.ApprovedBy = actor.Id; job.ApprovedAtUtc = DateTime.UtcNow;
        security.Audit(actor.Id, inst, "import.approved", id.ToString());
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct); return await Report(user, inst, id, ct);
    }
    public async Task<ImportReport> Apply(ClaimsPrincipal user, Guid inst, Guid id, CancellationToken ct)
    {
        await security.RequireAdminAsync(user, inst, ct);
        if (!await db.ImportJobs.AsNoTracking().AnyAsync(x => x.Id == id && x.InstitutionId == inst && x.Status == "Approved", ct))
            throw ApiFault.Conflict("Approved import required.");
        var rowIds = await db.ImportRows.AsNoTracking().Where(x => x.InstitutionId == inst && x.JobId == id && x.Status != "Applied")
            .OrderBy(x => x.Number).Select(x => x.Id).ToListAsync(ct);
        foreach (var rowId in rowIds)
        {
            try { await retry.ExecuteAsync<InstitutionalImportService>(s => s.ApplyRow(user, inst, id, rowId, ct), ct); }
            catch(ApiFault fault)when(fault.StatusCode is 401 or 403){throw;}
            catch (Exception ex) when (ex is ApiFault or DbUpdateException or Npgsql.NpgsqlException or TimeoutException or JsonException)
            {
                // Safe public issue; never persist database exceptions or sensitive server output.
                await db.ImportRows.Where(x => x.Id == rowId && x.InstitutionId == inst && x.Status != "Applied")
                    .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, "Failed").SetProperty(x => x.Issue, "AcademicOperationRejected; revalidate institution mappings and retry."), ct);
            }
        }
        return await Report(user, inst, id, ct);
    }
    private async Task ApplyRow(ClaimsPrincipal user, Guid inst, Guid jobId, Guid rowId, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await Academix.Api.Services.Academic.RegistrationCoordination.Lock(db,inst,ct);
        var job = await LockJob(inst, jobId, ct); // Serializes apply/resume of this job across instances.
        var actor = await security.RequireAdminAsync(user, inst, ct);
        if (job.Status != "Approved" || job.ApprovedBy is null) throw ApiFault.Conflict("Approved import required.");
        var row = await db.ImportRows.SingleOrDefaultAsync(x => x.Id == rowId && x.JobId == jobId && x.InstitutionId == inst, ct) ?? throw ApiFault.NotFound();
        if (row.Status == "Applied") return;
        var payload = JsonSerializer.Deserialize<ImportStudentRecord>(row.PayloadJson)!;
        var targetUser=await db.Users.AsNoTracking().Where(x=>x.NormalizedEmail==payload.Email&&x.InstitutionId==inst).Select(x=>x.Id).SingleOrDefaultAsync(ct);
        await db.Users.FromSqlInterpolated($"SELECT * FROM identity_users WHERE \"Id\"={targetUser} FOR UPDATE").ToListAsync(ct);
        var check = await Validate(inst, job.SourceSystem, payload, ct);
        if (check.Issue is not null) throw ApiFault.Conflict("Import row no longer valid: "+check.Issue);
        if(check.StudentId is not Guid studentId)
        {
            if(!payload.InviteIfMissing)throw ApiFault.Conflict("A verified student identity is required.");
            var applicant=await applicants.PrepareImportedApplicant(inst,row.Id,payload,job.CreatedBy,job.ApprovedBy.Value,ct);
            row.Status="AwaitingActivation";row.Issue="ApplicantActivationRequired:"+applicant.Id;
            var dedup="ImportApplicantInvitationRequired:"+row.Id.ToString("N");
            if(!await db.InstitutionalOutboxEvents.AnyAsync(x=>x.InstitutionId==inst&&x.DeduplicationKey==dedup,ct))
                OutboxService.Record(db,inst,"ImportApplicantInvitationRequired",row.Id,new{JobId=job.Id,RowId=row.Id,ApplicantId=applicant.Id});
            await db.SaveChangesAsync(ct);await tx.CommitAsync(ct);return;
        }
        // All academic effects, the checkpoint and the outbox share this row transaction.
        if (!await db.StudentProgramMemberships.AsNoTracking().AnyAsync(x => x.InstitutionId == inst && x.StudentId == studentId, ct))
            await records.AdmitWithinTransaction(user, payload.ProgramId, new AdmitStudent(studentId, payload.AdmissionTermId), ct);
        if (!await db.StudentCurriculumAssignments.AsNoTracking().AnyAsync(x => x.InstitutionId == inst && x.StudentId == studentId, ct))
            await curriculum.AssignWithinTransaction(user, studentId, new AssignStudentCurriculum(payload.PlanId), ct);
        if(payload.FullName is string name)
        {
            var profile=await db.Set<StudentProfile>().SingleOrDefaultAsync(x=>x.StudentId==studentId&&x.InstitutionId==inst,ct);
            if(profile is null)db.Set<StudentProfile>().Add(new(){StudentId=studentId,InstitutionId=inst,FullName=name,ExternalReference=payload.ExternalId});
            else if(profile.FullName!=name||profile.ExternalReference!=payload.ExternalId){profile.FullName=name;profile.ExternalReference=payload.ExternalId;profile.Version++;}
        }
        if(payload.PublishedSectionId is Guid publishedSection)await records.SyncTranscriptWithinTransaction(user,publishedSection,ct);
        var mapping = await db.ExternalIdentityMaps.SingleOrDefaultAsync(x => x.InstitutionId == inst && x.SourceSystem == job.SourceSystem && x.ExternalId == payload.ExternalId, ct);
        if (mapping is null) db.ExternalIdentityMaps.Add(new ExternalIdentityMap { InstitutionId = inst, SourceSystem = job.SourceSystem, ExternalId = payload.ExternalId, StudentId = studentId, ImportRowId = row.Id });
        row.Status = "Applied"; row.StudentId = studentId; row.Issue = null; row.AppliedAtUtc = DateTime.UtcNow;
        db.InstitutionalOutboxEvents.Add(new InstitutionalOutboxEvent { InstitutionId = inst, ImportRowId = row.Id,
            PayloadJson = JsonSerializer.Serialize(new { JobId = jobId, RowId = row.Id, StudentId = studentId }) });
        security.Audit(actor.Id, inst, "import.row.applied", row.Id.ToString());
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
    }
    public async Task<ImportReport> Report(ClaimsPrincipal user, Guid inst, Guid id, CancellationToken ct)
    {
        await security.RequireAdminAsync(user, inst, ct);
        var job = await db.ImportJobs.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id && x.InstitutionId == inst, ct) ?? throw ApiFault.NotFound();
        var rows = await db.ImportRows.AsNoTracking().Where(x => x.JobId == id && x.InstitutionId == inst).OrderBy(x => x.Number)
            .Select(x => new ImportRowResult(x.Number, x.Status, x.StudentId, x.Issue)).ToArrayAsync(ct);
        return new(new(job.Id, job.SourceSystem, job.Digest, job.Status, job.CreatedBy, job.ApprovedBy, rows.Length, rows.Count(x => x.Status == "Applied"), rows.Count(x => x.Status is "Failed" or "Invalid")), rows);
    }

    public async Task<ImportReport> Reconcile(ClaimsPrincipal user, Guid inst, Guid id, CancellationToken ct)
    {
        var report = await Report(user, inst, id, ct);
        var rows = await db.ImportRows.AsNoTracking().Where(x => x.JobId == id && x.InstitutionId == inst).OrderBy(x => x.Number).ToListAsync(ct);
        var results = new List<ImportRowResult>();
        foreach (var row in rows)
        {
            if (row.Status != "Applied") { results.Add(new(row.Number, row.Status, row.StudentId, row.Issue)); continue; }
            var input = JsonSerializer.Deserialize<ImportStudentRecord>(row.PayloadJson)!;
            var check = await Validate(inst, report.Job.SourceSystem, input, ct);
            var complete = check.Issue is null && check.StudentId == row.StudentId &&
                await db.ExternalIdentityMaps.AnyAsync(x => x.InstitutionId == inst && x.SourceSystem == report.Job.SourceSystem && x.ExternalId == input.ExternalId && x.StudentId == row.StudentId, ct) &&
                await db.StudentProgramMemberships.AnyAsync(x => x.InstitutionId == inst && x.StudentId == row.StudentId && x.ProgramId == input.ProgramId && x.AdmissionTermId == input.AdmissionTermId, ct) &&
                await db.StudentCurriculumAssignments.AnyAsync(x => x.InstitutionId == inst && x.StudentId == row.StudentId && x.PlanId == input.PlanId, ct);
            if(complete&&input.FullName is string name)complete=await db.Set<StudentProfile>().AnyAsync(x=>x.StudentId==row.StudentId&&x.InstitutionId==inst&&x.FullName==name,ct);
            if(complete&&input.PublishedSectionId is Guid section)complete=await db.StudentTranscriptEntries.Join(db.GradebookResults,t=>new{t.SectionId,t.StudentId},g=>new{g.SectionId,g.StudentId},(t,g)=>new{t,g})
                .AnyAsync(x=>x.t.SectionId==section&&x.t.StudentId==row.StudentId&&x.t.InstitutionId==inst&&x.g.InstitutionId==inst&&x.t.GradebookVersion==x.g.Version,ct);
            results.Add(new(row.Number, complete ? "Reconciled" : "DriftDetected", row.StudentId, complete ? null : check.Issue ?? "ImportedStateChanged"));
        }
        return report with { Rows = results };
    }
    public async Task<ImportReport> UploadCsv(ClaimsPrincipal user,Guid inst,IFormFile file,Guid clientRequestId,string sourceSystem,string fieldsJson,CancellationToken ct)
    {
        await security.RequireAdminAsync(user,inst,ct);
        if(file is null||file.Length is <=0 or >2*1024*1024)throw ApiFault.Bad("CSV file size must be 1 byte to 2 MiB.");
        ImportFieldMap map;try{map=JsonSerializer.Deserialize<ImportFieldMap>(fieldsJson,new JsonSerializerOptions(JsonSerializerDefaults.Web))??throw ApiFault.Bad("Field mapping required.");}catch(JsonException){throw ApiFault.Bad("Invalid field mapping JSON.");}
        using var reader=new StreamReader(file.OpenReadStream(),new UTF8Encoding(false,true),true);
        IReadOnlyList<Dictionary<string,string?>> rows;try{rows=ImportCsvParser.Parse(reader);}catch(DecoderFallbackException){throw ApiFault.Bad("CSV must contain valid UTF-8.");}
        return await Upload(user,inst,new(clientRequestId,sourceSystem,map,rows),ct);
    }
    public async Task<IReadOnlyList<ImportProposal>> Preview(ClaimsPrincipal user,Guid inst,Guid id,CancellationToken ct)
    {
        await security.RequireAdminAsync(user,inst,ct);
        if(!await db.ImportJobs.AnyAsync(x=>x.Id==id&&x.InstitutionId==inst,ct))throw ApiFault.NotFound();
        return (await db.ImportRows.AsNoTracking().Where(x=>x.JobId==id&&x.InstitutionId==inst).OrderBy(x=>x.Number).ToListAsync(ct)).Select(x=>{
            var record=JsonSerializer.Deserialize<ImportStudentRecord>(x.PayloadJson)!;return new ImportProposal(x.Number,x.StudentId,record.ProgramId,record.PlanId,record.FullName,record.PublishedSectionId,x.Status,x.Issue,record.InviteIfMissing);}).ToArray();
    }
    public async Task<IReadOnlyList<ImportJobView>> History(ClaimsPrincipal user,Guid inst,int page,CancellationToken ct)
    {
        await security.RequireAdminAsync(user,inst,ct);if(page is <1 or >100000)throw ApiFault.Bad("Invalid page.");
        return await db.ImportJobs.AsNoTracking().Where(x=>x.InstitutionId==inst).OrderByDescending(x=>x.CreatedAtUtc).ThenBy(x=>x.Id).Skip((page-1)*50).Take(50)
            .Select(x=>new ImportJobView(x.Id,x.SourceSystem,x.Digest,x.Status,x.CreatedBy,x.ApprovedBy,
                db.ImportRows.Count(r=>r.JobId==x.Id),db.ImportRows.Count(r=>r.JobId==x.Id&&r.Status=="Applied"),db.ImportRows.Count(r=>r.JobId==x.Id&&(r.Status=="Failed"||r.Status=="Invalid")))).ToArrayAsync(ct);
    }

}
