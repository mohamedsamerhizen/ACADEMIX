using Microsoft.AspNetCore.Identity;

namespace Academix.Api.Domain.Identity;

public sealed class ApplicationUser : IdentityUser<Guid>
{
    public ApplicationUser() { LockoutEnabled = true; }
    public Guid? InstitutionId { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class ApplicationRole : IdentityRole<Guid> { }

public static class AcademixRoles
{
    public const string SuperAdministrator = "SuperAdministrator";
    public const string InstitutionAdministrator = "InstitutionAdministrator";
    public const string Instructor = "Instructor";
    public const string TeachingAssistant = "TeachingAssistant";
    public const string Student = "Student";
    public static readonly string[] All = [SuperAdministrator, InstitutionAdministrator, Instructor, TeachingAssistant, Student];
}

public sealed class RefreshSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public Guid FamilyId { get; set; }
    public string TokenHash { get; set; } = "";
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class SecurityAuditEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid? ActorId { get; set; }
    public Guid? InstitutionId { get; set; }
    public string Action { get; set; } = "";
    public string Target { get; set; } = "";
    public DateTime AtUtc { get; set; } = DateTime.UtcNow;
}
