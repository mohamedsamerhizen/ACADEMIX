using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Testcontainers.PostgreSql;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Academix.Api.Storage;

namespace Academix.Tests.Integration;

public sealed class ApiFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:18-alpine")
        .WithDatabase("academix_tests")
        .WithUsername("academix_tests")
        .WithPassword("isolated-test-only-password")
        .Build();

    public IReadOnlyDictionary<string, string?>? StorageConfiguration { get; init; }
    public Action<IServiceCollection>? ConfigureTestServices { get; init; }
    private WebApplicationFactory<Program>? _factory;
    public HttpClient Client { get; private set; } = null!;
    public IServiceProvider Services => _factory!.Services;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            // Minimal hosting validates JWT options before the late app-configuration callback.
            // Supply the isolated test key through host settings as well; keep production validation intact.
            builder.UseSetting("Jwt:SigningKey", "ACADEMIX-TEST-SIGNING-KEY-ONLY-DO-NOT-USE-IN-REAL-ENVIRONMENT-2026");
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:Default"] = _postgres.GetConnectionString(),
                    ["Database:ApplyMigrationsOnStartup"] = "true",
                    ["BackgroundProcessing:Enabled"] = "false",
                    ["Documents:PdfLicense"] = "Evaluation",
                    ["Documents:LegacyVerificationKey"] = "ACADEMIX-TEST-SIGNING-KEY-ONLY-DO-NOT-USE-IN-REAL-ENVIRONMENT-2026",
                    ["Jwt:SigningKey"] = "ACADEMIX-TEST-SIGNING-KEY-ONLY-DO-NOT-USE-IN-REAL-ENVIRONMENT-2026"
                });
                if (StorageConfiguration is not null) config.AddInMemoryCollection(StorageConfiguration);
            });
            builder.ConfigureServices(services=>{
                services.RemoveAll<IPrivateFileStore>();
                if (StorageConfiguration is null)
                {
                    services.AddSingleton<TestArtifactStore>();
                    services.AddSingleton<IPrivateFileStore>(p=>p.GetRequiredService<TestArtifactStore>());
                }
                else services.AddSingleton<IPrivateFileStore>(p=>p.GetRequiredService<PrivateFileStore>());
                ConfigureTestServices?.Invoke(services);
            });
        });

        // Triggers application startup and executes the real EF migration.
        Client = _factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        Client?.Dispose();
        _factory?.Dispose();
        await _postgres.DisposeAsync();
    }
}
