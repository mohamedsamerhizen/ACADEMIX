using Academix.Api.Services.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Academix.Tests.Unit;

public sealed class BackgroundWorkerCancellationTests
{
    [Fact]
    public async Task CancelledTickPropagatesCancellationBeforeResolvingDatabaseWork()
    {
        await using var services=new ServiceCollection().BuildServiceProvider();
        using var cancellation=new CancellationTokenSource(); cancellation.Cancel();
        using var worker=new AcademicBackgroundWorker(services.GetRequiredService<IServiceScopeFactory>(),
            new ConfigurationBuilder().Build(),NullLogger<AcademicBackgroundWorker>.Instance);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>worker.Tick(cancellation.Token));
    }
}
