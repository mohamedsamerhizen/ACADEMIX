using Academix.Api.Extensions;

if (args.Contains("--health-probe", StringComparer.Ordinal))
{
    using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(4) };
    try
    {
        using var response = await client.GetAsync("http://127.0.0.1:8080/health/ready");
        Environment.ExitCode = response.IsSuccessStatusCode ? 0 : 1;
    }
    catch (HttpRequestException)
    {
        Environment.ExitCode = 1;
    }
    catch (TaskCanceledException)
    {
        Environment.ExitCode = 1;
    }

    return;
}

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddAcademixServices(builder.Configuration);

var app = builder.Build();
app.UseAcademixApi();
await app.ApplyConfiguredMigrationsAsync();
await app.RunAsync();

public partial class Program;
