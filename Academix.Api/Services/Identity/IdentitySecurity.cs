using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Academix.Api.Data;
using Academix.Api.Domain.Identity;
using Academix.Api.Middleware;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace Academix.Api.Services.Identity;

public sealed class IdentitySecurity(AcademixDbContext db, UserManager<ApplicationUser> users, IConfiguration config)
{
    public async Task<(ApplicationUser User, string[] Roles)> CurrentAsync(ClaimsPrincipal principal, CancellationToken ct)
    {
        var idString = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(idString, out var userId)) throw ApiFault.Unauthorized();
        // Authorization must not reuse an account tracked before a resource-lock wait.
        var user = await db.Users.AsNoTracking().SingleOrDefaultAsync(x => x.Id == userId, ct);
        if (user is null || !user.IsActive) throw ApiFault.Unauthorized();
        var claimedInstitution = principal.FindFirstValue("institution_id");
        if (user.InstitutionId?.ToString() != claimedInstitution && !(user.InstitutionId is null && string.IsNullOrEmpty(claimedInstitution)))
            throw ApiFault.Unauthorized();
        var roles = (await users.GetRolesAsync(user)).ToArray();
        if (!roles.Contains(AcademixRoles.SuperAdministrator))
        {
            if (user.InstitutionId is not Guid instId || !await db.Institutions.AnyAsync(x => x.Id == instId && x.IsActive, ct))
                throw ApiFault.Unauthorized();
        }
        return (user, roles);
    }

    public async Task<ApplicationUser> RequireAdminAsync(ClaimsPrincipal principal, Guid institutionId, CancellationToken ct, bool superOnly = false)
    {
        var (user, roles) = await CurrentAsync(principal, ct);
        if (roles.Contains(AcademixRoles.SuperAdministrator)) return user;
        if (!superOnly && roles.Contains(AcademixRoles.InstitutionAdministrator) && user.InstitutionId == institutionId)
            return user;
        throw ApiFault.Forbidden();
    }

    public async Task<ApplicationUser> RequireSectionStaffAsync(ClaimsPrincipal principal, Guid institutionId, Guid sectionId, CancellationToken ct)
    {
        var (user, roles) = await CurrentAsync(principal, ct);
        if (roles.Contains(AcademixRoles.SuperAdministrator) || roles.Contains(AcademixRoles.InstitutionAdministrator) && user.InstitutionId == institutionId)
            return user;
        if (await IsCurrentSectionTeacherAsync(user, roles, institutionId, sectionId, ct)) return user;
        throw ApiFault.Forbidden();
    }

    // This check intentionally consults CURRENT persisted roles, not role claims in an old JWT.
    // It also requires the assignment kind to match an active teaching role.
    public async Task<bool> IsCurrentSectionTeacherAsync(ApplicationUser user, IReadOnlyCollection<string> currentRoles,
        Guid institutionId, Guid sectionId, CancellationToken ct)
    {
        if (user.InstitutionId != institutionId) return false;
        var allowedKinds = TeachingRolePolicy.ActiveAssignmentKinds(currentRoles);
        return allowedKinds.Length > 0 && await db.TeachingAssignments.AnyAsync(x =>
            x.InstitutionId == institutionId && x.SectionId == sectionId && x.UserId == user.Id &&
            allowedKinds.Contains(x.Kind), ct);
    }

    public async Task RequireStudentAsync(ClaimsPrincipal principal, Guid studentId, Guid institutionId, CancellationToken ct)
    {
        var (user, roles) = await CurrentAsync(principal, ct);
        if (roles.Contains(AcademixRoles.SuperAdministrator) || roles.Contains(AcademixRoles.InstitutionAdministrator) && user.InstitutionId == institutionId) return;
        if (user.Id == studentId && user.InstitutionId == institutionId && roles.Contains(AcademixRoles.Student)) return;
        throw ApiFault.Forbidden();
    }

    public static string TokenHash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    public static string RandomToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));

    public string IssueAccessToken(ApplicationUser user, IReadOnlyCollection<string> roles, string stamp)
    {
        var key = config["Jwt:SigningKey"] ?? throw new InvalidOperationException("Jwt:SigningKey is required.");
        var now = DateTime.UtcNow;
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new("security_stamp", stamp),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
        };
        if (user.InstitutionId.HasValue) claims.Add(new Claim("institution_id", user.InstitutionId.Value.ToString()));
        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));
        var token = new JwtSecurityToken(config["Jwt:Issuer"] ?? "ACADEMIX", config["Jwt:Audience"] ?? "ACADEMIX.Api", claims,
            notBefore: now, expires: now.AddMinutes(10),
            signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public void Audit(Guid? actor, Guid? institution, string action, string target)
        => db.SecurityAuditEvents.Add(new SecurityAuditEvent { ActorId = actor, InstitutionId = institution, Action = action, Target = target });
}
