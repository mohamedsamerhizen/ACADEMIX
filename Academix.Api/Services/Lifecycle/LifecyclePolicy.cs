using Academix.Api.DTOs.Lifecycle;
using Academix.Api.Middleware;

namespace Academix.Api.Services.Lifecycle;

public static class LifecyclePolicy
{
    public static string Description(string? value, int min, int max)
    {
        var text = value?.Trim() ?? "";
        if (text.Length < min || text.Length > max) throw ApiFault.Bad($"Text must be {min}-{max} characters.");
        return text;
    }

    public static string Decision(string decision)
    {
        if (decision is not ("Approve" or "Reject")) throw ApiFault.Bad("Decision must be Approve or Reject.");
        return decision;
    }

    public static void ValidateTransfer(SubmitTransferCredit body)
    {
        Description(body.OriginInstitution, 3, 150);
        if (body.Courses is null || body.Courses.Count is < 1 or > 25)
            throw ApiFault.Bad("Transfer request requires 1-25 courses.");
        if (body.Courses.Select(x => x.TargetCourseId).Distinct().Count() != body.Courses.Count)
            throw ApiFault.Bad("Target courses must be unique.");
        foreach (var row in body.Courses)
        {
            if (row.TargetCourseId == Guid.Empty) throw ApiFault.Bad("Target course is required.");
            Description(row.OriginCourseCode, 2, 30);
            Description(row.OriginCourseTitle, 3, 200);
            if (row.ProposedCreditHours is <= 0 or > 30 ||
                decimal.Round(row.ProposedCreditHours, 2) != row.ProposedCreditHours)
                throw ApiFault.Bad("Transfer credits must be 0.01-30.00.");
        }
    }

    public static void DisallowSelfApproval(Guid actorId, Guid studentId)
    {
        if (actorId == studentId) throw ApiFault.Forbidden();
    }

    public static void RequirePending(string status, long actual, long expected)
    {
        if (status != "Submitted" || actual != expected)
            throw ApiFault.Conflict("Request is no longer pending or expected version is stale.");
    }
}
