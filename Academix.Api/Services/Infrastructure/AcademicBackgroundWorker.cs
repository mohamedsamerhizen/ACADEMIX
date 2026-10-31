using Academix.Api.Data;
using Academix.Api.Services.Communications;
using Academix.Api.Services.Examinations;
using Microsoft.EntityFrameworkCore;

namespace Academix.Api.Services.Infrastructure;

public sealed class AcademicBackgroundWorker(IServiceScopeFactory scopes, IConfiguration configuration,
    ILogger<AcademicBackgroundWorker> logger) : BackgroundService
{
    private Guid? afterAttempt;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuration.GetValue("BackgroundProcessing:Enabled", true)) return;
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(15));
        do
        {
            try { await Tick(stoppingToken); }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Academic background processing failed; pending database work will be retried."); }
        } while (await timer.WaitForNextTickAsync(stoppingToken));
    }

    public async Task Tick(CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        await Isolate("examination scan", async () =>
        {
            await using var scope = scopes.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AcademixDbContext>();
            var now = DateTime.UtcNow;
            var query = db.ExamAttempts.AsNoTracking().Where(x => x.Status == "InProgress" && x.DeadlineUtc <= now);
            // Rotate through bounded pages so 100 persistent failures cannot starve later work.
            if (afterAttempt is Guid cursor) query = query.Where(x => x.Id.CompareTo(cursor) > 0);
            var attempts = await query.OrderBy(x => x.Id).Take(100).Select(x => x.Id).ToListAsync(ct);
            afterAttempt = attempts.Count == 100 ? attempts[^1] : null;
            foreach (var id in attempts)
            {
                ct.ThrowIfCancellationRequested();
                await Isolate($"exam attempt {id}", async () =>
                {
                    await using var itemScope = scopes.CreateAsyncScope();
                    await itemScope.ServiceProvider.GetRequiredService<ExamExpirationService>().FinalizeOne(id, ct);
                }, ct);
            }
        }, ct);
        await Isolate("outbox scheduling", async () =>
        {
            await using var scope = scopes.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<OutboxService>().QueueDue(ct);
        }, ct);
        await Isolate("outbox delivery", async () =>
        {
            await using var scope = scopes.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<OutboxService>().ProcessBatch(ct);
        }, ct);
    }

    private async Task Isolate(string operation, Func<Task> work, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        try { await work(); }
        catch (OperationCanceledException) when (ct.IsCancellationRequested) { throw; }
        catch (Exception ex) { logger.LogError(ex, "Background operation {Operation} failed; pending work will be retried.", operation); }
    }
}
