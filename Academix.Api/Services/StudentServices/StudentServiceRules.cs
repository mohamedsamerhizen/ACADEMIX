using Academix.Api.DTOs.StudentServices;
using Academix.Api.Middleware;

namespace Academix.Api.Services.StudentServices;

public static class StudentServiceRules
{
    private static readonly HashSet<string> ValidCategories = new(StringComparer.Ordinal)
    { "GeneralSupport", "StudentAffairs", "ITSupport", "Library", "CampusServices" };

    public static bool IsQueue(string? code) => code is not null && ValidCategories.Contains(code);

    public static void ValidateAssignment(ServiceCaseAssign value)
    {
        if (value is null || value.ExpectedVersion < 1 || string.IsNullOrWhiteSpace(value.Reason) || value.Reason.Length > 1000)
            throw ApiFault.Bad("Invalid assignment.");
    }
    public static void ValidateReferral(ServiceCaseRefer value, string from)
    {
        if (value is null || !IsQueue(value.TargetQueue) || value.TargetQueue == from ||
            value.ExpectedVersion < 1 || string.IsNullOrWhiteSpace(value.Reason) || value.Reason.Length > 1000)
            throw ApiFault.Bad("Invalid referral.");
    }
    public static void ValidateEscalation(ServiceCaseEscalate value)
    {
        if (value is null || value.Priority is not ("High" or "Urgent") || value.ExpectedVersion < 1 ||
            string.IsNullOrWhiteSpace(value.Reason) || value.Reason.Length > 1000)
            throw ApiFault.Bad("Invalid escalation.");
    }
    public static void ValidateAttachment(string name, string contentType, long length, byte[] contents)
    {
        if (string.IsNullOrWhiteSpace(name) || name.Length > 160 || length is < 1 or > 2097152 ||
            contents.LongLength != length) throw ApiFault.Bad("Invalid attachment size or name.");
        var valid = contentType switch
        {
            "application/pdf" => contents.Length >= 5 && contents.AsSpan(0, 5).SequenceEqual("%PDF-"u8) && name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase),
            "image/png" => contents.Length >= 8 && contents.AsSpan(0, 8).SequenceEqual(new byte[] {137,80,78,71,13,10,26,10}) && name.EndsWith(".png", StringComparison.OrdinalIgnoreCase),
            "image/jpeg" => contents.Length >= 3 && contents[0] == 255 && contents[1] == 216 && contents[2] == 255 && (name.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase)),
            _ => false
        };
        if (!valid) throw ApiFault.Bad("Unsupported attachment type or signature.");
    }

    public static void ValidateDefinition(ServiceDefinitionInput value, bool creating)
    {
        if (value is null) throw ApiFault.Bad("Service definition is required.");
        if (!ValidCategories.Contains(value.Code)) throw ApiFault.Bad("Unsupported general service category. Academic, finance and graduation requests have dedicated workflows.");
        if (string.IsNullOrWhiteSpace(value.Name) || value.Name.Trim().Length > 120 ||
            value.Description is null || value.Description.Length > 1000 || value.SlaHours is < 1 or > 720 ||
            (!creating && value.ExpectedVersion < 1)) throw ApiFault.Bad("Invalid service definition.");
    }

    public static void ValidateSubmission(ServiceCaseSubmit value)
    {
        if (value is null || value.DefinitionId == Guid.Empty || value.ClientRequestId == Guid.Empty ||
            string.IsNullOrWhiteSpace(value.Subject) || value.Subject.Trim().Length > 160 ||
            string.IsNullOrWhiteSpace(value.Details) || value.Details.Trim().Length > 3000)
            throw ApiFault.Bad("Invalid student service request.");
    }

    public static string Transition(string status, string action, bool isStudent)
        => (status, action, isStudent) switch
        {
            ("Submitted", "Start", false) => "InReview",
            ("InReview", "RequestInfo", false) => "AwaitingStudent",
            ("AwaitingStudent", "Reply", true) => "InReview",
            ("InReview", "Resolve", false) => "Resolved",
            ("InReview", "Reject", false) => "Rejected",
            ("Resolved", "Close", false) => "Closed",
            ("Submitted", "Cancel", true) => "Cancelled",
            ("AwaitingStudent", "Cancel", true) => "Cancelled",
            _ => throw ApiFault.Conflict("Service case action is not valid for this actor and state.")
        };

    public static void ValidateAction(ServiceCaseAction value, bool student)
    {
        if (value is null || value.ExpectedVersion < 1 || value.PublicMessage is null ||
            value.PublicMessage.Length > 2000 || value.PrivateStaffNote is null ||
            value.PrivateStaffNote.Length > 2000 ||
            (student && value.PrivateStaffNote.Length != 0) ||
            ((value.Action is "RequestInfo" or "Resolve" or "Reject" or "Reply" or "Cancel") &&
             string.IsNullOrWhiteSpace(value.PublicMessage)))
            throw ApiFault.Bad("Invalid service case action.");
    }
}
