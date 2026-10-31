using Academix.Api.Middleware;

namespace Academix.Api.Services.Institutional;

public static class InstitutionalDocumentPolicyRules
{
    public static string Kind(string? value) => value switch
    {
        "Enrollment" or "Transcript" or "Standing" or "Graduation" => value!,
        _ => throw ApiFault.Bad("Unsupported institutional document kind.")
    };

    public static string Purpose(string? value)
    {
        var cleaned = value?.Trim() ?? "";
        if (cleaned.Length is < 5 or > 300) throw ApiFault.Bad("Purpose must be 5-300 characters.");
        return cleaned;
    }

    public static string Reason(string? value)
    {
        var cleaned = value?.Trim() ?? "";
        if (cleaned.Length is < 5 or > 500) throw ApiFault.Bad("Reason must be 5-500 characters.");
        return cleaned;
    }

    public static int ValidityDays(int value)
    {
        if (value is < 1 or > 365) throw ApiFault.Bad("ValidityDays must be between 1 and 365.");
        return value;
    }

    public static void EnsureVersion(long actual, long expected)
    {
        if (expected <= 0 || actual != expected) throw ApiFault.Conflict("Stale document version.");
    }

    public static void EnsureTransition(string status, string expectedStatus)
    {
        if (status != expectedStatus) throw ApiFault.Conflict("Document workflow state has changed.");
    }

    public static void EnsureIndependentReviewer(Guid student, Guid reviewer)
    {
        if (student == reviewer) throw ApiFault.Forbidden();
    }

    public static bool Allowed(string kind, bool enrollment, bool transcript, bool standing, bool graduation = true) => kind switch
    {
        "Enrollment" => enrollment,
        "Transcript" => transcript,
        "Standing" => standing,
        "Graduation" => graduation,
        _ => false
    };

    public static string State(bool revoked, DateTime expiresAtUtc, DateTime nowUtc) =>
        revoked ? "Revoked" : expiresAtUtc <= nowUtc ? "Expired" : "Valid";
}
