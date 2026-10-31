namespace Academix.Api.Domain.AcademicOperations;

public sealed class TimetableSlot
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public Guid SectionId { get; set; }
    public int Weekday { get; set; } // ISO: Monday=1 through Sunday=7
    public int StartsAtMinute { get; set; } // minutes after midnight, institution-local wall clock
    public int DurationMinutes { get; set; }
    public string Room { get; set; } = ""; // Legacy display name retained for older API clients.
    public Guid? RoomId { get; set; } // Explicit managed campus resource.
    public bool IsActive { get; set; } = true;
    public long Version { get; set; } = 1;
}

public sealed class ClassSession
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public Guid SectionId { get; set; }
    public Guid? TimetableSlotId { get; set; }
    public Guid? RoomId { get; set; } // Persisted physical room booking; independent of recurring slot.
    public DateTime StartsAtUtc { get; set; }
    public DateTime EndsAtUtc { get; set; }
    public string Topic { get; set; } = "";
    public string Status { get; set; } = "Planned"; // Planned|Completed|Cancelled
    public long Version { get; set; } = 1;
}

public sealed class AttendanceRecord
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public Guid SectionId { get; set; }
    public Guid SessionId { get; set; }
    public Guid StudentId { get; set; }
    public string Status { get; set; } = "Present"; // Present|Absent|Late|Excused
    public string Note { get; set; } = "";
    public Guid RecordedBy { get; set; }
    public DateTime RecordedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class AcademicCalendarEvent
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public Guid? TermId { get; set; }
    public Guid? SectionId { get; set; }
    public string Title { get; set; } = "";
    public string Description { get; set; } = "";
    public DateTime StartsAtUtc { get; set; }
    public DateTime EndsAtUtc { get; set; }
    public string Audience { get; set; } = "All"; // All|Staff|Students
    public bool IsPublished { get; set; }
    public Guid CreatedBy { get; set; }
}

public sealed class StudentAcademicRequest
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public Guid StudentId { get; set; }
    public Guid? SectionId { get; set; }
    public string Kind { get; set; } = "General"; // General|AttendanceExcuse|EnrollmentException
    public string Subject { get; set; } = "";
    public string Details { get; set; } = "";
    public string? ExecutionDataJson { get; set; }
    public Guid? ClientRequestId { get; set; }
    public DateTime? FulfilledAtUtc { get; set; }
    public Guid? FulfilledBy { get; set; }
    public string? FulfillmentResult { get; set; }
    public string Status { get; set; } = "Pending"; // Pending|Approved|Rejected|Cancelled
    public DateTime SubmittedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? DecidedAtUtc { get; set; }
    public Guid? DecidedBy { get; set; }
    public string DecisionReason { get; set; } = "";
    public long Version { get; set; } = 1;
}

public sealed class AcademicRequestHistory
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public Guid RequestId { get; set; }
    public Guid ActorId { get; set; }
    public string FromStatus { get; set; } = "";
    public string ToStatus { get; set; } = "";
    public string Reason { get; set; } = "";
    public DateTime AtUtc { get; set; } = DateTime.UtcNow;
}
