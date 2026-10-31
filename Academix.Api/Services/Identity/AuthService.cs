using Academix.Api.Services.Infrastructure;
using System.Security.Claims;
using Academix.Api.Data;
using Academix.Api.Domain.Identity;
using Academix.Api.DTOs.Identity;
using Academix.Api.Middleware;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace Academix.Api.Services.Identity;

public sealed class AuthService(AcademixDbContext db, UserManager<ApplicationUser> users,
    SignInManager<ApplicationUser> signIn, IdentitySecurity security, RetryingServiceWork retry)
{
    public async Task<AuthResponse> LoginAsync(LoginRequest request, CancellationToken ct)
    {
        IdentityInputPolicy.Login(request);
        var user = await users.FindByEmailAsync(request.Email.Trim());
        if (user is null) throw ApiFault.Unauthorized();
        var result = await signIn.CheckPasswordSignInAsync(user, request.Password, lockoutOnFailure: true);
        if (!result.Succeeded || !user.IsActive) throw ApiFault.Unauthorized();
        var roles = await users.GetRolesAsync(user);
        if (!roles.Contains(AcademixRoles.SuperAdministrator) &&
            (user.InstitutionId is not Guid inst || !await db.Institutions.AnyAsync(x => x.Id == inst && x.IsActive, ct)))
            throw ApiFault.Unauthorized();
        return await NewSessionAsync(user, Guid.NewGuid(), DateTime.UtcNow.AddDays(7), ct);
    }

    private async Task<AuthResponse> NewSessionAsync(ApplicationUser user, Guid familyId, DateTime expires, CancellationToken ct)
    {
        var token = IdentitySecurity.RandomToken();
        db.RefreshSessions.Add(new RefreshSession { UserId = user.Id, FamilyId = familyId, TokenHash = IdentitySecurity.TokenHash(token), ExpiresAtUtc = expires });
        await db.SaveChangesAsync(ct);
        var roles = await users.GetRolesAsync(user);
        var stamp = await users.GetSecurityStampAsync(user);
        return new AuthResponse(security.IssueAccessToken(user, roles.ToArray(), stamp), token, DateTime.UtcNow.AddMinutes(10), expires);
    }

    public Task<AuthResponse> RefreshAsync(RefreshRequest request, CancellationToken ct)
        => retry.ExecuteAsync<AuthService, AuthResponse>(service => service.RefreshAsyncCore(request, ct), ct);

    private async Task<AuthResponse> RefreshAsyncCore(RefreshRequest request, CancellationToken ct)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.RefreshToken)) throw ApiFault.Unauthorized();
        var hash = IdentitySecurity.TokenHash(request.RefreshToken);
        var strategy = db.Database.CreateExecutionStrategy();
        return await strategy.ExecuteAsync(async () =>
        {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            var ownerId = await db.RefreshSessions.AsNoTracking().Where(x => x.TokenHash == hash)
                .Select(x => (Guid?)x.UserId).SingleOrDefaultAsync(ct) ?? throw ApiFault.Unauthorized();
            await db.Users.FromSqlInterpolated($"SELECT * FROM identity_users WHERE \"Id\" = {ownerId} FOR UPDATE").ToListAsync(ct);
            // Lock the concrete token row so rotation and replay cannot race.
            var sessions = await db.RefreshSessions.FromSqlInterpolated(
                $"SELECT * FROM identity_refresh_sessions WHERE \"TokenHash\" = {hash} FOR UPDATE").ToListAsync(ct);
            var session = sessions.SingleOrDefault();
            if (session is null) throw ApiFault.Unauthorized();
            var now = DateTime.UtcNow;
            if (session.RevokedAtUtc.HasValue)
            {
                // A replayed rotated token revokes all descendants in its family.
                await db.RefreshSessions.Where(x => x.UserId == session.UserId && x.FamilyId == session.FamilyId && x.RevokedAtUtc == null)
                    .ExecuteUpdateAsync(set => set.SetProperty(x => x.RevokedAtUtc, now), ct);
                await tx.CommitAsync(ct);
                throw ApiFault.Unauthorized();
            }
            if (session.ExpiresAtUtc <= now) throw ApiFault.Unauthorized();
            var user = await users.FindByIdAsync(session.UserId.ToString());
            if (user is null || !user.IsActive) throw ApiFault.Unauthorized();
            var roles = await users.GetRolesAsync(user);
            if (!roles.Contains(AcademixRoles.SuperAdministrator) &&
                (user.InstitutionId is not Guid inst || !await db.Institutions.AnyAsync(x => x.Id == inst && x.IsActive, ct)))
                throw ApiFault.Unauthorized();
            session.RevokedAtUtc = now;
            var next = await NewSessionAsync(user, session.FamilyId, session.ExpiresAtUtc, ct);
            await tx.CommitAsync(ct);
            return next;
        });
    }

    public Task LogoutAsync(ClaimsPrincipal principal, RefreshRequest request, CancellationToken ct)
        => retry.ExecuteAsync<AuthService>(service => service.LogoutAsyncCore(principal, request, ct), ct);

    private async Task LogoutAsyncCore(ClaimsPrincipal principal, RefreshRequest request, CancellationToken ct)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.RefreshToken)) throw ApiFault.Bad("Refresh token required.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var (user, _) = await security.CurrentAsync(principal, ct);
        await db.Users.FromSqlInterpolated($"SELECT * FROM identity_users WHERE \"Id\" = {user.Id} FOR UPDATE").ToListAsync(ct);
        var hash = IdentitySecurity.TokenHash(request.RefreshToken);
        var session = await db.RefreshSessions.AsNoTracking().SingleOrDefaultAsync(x => x.UserId == user.Id && x.TokenHash == hash, ct);
        if (session is not null)
            await db.RefreshSessions.Where(x => x.UserId == user.Id && x.FamilyId == session.FamilyId && x.RevokedAtUtc == null)
                .ExecuteUpdateAsync(x => x.SetProperty(t => t.RevokedAtUtc, DateTime.UtcNow), ct);
        await tx.CommitAsync(ct);
    }

    public async Task<object> ProfileAsync(ClaimsPrincipal principal, CancellationToken ct)
    {
        var (user, roles) = await security.CurrentAsync(principal, ct);
        return new { user.Id, user.Email, user.InstitutionId, user.IsActive, Roles = roles };
    }

    public Task<object> CreateUserAsync(ClaimsPrincipal principal, ProvisionUserRequest req, CancellationToken ct)
        => retry.ExecuteAsync<AuthService, object>(service => service.CreateUserAsyncCore(principal, req, ct), ct);

    private async Task<object> CreateUserAsyncCore(ClaimsPrincipal principal, ProvisionUserRequest req, CancellationToken ct)
    {
        if (req is null || !AcademixRoles.All.Contains(req.Role)) throw ApiFault.Bad("Unknown role.");
        var (actor, actorRoles) = await security.CurrentAsync(principal, ct);
        var global = actorRoles.Contains(AcademixRoles.SuperAdministrator);
        if (!global && (!actorRoles.Contains(AcademixRoles.InstitutionAdministrator) ||
            actor.InstitutionId != req.InstitutionId || req.Role is AcademixRoles.SuperAdministrator or AcademixRoles.InstitutionAdministrator)) throw ApiFault.Forbidden();
        if (req.Role == AcademixRoles.SuperAdministrator && (!global || req.InstitutionId.HasValue)) throw ApiFault.Forbidden();
        if (req.Role != AcademixRoles.SuperAdministrator &&
            (req.InstitutionId is not Guid institution || !await db.Institutions.AnyAsync(x => x.Id == institution && x.IsActive, ct))) throw ApiFault.Bad("Active institution required.");
        IdentityInputPolicy.Provision(req);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var user = new ApplicationUser { Id = Guid.NewGuid(), Email = req.Email.Trim(), UserName = req.Email.Trim(), InstitutionId = req.InstitutionId, IsActive = true };
        var created = await users.CreateAsync(user, req.Password);
        if (!created.Succeeded) throw ApiFault.Bad(string.Join("; ", created.Errors.Select(x => x.Description)));
        var assigned = await users.AddToRoleAsync(user, req.Role);
        if (!assigned.Succeeded) throw ApiFault.Bad("Role assignment failed.");
        security.Audit(actor.Id, req.InstitutionId, "user.created", user.Id.ToString());
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return new { user.Id, user.Email, user.InstitutionId, Role = req.Role };
    }

    public Task ChangeRoleAsync(ClaimsPrincipal principal, Guid targetId, ChangeRoleRequest req, CancellationToken ct)
        => retry.ExecuteAsync<AuthService>(service => service.ChangeRoleAsyncCore(principal, targetId, req, ct), ct);

    private async Task ChangeRoleAsyncCore(ClaimsPrincipal principal, Guid targetId, ChangeRoleRequest req, CancellationToken ct)
    {
        if (req is null || !AcademixRoles.All.Contains(req.Role)) throw ApiFault.Bad("Unknown role.");
        if (req is null) throw ApiFault.Bad("Request required.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Users.FromSqlInterpolated($"SELECT * FROM identity_users WHERE \"Id\" = {targetId} FOR UPDATE").ToListAsync(ct);
        var (actor, actorRoles) = await security.CurrentAsync(principal, ct);
        var target = await users.FindByIdAsync(targetId.ToString()) ?? throw ApiFault.NotFound();
        var global = actorRoles.Contains(AcademixRoles.SuperAdministrator);
        if (!global && (!actorRoles.Contains(AcademixRoles.InstitutionAdministrator) || actor.InstitutionId != target.InstitutionId ||
            req.Role is AcademixRoles.SuperAdministrator or AcademixRoles.InstitutionAdministrator)) throw ApiFault.Forbidden();
        if (req.Role == AcademixRoles.SuperAdministrator && (target.InstitutionId.HasValue || !global)) throw ApiFault.Forbidden();
        if (!req.Add && req.Role == AcademixRoles.SuperAdministrator && actor.Id == target.Id)
            throw ApiFault.Bad("Cannot remove your own global administrator role.");
        var outcome = req.Add ? await users.AddToRoleAsync(target, req.Role) : await users.RemoveFromRoleAsync(target, req.Role);
        if (!outcome.Succeeded) throw ApiFault.Bad("Role operation rejected.");
        if (!(await users.UpdateSecurityStampAsync(target)).Succeeded)
            throw ApiFault.Conflict("Could not invalidate existing sessions.");
        await db.RefreshSessions.Where(x => x.UserId == target.Id && x.RevokedAtUtc == null)
            .ExecuteUpdateAsync(x => x.SetProperty(t => t.RevokedAtUtc, DateTime.UtcNow), ct);
        security.Audit(actor.Id, target.InstitutionId, "user.role.changed", targetId.ToString());
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }

    public Task ChangeActiveAsync(ClaimsPrincipal principal, Guid targetId, ChangeActiveRequest req, CancellationToken ct)
        => retry.ExecuteAsync<AuthService>(service => service.ChangeActiveAsyncCore(principal, targetId, req, ct), ct);

    private async Task ChangeActiveAsyncCore(ClaimsPrincipal principal, Guid targetId, ChangeActiveRequest req, CancellationToken ct)
    {
        if (req is null) throw ApiFault.Bad("Request required.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await db.Users.FromSqlInterpolated($"SELECT * FROM identity_users WHERE \"Id\" = {targetId} FOR UPDATE").ToListAsync(ct);
        var (actor, actorRoles) = await security.CurrentAsync(principal, ct);
        var target = await users.FindByIdAsync(targetId.ToString()) ?? throw ApiFault.NotFound();
        var global = actorRoles.Contains(AcademixRoles.SuperAdministrator);
        if (!global && (!actorRoles.Contains(AcademixRoles.InstitutionAdministrator) || actor.InstitutionId != target.InstitutionId)) throw ApiFault.Forbidden();
        if (target.Id == actor.Id && !req.IsActive) throw ApiFault.Bad("Cannot deactivate own administrator account.");
        if (!global && await users.IsInRoleAsync(target, AcademixRoles.SuperAdministrator)) throw ApiFault.Forbidden();
        target.IsActive = req.IsActive;
        var outcome = await users.UpdateAsync(target);
        if (!outcome.Succeeded) throw ApiFault.Bad("User update rejected.");
        if (!(await users.UpdateSecurityStampAsync(target)).Succeeded)
            throw ApiFault.Conflict("Could not invalidate existing sessions.");
        await db.RefreshSessions.Where(x => x.UserId == target.Id && x.RevokedAtUtc == null)
            .ExecuteUpdateAsync(x => x.SetProperty(t => t.RevokedAtUtc, DateTime.UtcNow), ct);
        if (!req.IsActive)
            await db.RefreshSessions.Where(x => x.UserId == target.Id && x.RevokedAtUtc == null)
                .ExecuteUpdateAsync(x => x.SetProperty(s => s.RevokedAtUtc, DateTime.UtcNow), ct);
        security.Audit(actor.Id, target.InstitutionId, "user.active.changed", targetId.ToString());
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
    }
}
