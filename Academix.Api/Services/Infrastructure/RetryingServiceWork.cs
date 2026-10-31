using Academix.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Academix.Api.Services.Infrastructure;

/// <summary>
/// Runs each EF execution-strategy attempt in a NEW DI scope, preventing retries
/// from reusing tracked entities from an aborted/ambiguous transaction.
/// The delegated service method is responsible for one complete transaction.
/// </summary>
public sealed class RetryingServiceWork(AcademixDbContext db, IServiceScopeFactory scopes)
{
    public Task<TResult> ExecuteAsync<TService, TResult>(
        Func<TService, Task<TResult>> operation, CancellationToken ct)
        where TService : notnull
    {
        var strategy = db.Database.CreateExecutionStrategy();
        return strategy.ExecuteAsync(async () =>
        {
            ct.ThrowIfCancellationRequested();
            await using var scope = scopes.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<TService>();
            return await operation(service);
        });
    }

    public Task ExecuteAsync<TService>(
        Func<TService, Task> operation, CancellationToken ct)
        where TService : notnull
    {
        var strategy = db.Database.CreateExecutionStrategy();
        return strategy.ExecuteAsync(async () =>
        {
            ct.ThrowIfCancellationRequested();
            await using var scope = scopes.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<TService>();
            await operation(service);
        });
    }
}
