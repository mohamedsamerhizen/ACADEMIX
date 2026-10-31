using Academix.Api.Domain.Academic;
using Academix.Api.Domain.Advising;
using Academix.Api.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Academix.Api.Data;

public static class AdvisingConfiguration
{
    public static void Apply(ModelBuilder b)
    {
        b.Entity<AdvisingCase>(e =>
        {
            e.ToTable("advising_cases", t => t.HasCheckConstraint("CK_p13_case_status",
                "\"Status\" IN ('Open','InProgress','Resolved')"));
            e.HasKey(x => x.Id); e.Property(x => x.Id).ValueGeneratedNever();
            e.HasAlternateKey(x => new { x.Id, x.InstitutionId });
            e.HasIndex(x => new { x.InstitutionId, x.ClientRequestId }).IsUnique().HasDatabaseName("IX_p13_case_request");
            e.HasIndex(x => new { x.InstitutionId, x.SectionId, x.Status, x.CreatedAtUtc }).HasDatabaseName("IX_p13_case_section");
            e.HasIndex(x => new { x.InstitutionId, x.StudentId, x.CreatedAtUtc }).HasDatabaseName("IX_p13_case_student");
            e.HasIndex(x => x.OpenedBy).HasDatabaseName("IX_p13_case_actor");
            e.Property(x => x.Title).HasMaxLength(160).IsRequired();
            e.Property(x => x.InternalSummary).HasMaxLength(3000).IsRequired();
            e.Property(x => x.Status).HasMaxLength(20).IsRequired();
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasOne<CourseSection>().WithMany().HasForeignKey(x => new { x.SectionId, x.InstitutionId })
                .HasPrincipalKey(x => new { x.Id, x.InstitutionId }).OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_p13_case_section");
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.StudentId)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_p13_case_student");
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.OpenedBy)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_p13_case_actor");
        });
        b.Entity<AdvisingNote>(e =>
        {
            e.ToTable("advising_notes"); e.HasKey(x => x.Id); e.Property(x => x.Id).ValueGeneratedNever();
            e.HasIndex(x => new { x.CaseId, x.AtUtc }).HasDatabaseName("IX_p13_note_case");
            e.HasIndex(x => new { x.CaseId, x.InstitutionId }).HasDatabaseName("IX_p13_note_fk");
            e.Property(x => x.Body).HasMaxLength(4000).IsRequired();
            e.HasOne<AdvisingCase>().WithMany().HasForeignKey(x => new { x.CaseId, x.InstitutionId })
                .HasPrincipalKey(x => new { x.Id, x.InstitutionId }).OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_p13_note_case");
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.AuthorId)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_p13_note_author");
        });
        b.Entity<AdvisingTask>(e =>
        {
            e.ToTable("advising_tasks", t => t.HasCheckConstraint("CK_p13_task_status",
                "\"Status\" IN ('Pending','ReportedDone','Completed')"));
            e.HasKey(x => x.Id); e.Property(x => x.Id).ValueGeneratedNever();
            e.HasIndex(x => new { x.CaseId, x.Status, x.DueAtUtc }).HasDatabaseName("IX_p13_task_case_status");
            e.HasIndex(x => new { x.CaseId, x.InstitutionId }).HasDatabaseName("IX_p13_task_fk");
            e.HasIndex(x => x.CreatedBy).HasDatabaseName("IX_p13_task_creator");
            e.HasIndex(x => x.ReviewedBy).HasDatabaseName("IX_p13_task_reviewer");
            e.Property(x => x.Description).HasMaxLength(1000).IsRequired();
            e.Property(x => x.Status).HasMaxLength(20).IsRequired();
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasOne<AdvisingCase>().WithMany().HasForeignKey(x => new { x.CaseId, x.InstitutionId })
                .HasPrincipalKey(x => new { x.Id, x.InstitutionId }).OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_p13_task_case");
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.CreatedBy)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_p13_task_creator");
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ReviewedBy)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_p13_task_reviewer");
        });
        b.Entity<AdvisingEvent>(e =>
        {
            e.ToTable("advising_events"); e.HasKey(x => x.Id); e.Property(x => x.Id).ValueGeneratedNever();
            e.HasIndex(x => new { x.CaseId, x.AtUtc }).HasDatabaseName("IX_p13_event_case");
            e.HasIndex(x => new { x.CaseId, x.InstitutionId }).HasDatabaseName("IX_p13_event_fk");
            e.HasIndex(x => x.ActorId).HasDatabaseName("IX_p13_event_actor");
            e.Property(x => x.Kind).HasMaxLength(40).IsRequired();
            e.Property(x => x.Description).HasMaxLength(1100).IsRequired();
            e.HasOne<AdvisingCase>().WithMany().HasForeignKey(x => new { x.CaseId, x.InstitutionId })
                .HasPrincipalKey(x => new { x.Id, x.InstitutionId }).OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_p13_event_case");
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ActorId)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_p13_event_actor");
        });
    }
}
