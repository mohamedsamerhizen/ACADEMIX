using Academix.Api.Data;
using Academix.Api.Middleware;
using Microsoft.EntityFrameworkCore;

namespace Academix.Api.Services.Infrastructure;

public static class AcademicFreshness
{
    public static async Task Invalidate(AcademixDbContext db,Guid institutionId,Guid studentId,bool includeEnrollment,CancellationToken ct)
    {
        if(db.Database.CurrentTransaction is null)throw new InvalidOperationException("Invalidation must commit with its academic source change.");
        var now=DateTime.UtcNow;
        await db.IssuedAcademicDocuments.Where(x=>x.InstitutionId==institutionId&&x.StudentId==studentId&&x.SourceInvalidatedAtUtc==null&&
            (includeEnrollment||x.Kind=="Transcript"||x.Kind=="Standing"||x.Kind=="Graduation")).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.SourceInvalidatedAtUtc,now),ct);
        await db.GraduationApplications.Where(x=>x.InstitutionId==institutionId&&x.StudentId==studentId&&x.Status=="Issued"&&x.SourceInvalidatedAtUtc==null)
            .ExecuteUpdateAsync(s=>s.SetProperty(x=>x.SourceInvalidatedAtUtc,now),ct);
    }
    public static Task<bool> StandingStale(AcademixDbContext db,Guid institutionId,Guid studentId,
        Guid termId,DateTime calculatedAtUtc,CancellationToken ct)
        => db.StudentTranscriptEntries.AsNoTracking().AnyAsync(x=>x.InstitutionId==institutionId&&
            x.StudentId==studentId&&x.TermId==termId&&x.SyncedAtUtc>calculatedAtUtc,ct);

    public static async Task Ensure(AcademixDbContext db, Guid institutionId, Guid studentId, CancellationToken ct)
    {
        await AssessmentFreshness.Ensure(db,institutionId,studentId,ct);
        var stale = await db.GradebookResults.AsNoTracking().Where(g => g.InstitutionId == institutionId && g.StudentId == studentId)
            .AnyAsync(g => !db.StudentTranscriptEntries.Any(t => t.InstitutionId == institutionId && t.StudentId == studentId &&
                t.SectionId == g.SectionId && t.GradebookVersion == g.Version && t.Percentage == g.Percentage && t.Passed == g.Passed && t.Letter == g.Letter), ct);
        if (stale) throw ApiFault.Conflict("Academic records are stale. Synchronize the affected section transcripts before continuing.");
    }
}
