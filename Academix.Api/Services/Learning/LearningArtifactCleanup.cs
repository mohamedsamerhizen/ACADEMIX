using Academix.Api.Data;
using Academix.Api.Services.Infrastructure;
using Academix.Api.Storage;
using Microsoft.EntityFrameworkCore;

namespace Academix.Api.Services.Learning;

public sealed class LearningArtifactCleanup(IServiceScopeFactory scopes)
{
    public static string Key(Guid institution,Guid section,Guid file)=>$"{institution:N}/{section:N}/{file:N}";
    public async Task Schedule(Guid institution,Guid section,Guid file,CancellationToken ct)
    {
        await using var scope=scopes.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<AcademixDbContext>();
        OutboxService.Record(db,institution,"LearningArtifactCleanup",Guid.NewGuid(),new{SectionId=section,FileId=file});
        await db.SaveChangesAsync(ct);
    }
    public static async Task Reconcile(AcademixDbContext db,IPrivateFileStore files,Guid institution,Guid section,Guid file,CancellationToken ct)
    {
        await using var tx=await db.Database.BeginTransactionAsync(ct);
        await db.Institutions.FromSqlInterpolated($"SELECT * FROM institutions WHERE \"Id\"={institution} FOR UPDATE").SingleAsync(ct);
        var key=Key(institution,section,file);
        if(!await db.LearningFiles.AsNoTracking().AnyAsync(x=>x.InstitutionId==institution&&x.ObjectKey==key,ct))await files.RemoveAsync(key,ct);
        await tx.CommitAsync(ct);
    }
}
