using Academix.Api.Domain.Academic;
using Academix.Api.Domain.Identity;
using Academix.Api.Domain.StudentServices;
using Microsoft.EntityFrameworkCore;

namespace Academix.Api.Data;

public static class StudentServicesConfiguration
{
    public static void Apply(ModelBuilder b)
    {
        b.Entity<StudentServiceDefinition>(e =>
        {
            e.ToTable("student_service_definitions", t => t.HasCheckConstraint("CK_p23_sla", "\"SlaHours\" BETWEEN 1 AND 720"));
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.HasAlternateKey(x => new { x.Id, x.InstitutionId });
            e.Property(x => x.Code).IsRequired().HasMaxLength(40);
            e.Property(x => x.Name).IsRequired().HasMaxLength(120);
            e.Property(x => x.Description).IsRequired().HasMaxLength(1000);
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasIndex(x => new { x.InstitutionId, x.Code }).IsUnique();
            e.HasOne<Institution>().WithMany().HasForeignKey(x => x.InstitutionId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<StudentServiceCase>(e =>
        {
            e.ToTable("student_service_cases", t =>
            {
                t.HasCheckConstraint("CK_p23_case_status", "\"Status\" IN ('Submitted','InReview','AwaitingStudent','Resolved','Rejected','Closed','Cancelled')");
                t.HasCheckConstraint("CK_p23_case_queue", "\"CurrentQueueCode\" IN ('GeneralSupport','StudentAffairs','ITSupport','Library','CampusServices')");
                t.HasCheckConstraint("CK_p23_case_priority", "\"Priority\" IN ('Normal','High','Urgent')");
            });
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.HasAlternateKey(x => new { x.Id, x.InstitutionId });
            e.Property(x => x.Subject).IsRequired().HasMaxLength(160);
            e.Property(x => x.Details).IsRequired().HasMaxLength(3000);
            e.Property(x => x.Status).IsRequired().HasMaxLength(25);
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasIndex(x => new { x.InstitutionId, x.StudentId, x.ClientRequestId }).IsUnique();
            e.HasIndex(x => new { x.InstitutionId, x.Status, x.DueAtUtc });
            e.HasOne<StudentServiceDefinition>().WithMany()
                .HasForeignKey(x => new { x.DefinitionId, x.InstitutionId })
                .HasPrincipalKey(x => new { x.Id, x.InstitutionId }).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.StudentId)
                .OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<StudentServiceCaseEvent>(e =>
        {
            e.ToTable("student_service_case_events");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.Action).IsRequired().HasMaxLength(30);
            e.Property(x => x.FromStatus).IsRequired().HasMaxLength(25);
            e.Property(x => x.ToStatus).IsRequired().HasMaxLength(25);
            e.Property(x => x.PublicMessage).IsRequired().HasMaxLength(2000);
            e.Property(x => x.PrivateStaffNote).IsRequired().HasMaxLength(2000);
            e.HasIndex(x => new { x.InstitutionId, x.CaseId, x.OccurredAtUtc });
            e.HasOne<StudentServiceCase>().WithMany()
                .HasForeignKey(x => new { x.CaseId, x.InstitutionId })
                .HasPrincipalKey(x => new { x.Id, x.InstitutionId }).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ActorId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<StudentServiceStaffGrant>(e =>
        {
            e.ToTable("student_service_staff_grants");
            e.HasKey(x => x.Id); e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.QueueCode).IsRequired().HasMaxLength(40);
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasIndex(x => new { x.InstitutionId, x.QueueCode, x.UserId }).IsUnique().HasDatabaseName("IX_p23_grant_queue_user");
            e.HasOne<Institution>().WithMany().HasForeignKey(x => x.InstitutionId).OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_p23_staff_institution");
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_p23_staff_user");
        });
        b.Entity<StudentServiceReferral>(e =>
        {
            e.ToTable("student_service_referrals");
            e.HasKey(x => x.Id); e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.FromQueue).IsRequired().HasMaxLength(40);
            e.Property(x => x.ToQueue).IsRequired().HasMaxLength(40);
            e.Property(x => x.Reason).IsRequired().HasMaxLength(1000);
            e.HasIndex(x => new { x.InstitutionId, x.CaseId, x.OccurredAtUtc }).HasDatabaseName("IX_p23_referral_case_date");
            e.HasOne<StudentServiceCase>().WithMany().HasForeignKey(x => new { x.CaseId, x.InstitutionId })
                .HasPrincipalKey(x => new { x.Id, x.InstitutionId }).OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_p23_ref_case");
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ActorId).OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_p23_ref_actor");
        });
        b.Entity<StudentServiceAttachment>(e =>
        {
            e.ToTable("student_service_attachments", t =>
                t.HasCheckConstraint("CK_p23_attachment_size", "\"Size\" BETWEEN 1 AND 2097152"));
            e.HasKey(x => x.Id); e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.FileName).IsRequired().HasMaxLength(160);
            e.Property(x => x.ContentType).IsRequired().HasMaxLength(80);
            e.Property(x => x.Visibility).IsRequired().HasMaxLength(20);
            e.Property(x => x.Sha256).IsRequired().HasMaxLength(64);
            e.Property(x => x.Content).IsRequired().HasColumnType("bytea");
            e.HasIndex(x => new { x.InstitutionId, x.CaseId, x.UploadedAtUtc }).HasDatabaseName("IX_p23_attachment_case_date");
            e.HasOne<StudentServiceCase>().WithMany().HasForeignKey(x => new { x.CaseId, x.InstitutionId })
                .HasPrincipalKey(x => new { x.Id, x.InstitutionId }).OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_p23_attach_case");
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UploadedBy).OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_p23_attach_user");
        });
        b.Entity<StudentServiceCase>(e =>
        {
            e.Property(x => x.CurrentQueueCode).IsRequired().HasMaxLength(40);
            e.Property(x => x.Priority).IsRequired().HasMaxLength(12);
            e.HasIndex(x => new { x.InstitutionId, x.CurrentQueueCode, x.Status, x.DueAtUtc }).HasDatabaseName("IX_p23_case_queue_status_due");
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.AssignedStaffId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
