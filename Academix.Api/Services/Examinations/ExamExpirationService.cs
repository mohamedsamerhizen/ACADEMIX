using Academix.Api.Data;
using Academix.Api.Services.Identity;
using Microsoft.EntityFrameworkCore;

namespace Academix.Api.Services.Examinations;

public sealed class ExamExpirationService(AcademixDbContext db)
{
    public async Task FinalizeOne(Guid id, CancellationToken ct)
    {
        var strategy = db.Database.CreateExecutionStrategy();
        await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            // Update returning avoids tracked state across a transient retry.
            var now = DateTime.UtcNow;
            var changed = await db.ExamAttempts.Where(x => x.Id == id && x.Status == "InProgress" && x.DeadlineUtc <= now)
                .ExecuteUpdateAsync(s => s.SetProperty(x => x.Status, "Expired")
                    .SetProperty(x => x.LastActivityAtUtc, now).SetProperty(x => x.Version, x => x.Version + 1), ct);
            if (changed != 0)
            {
                var inst = await db.ExamAttempts.AsNoTracking().Where(x => x.Id == id).Select(x => x.InstitutionId).SingleAsync(ct);
                // The audit and finalization commit together; a replay cannot duplicate either.
                await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO security_audit_events (\"Id\", \"ActorId\", \"InstitutionId\", \"Action\", \"Target\", \"AtUtc\") VALUES ({Guid.NewGuid()}, NULL, {inst}, {"exam.attempt.timeout"}, {id.ToString()}, {now})", ct);
            }
            await tx.CommitAsync(ct);
        });
    }
}
