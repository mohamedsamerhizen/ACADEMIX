using Academix.Api.Data;
using Academix.Api.Services.Infrastructure;
using Academix.Api.Storage;
using Microsoft.EntityFrameworkCore;

namespace Academix.Api.Services.Institutional;

public sealed class DocumentArtifactCleanup(IServiceScopeFactory scopes)
{
    // Commit intent independently BEFORE upload. It survives rollback, cancellation,
    // process death and an ambiguous issuance commit. Each upload gets its own intent.
    public async Task Schedule(Guid institution, Guid document, CancellationToken ct)
    {
        await using var scope = scopes.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AcademixDbContext>();
        OutboxService.Record(db, institution, "DocumentArtifactCleanup", Guid.NewGuid(), new { DocumentId = document });
        await db.SaveChangesAsync(ct);
    }

    public static async Task Reconcile(AcademixDbContext db, IPrivateFileStore files,
        Guid institution, Guid document, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        // Issuance holds the same institution row until its commit outcome is known.
        // Keep this lock through deletion, preventing a retry from uploading meanwhile.
        await db.Institutions.FromSqlInterpolated(
            $"SELECT * FROM institutions WHERE \"Id\" = {institution} FOR UPDATE").SingleAsync(ct);
        var key = $"official/{institution:N}/{document:N}.pdf";
        if (!await db.IssuedAcademicDocuments.AsNoTracking().AnyAsync(x => x.ArtifactKey == key, ct))
            await files.RemoveAsync(key, ct);
        await tx.CommitAsync(ct);
    }
}
