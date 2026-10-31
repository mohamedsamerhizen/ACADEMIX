using Academix.Api.Domain.Academic;
using Academix.Api.Domain.AcademicOperations;
using Academix.Api.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Academix.Api.Data;

public static class AcademicOperationsConfiguration
{
    public static void Apply(ModelBuilder b)
    {
        b.Entity<TimetableSlot>(e =>
        {
            e.ToTable("academic_timetable_slots", t =>
            {
                t.HasCheckConstraint("CK_timetable_time", "\"Weekday\" BETWEEN 1 AND 7 AND \"StartsAtMinute\" BETWEEN 0 AND 1439 AND \"DurationMinutes\" BETWEEN 15 AND 600 AND \"StartsAtMinute\" + \"DurationMinutes\" <= 1440");
            });
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.HasAlternateKey(x => new { x.Id, x.SectionId, x.InstitutionId });
            e.HasIndex(x => new { x.InstitutionId, x.SectionId, x.Weekday, x.StartsAtMinute }).HasDatabaseName("IX_slots_inst_section_day_start");
            e.Property(x => x.Room).HasMaxLength(120).IsRequired();
            e.HasIndex(x => new { x.RoomId, x.InstitutionId });
            e.HasOne<CampusRoom>().WithMany()
                .HasForeignKey(x => new { x.RoomId, x.InstitutionId })
                .HasPrincipalKey(x => new { x.Id, x.InstitutionId })
                .OnDelete(DeleteBehavior.Restrict);
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasOne<CourseSection>().WithMany().HasForeignKey(x => new { x.SectionId, x.InstitutionId })
                .HasPrincipalKey(x => new { x.Id, x.InstitutionId }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_timetable_section");
        });
        b.Entity<TimetableSlotRevision>(e =>
        {
            e.ToTable("academic_timetable_slot_revisions", t =>
                t.HasCheckConstraint("CK_p22_revision_action", "\"Action\" IN ('Created','Updated','Deactivated')"));
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Action).HasMaxLength(20).IsRequired();
            e.Property(x => x.Reason).HasMaxLength(500).IsRequired();
            e.Property(x => x.BeforeJson).HasColumnType("text");
            e.Property(x => x.AfterJson).HasColumnType("text").IsRequired();
            e.HasIndex(x => x.ActorId).HasDatabaseName("IX_p22_revision_actor");
            e.HasIndex(x => new { x.SlotId, x.SectionId, x.InstitutionId })
                .HasDatabaseName("IX_p22_revision_slot");
            e.HasIndex(x => new { x.InstitutionId, x.SectionId, x.SlotId, x.AtUtc })
                .HasDatabaseName("IX_p22_revision_scope_time");
            e.HasOne<TimetableSlot>().WithMany()
                .HasForeignKey(x => new { x.SlotId, x.SectionId, x.InstitutionId })
                .HasPrincipalKey(x => new { x.Id, x.SectionId, x.InstitutionId })
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_p22_revision_slot");
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ActorId)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_p22_revision_actor");
        });
        b.Entity<ClassSession>(e =>
        {
            e.ToTable("academic_class_sessions", t =>
            {
                t.HasCheckConstraint("CK_session_times", "\"EndsAtUtc\" > \"StartsAtUtc\"");
                t.HasCheckConstraint("CK_session_status", "\"Status\" IN ('Planned','Completed','Cancelled')");
            });
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.HasAlternateKey(x => new { x.Id, x.SectionId, x.InstitutionId });
            e.HasIndex(x => new { x.InstitutionId, x.SectionId, x.StartsAtUtc });
            e.HasIndex(x => new { x.TimetableSlotId, x.SectionId, x.InstitutionId }).HasDatabaseName("IX_sessions_slot_section_inst");
            e.HasIndex(x => new { x.RoomId, x.InstitutionId, x.StartsAtUtc })
                .HasDatabaseName("IX_p22_session_room_utc");
            e.HasOne<CampusRoom>().WithMany()
                .HasForeignKey(x => new { x.RoomId, x.InstitutionId })
                .HasPrincipalKey(x => new { x.Id, x.InstitutionId })
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_p22_session_room");
            e.Property(x => x.Topic).HasMaxLength(200).IsRequired();
            e.Property(x => x.Status).HasMaxLength(20).IsRequired();
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasOne<CourseSection>().WithMany().HasForeignKey(x => new { x.SectionId, x.InstitutionId })
                .HasPrincipalKey(x => new { x.Id, x.InstitutionId }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_session_slot").HasConstraintName("FK_session_section");
            e.HasOne<TimetableSlot>().WithMany()
                .HasForeignKey(x => new { x.TimetableSlotId, x.SectionId, x.InstitutionId })
                .HasPrincipalKey(x => new { x.Id, x.SectionId, x.InstitutionId })
                .OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<ClassSessionRevision>(e =>
        {
            e.ToTable("academic_class_session_revisions");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Reason).HasMaxLength(500).IsRequired();
            e.Property(x => x.BeforeJson).HasColumnType("text").IsRequired();
            e.Property(x => x.AfterJson).HasColumnType("text").IsRequired();
            e.HasIndex(x => new { x.InstitutionId, x.SectionId, x.SessionId, x.AtUtc })
                .HasDatabaseName("IX_p22_session_revision_scope");
            e.HasOne<ClassSession>().WithMany()
                .HasForeignKey(x => new { x.SessionId, x.SectionId, x.InstitutionId })
                .HasPrincipalKey(x => new { x.Id, x.SectionId, x.InstitutionId })
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_p22_session_revision_session");
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ActorId)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_p22_session_revision_actor");
        });
        b.Entity<AttendanceRecord>(e =>
        {
            e.ToTable("academic_attendance_records", t =>
                t.HasCheckConstraint("CK_attendance_status", "\"Status\" IN ('Present','Absent','Late','Excused')"));
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.HasIndex(x => new { x.SessionId, x.StudentId }).IsUnique();
            e.HasIndex(x => new { x.InstitutionId, x.SectionId, x.StudentId }).HasDatabaseName("IX_attendance_inst_section_student");
            e.Property(x => x.Status).HasMaxLength(20).IsRequired();
            e.Property(x => x.Note).HasMaxLength(500).IsRequired();
            e.HasOne<ClassSession>().WithMany().HasForeignKey(x => new { x.SessionId, x.SectionId, x.InstitutionId })
                .HasPrincipalKey(x => new { x.Id, x.SectionId, x.InstitutionId }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_attendance_actor").HasConstraintName("FK_attendance_student").HasConstraintName("FK_attendance_session");
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.RecordedBy).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<AcademicCalendarEvent>(e =>
        {
            e.ToTable("academic_calendar_events", t =>
            {
                t.HasCheckConstraint("CK_calendar_times", "\"EndsAtUtc\" > \"StartsAtUtc\"");
                t.HasCheckConstraint("CK_calendar_audience", "\"Audience\" IN ('All','Staff','Students')");
            });
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.HasIndex(x => new { x.InstitutionId, x.StartsAtUtc });
            e.Property(x => x.Title).HasMaxLength(160).IsRequired();
            e.Property(x => x.Description).HasMaxLength(2000).IsRequired();
            e.Property(x => x.Audience).HasMaxLength(20).IsRequired();
            e.HasOne<Institution>().WithMany().HasForeignKey(x => x.InstitutionId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_calendar_actor").HasConstraintName("FK_calendar_section").HasConstraintName("FK_calendar_term").HasConstraintName("FK_calendar_institution");
            e.HasOne<AcademicTerm>().WithMany().HasForeignKey(x => new { x.TermId, x.InstitutionId })
                .HasPrincipalKey(x => new { x.Id, x.InstitutionId }).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<CourseSection>().WithMany().HasForeignKey(x => new { x.SectionId, x.InstitutionId })
                .HasPrincipalKey(x => new { x.Id, x.InstitutionId }).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<StudentAcademicRequest>(e =>
        {
            e.ToTable("student_academic_requests", t =>
            {
                t.HasCheckConstraint("CK_academic_request_kind", "\"Kind\" IN ('General','AttendanceExcuse','EnrollmentException')");
                t.HasCheckConstraint("CK_academic_request_status", "\"Status\" IN ('Pending','Approved','Rejected','Cancelled')");
            });
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.HasAlternateKey(x => new { x.Id, x.InstitutionId });
            e.HasIndex(x => new { x.InstitutionId, x.Status, x.SubmittedAtUtc }).HasDatabaseName("IX_requests_inst_status_date");
            e.HasIndex(x => new { x.StudentId, x.SubmittedAtUtc });
            e.Property(x => x.Kind).HasMaxLength(30).IsRequired();
            e.Property(x => x.Subject).HasMaxLength(160).IsRequired();
            e.Property(x => x.Details).HasMaxLength(3000).IsRequired();
            e.Property(x => x.Status).HasMaxLength(20).IsRequired();
            e.Property(x => x.DecisionReason).HasMaxLength(2000).IsRequired();
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasOne<Institution>().WithMany().HasForeignKey(x => x.InstitutionId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_request_reviewer").HasConstraintName("FK_request_student").HasConstraintName("FK_request_section").HasConstraintName("FK_request_institution");
            e.HasOne<CourseSection>().WithMany().HasForeignKey(x => new { x.SectionId, x.InstitutionId })
                .HasPrincipalKey(x => new { x.Id, x.InstitutionId }).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.DecidedBy).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<AcademicRequestHistory>(e =>
        {
            e.ToTable("academic_request_history", t =>
                t.HasCheckConstraint("CK_request_history_to", "\"ToStatus\" IN ('Approved','Rejected','Cancelled')"));
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.HasIndex(x => new { x.InstitutionId, x.RequestId, x.AtUtc });
            e.Property(x => x.FromStatus).HasMaxLength(20).IsRequired();
            e.Property(x => x.ToStatus).HasMaxLength(20).IsRequired();
            e.Property(x => x.Reason).HasMaxLength(2000).IsRequired();
            e.HasOne<StudentAcademicRequest>().WithMany().HasForeignKey(x => new { x.RequestId, x.InstitutionId })
                .HasPrincipalKey(x => new { x.Id, x.InstitutionId }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_history_actor").HasConstraintName("FK_history_request");
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ActorId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
