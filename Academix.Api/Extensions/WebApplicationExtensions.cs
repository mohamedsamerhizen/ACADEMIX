using Academix.Api.Data;
using Academix.Api.Services.Identity;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;

namespace Academix.Api.Extensions;

public static class WebApplicationExtensions
{
    public static WebApplication UseAcademixApi(this WebApplication app)
    {
        app.UseExceptionHandler();
        app.UseRouting();
        app.UseAuthentication();
        app.UseRateLimiter();
        app.UseAuthorization();

        if (app.Environment.IsDevelopment())
            app.MapOpenApi();

        app.MapControllers();
        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
        app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = _ => true });
        return app;
    }

    public static async Task ApplyConfiguredMigrationsAsync(this WebApplication app)
    {
        if (!app.Configuration.GetValue<bool>("Database:ApplyMigrationsOnStartup"))
            return;

        await using var scope = app.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AcademixDbContext>();
        await db.Database.MigrateAsync();
        await IdentityBootstrap.EnsureRolesAndOptionalAdminAsync(app.Services, app.Configuration);
    }
}
