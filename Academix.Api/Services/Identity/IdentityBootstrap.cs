using Academix.Api.Domain.Identity;
using Microsoft.AspNetCore.Identity;

namespace Academix.Api.Services.Identity;

public static class IdentityBootstrap
{
    public static async Task EnsureRolesAndOptionalAdminAsync(IServiceProvider services, IConfiguration config)
    {
        using var scope = services.CreateScope();
        var roles = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
        foreach (var name in AcademixRoles.All)
        {
            if (await roles.RoleExistsAsync(name)) continue;
            var result = await roles.CreateAsync(new ApplicationRole { Id = Guid.NewGuid(), Name = name, NormalizedName = name.ToUpperInvariant() });
            if (!result.Succeeded) throw new InvalidOperationException("Role initialization failed.");
        }
        if (!config.GetValue<bool>("Bootstrap:Enabled")) return;
        var email = config["Bootstrap:Email"];
        var password = config["Bootstrap:Password"];
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
            throw new InvalidOperationException("Bootstrap enabled but missing credentials.");
        var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var existing = await users.FindByEmailAsync(email);
        if (existing is null)
        {
            existing = new ApplicationUser { Id = Guid.NewGuid(), Email = email, UserName = email, InstitutionId = null, EmailConfirmed = true };
            var created = await users.CreateAsync(existing, password);
            if (!created.Succeeded) throw new InvalidOperationException("Admin bootstrap password rejected by Identity policy.");
        }
        if (existing.InstitutionId.HasValue) throw new InvalidOperationException("Bootstrap user is scoped to an institution.");
        if (!await users.IsInRoleAsync(existing, AcademixRoles.SuperAdministrator))
        {
            var assigned = await users.AddToRoleAsync(existing, AcademixRoles.SuperAdministrator);
            if (!assigned.Succeeded) throw new InvalidOperationException("Bootstrap role assignment failed.");
        }
    }
}
