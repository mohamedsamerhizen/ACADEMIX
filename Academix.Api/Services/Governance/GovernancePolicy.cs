using Academix.Api.Domain.Identity;
using Academix.Api.Middleware;

namespace Academix.Api.Services.Governance;

public static class GovernancePolicy
{
    public const int MaximumBatch = 100;
    public static (string Action, string Role, string Reason) StaffChange(string? action, string? role, string? reason)
    {
        var chosenAction = action?.Trim() ?? "";
        var chosenRole = role?.Trim() ?? "";
        if (chosenAction is not ("AddRole" or "RemoveRole" or "Activate" or "Deactivate"))
            throw ApiFault.Bad("Unsupported staff action.");
        if (chosenAction is "AddRole" or "RemoveRole")
        {
            if (chosenRole is not (AcademixRoles.Student or AcademixRoles.Instructor or AcademixRoles.TeachingAssistant))
                throw ApiFault.Bad("Administrative or global roles cannot be changed by governance workflow.");
        }
        else if (chosenRole.Length != 0) throw ApiFault.Bad("Account status actions cannot include a role.");
        return (chosenAction, chosenRole, RequiredText(reason, 1000, "reason"));
    }
    public static string RequiredText(string? text, int maxLength, string name)
    {
        var clean = text?.Trim() ?? "";
        if (clean.Length is 0 || clean.Length > maxLength) throw ApiFault.Bad($"Invalid {name} length.");
        return clean;
    }
    public static Guid[] BatchStudents(IReadOnlyList<Guid>? studentIds)
    {
        if (studentIds is null || studentIds.Count is < 1 or > MaximumBatch ||
            studentIds.Any(x => x == Guid.Empty) || studentIds.Distinct().Count() != studentIds.Count)
            throw ApiFault.Bad("Student list must contain 1–100 distinct valid IDs.");
        return studentIds.OrderBy(x => x).ToArray();
    }
    public static void Review(Guid actor, Guid creator, Guid target, long actualVersion, long expectedVersion, string status)
    {
        if (actor == creator || actor == target) throw ApiFault.Forbidden();
        if (status != "Pending" || expectedVersion != actualVersion) throw ApiFault.Conflict("Request already reviewed or stale.");
    }
    public static (int Offset, int Limit) Page(int offset, int limit)
    {
        if (offset is < 0 or > 100000 || limit is < 1 or > 100) throw ApiFault.Bad("Invalid pagination.");
        return (offset, limit);
    }
}
