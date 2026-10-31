using Academix.Api.Domain.Academic;
using Academix.Api.Domain.AcademicOperations;
using Microsoft.EntityFrameworkCore;

namespace Academix.Api.Data;

public static class CampusFacilitiesConfiguration
{
    public static void Apply(ModelBuilder b)
    {
        b.Entity<CampusBuilding>(e =>
        {
            e.ToTable("campus_buildings");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.HasAlternateKey(x => new { x.Id, x.InstitutionId });
            e.Property(x => x.Code).HasMaxLength(30).IsRequired();
            e.Property(x => x.Name).HasMaxLength(160).IsRequired();
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasIndex(x => new { x.InstitutionId, x.Code }).IsUnique();
            e.HasOne<Institution>().WithMany().HasForeignKey(x => x.InstitutionId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<CampusRoomReservation>(e =>
        {
            e.ToTable("campus_room_reservations", t =>
            {
                t.HasCheckConstraint("CK_p22_reservation_times", "\"EndsAtUtc\" > \"StartsAtUtc\"");
                t.HasCheckConstraint("CK_p22_reservation_status", "\"Status\" IN ('Active','Cancelled')");
                t.HasCheckConstraint("CK_p22_reservation_kind", "\"Kind\" IN ('Maintenance','Exam')");
                t.HasCheckConstraint("CK_p22_reservation_shape", "(\"Kind\" = 'Maintenance' AND \"SectionId\" IS NULL AND \"ExamId\" IS NULL AND \"InvigilatorId\" IS NULL AND \"SeatCount\" IS NULL) OR (\"Kind\" = 'Exam' AND \"SectionId\" IS NOT NULL AND \"ExamId\" IS NOT NULL AND \"InvigilatorId\" IS NOT NULL AND \"SeatCount\" BETWEEN 1 AND 10000)");
            });
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Kind).HasMaxLength(20).IsRequired();
            e.Property(x => x.Status).HasMaxLength(20).IsRequired();
            e.Property(x => x.Reason).HasMaxLength(500).IsRequired();
            e.Property(x => x.CancellationReason).HasMaxLength(500).IsRequired();
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasIndex(x => x.CancelledBy).HasDatabaseName("IX_reservation_cancelled_by");
            e.HasIndex(x => x.CreatedBy).HasDatabaseName("IX_reservation_created_by");
            e.HasIndex(x => x.ExamId).HasDatabaseName("IX_reservation_exam_fk");
            e.HasIndex(x => x.InvigilatorId).HasDatabaseName("IX_reservation_invigilator");
            e.HasIndex(x => new { x.RoomId, x.InstitutionId }).HasDatabaseName("IX_reservation_room_fk");
            e.HasIndex(x => new { x.SectionId, x.InstitutionId }).HasDatabaseName("IX_reservation_section_fk");
            e.HasIndex(x => new { x.InstitutionId, x.ClientRequestId }).IsUnique().HasDatabaseName("IX_reservation_request");
            e.HasIndex(x => new { x.InstitutionId, x.ExamId }).IsUnique()
                .HasFilter("\"Status\" = 'Active' AND \"Kind\" = 'Exam'").HasDatabaseName("IX_reservation_active_exam");
            e.HasIndex(x => new { x.InstitutionId, x.RoomId, x.StartsAtUtc }).HasDatabaseName("IX_reservation_room_interval");
            e.HasIndex(x => new { x.InstitutionId, x.SectionId, x.StartsAtUtc }).HasDatabaseName("IX_reservation_section_interval");
            e.HasIndex(x => new { x.InstitutionId, x.InvigilatorId, x.StartsAtUtc }).HasDatabaseName("IX_reservation_invigilator_interval");
            e.HasOne<CampusRoom>().WithMany().HasForeignKey(x => new { x.RoomId, x.InstitutionId })
                .HasPrincipalKey(x => new { x.Id, x.InstitutionId }).OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_reservation_room");
            e.HasOne<Academix.Api.Domain.Academic.CourseSection>().WithMany()
                .HasForeignKey(x => new { x.SectionId, x.InstitutionId })
                .HasPrincipalKey(x => new { x.Id, x.InstitutionId }).OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_reservation_section");
            e.HasOne<Academix.Api.Domain.Examinations.ExamDefinition>().WithMany()
                .HasForeignKey(x => x.ExamId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_reservation_exam");
            e.HasOne<Academix.Api.Domain.Identity.ApplicationUser>().WithMany()
                .HasForeignKey(x => x.InvigilatorId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_reservation_invigilator");
            e.HasOne<Academix.Api.Domain.Identity.ApplicationUser>().WithMany()
                .HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_reservation_created_by");
            e.HasOne<Academix.Api.Domain.Identity.ApplicationUser>().WithMany()
                .HasForeignKey(x => x.CancelledBy).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_reservation_cancelled_by");
        });
        b.Entity<CampusRoom>(e =>
        {
            e.ToTable("campus_rooms", t => t.HasCheckConstraint("CK_p22_room_capacity", "\"Capacity\" BETWEEN 1 AND 10000"));
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.HasAlternateKey(x => new { x.Id, x.InstitutionId });
            e.HasIndex(x => new { x.InstitutionId, x.BuildingId, x.Code }).IsUnique();
            e.Property(x => x.Code).HasMaxLength(30).IsRequired();
            e.Property(x => x.Name).HasMaxLength(160).IsRequired();
            e.Property(x => x.Kind).HasMaxLength(30).IsRequired();
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasOne<CampusBuilding>().WithMany()
                .HasForeignKey(x => new { x.BuildingId, x.InstitutionId })
                .HasPrincipalKey(x => new { x.Id, x.InstitutionId })
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
