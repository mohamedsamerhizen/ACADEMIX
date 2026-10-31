namespace Academix.Api.Domain.AcademicOperations;

/// <summary>Concrete UTC room allocation, separate from recurring wall-clock timetable templates.
/// Maintenance blocks room usage; Exam reserves a physical venue without modifying online exam windows.</summary>
public sealed class CampusRoomReservation
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public Guid RoomId { get; set; }
    public string Kind { get; set; } = "Maintenance"; // Maintenance | Exam
    public Guid? SectionId { get; set; }
    public Guid? ExamId { get; set; }
    public Guid? InvigilatorId { get; set; }
    public int? SeatCount { get; set; }
    public DateTime StartsAtUtc { get; set; }
    public DateTime EndsAtUtc { get; set; }
    public string Status { get; set; } = "Active"; // Active | Cancelled
    public Guid ClientRequestId { get; set; }
    public string Reason { get; set; } = "";
    public Guid CreatedBy { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public Guid? CancelledBy { get; set; }
    public DateTime? CancelledAtUtc { get; set; }
    public string CancellationReason { get; set; } = "";
    public long Version { get; set; } = 1;
}
