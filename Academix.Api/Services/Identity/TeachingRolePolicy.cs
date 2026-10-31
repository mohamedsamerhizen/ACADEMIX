using Academix.Api.Domain.Identity;

namespace Academix.Api.Services.Identity;

/// <summary>Returns only teaching assignment kinds for which a user still has a persisted role.</summary>
public static class TeachingRolePolicy
{
    public static string[] ActiveAssignmentKinds(IEnumerable<string> currentRoles)
    {
        var roles = new HashSet<string>(currentRoles, StringComparer.Ordinal);
        var kinds = new List<string>(2);
        if (roles.Contains(AcademixRoles.Instructor)) kinds.Add(AcademixRoles.Instructor);
        if (roles.Contains(AcademixRoles.TeachingAssistant)) kinds.Add(AcademixRoles.TeachingAssistant);
        return kinds.ToArray();
    }
}
