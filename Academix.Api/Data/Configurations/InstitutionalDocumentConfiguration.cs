using Academix.Api.Domain.Identity;
using Academix.Api.Domain.Institutional;
using Microsoft.EntityFrameworkCore;

namespace Academix.Api.Data;

public static class InstitutionalDocumentConfiguration
{
    public static void Apply(ModelBuilder b)
    {
        b.Entity<InstitutionalDocumentPolicy>(e =>
        {
            e.ToTable("institutional_document_policies");
            e.HasKey(x => x.InstitutionId);
            e.Property(x => x.InstitutionId).ValueGeneratedNever();
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasOne<Academix.Api.Domain.Academic.Institution>().WithMany()
                .HasForeignKey(x => x.InstitutionId).OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_p17_policy_institution");
        });
        b.Entity<InstitutionalDocumentRequest>(e =>
        {
            e.ToTable("institutional_document_requests", t => t.HasCheckConstraint("CK_p17_request_state",
                "\"Status\" IN ('Submitted','Approved','Rejected','Cancelled','Issued')"));
            e.HasKey(x => x.Id); e.Property(x => x.Id).ValueGeneratedNever();
            e.HasAlternateKey(x => new { x.Id, x.InstitutionId });
            e.HasIndex(x => new { x.InstitutionId, x.StudentId, x.ClientRequestId }).IsUnique()
                .HasDatabaseName("IX_p17_request_idempotency");
            e.HasIndex(x => new { x.InstitutionId, x.Status, x.SubmittedAtUtc })
                .HasDatabaseName("IX_p17_request_inbox");
            e.Property(x => x.Kind).HasMaxLength(24).IsRequired();
            e.Property(x => x.Purpose).HasMaxLength(300).IsRequired();
            e.Property(x => x.Status).HasMaxLength(20).IsRequired();
            e.Property(x => x.DecisionReason).HasMaxLength(500);
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.StudentId)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_p17_request_student");
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ReviewedById)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_p17_request_reviewer");
        });
        b.Entity<IssuedAcademicDocument>(e =>
        {
            e.ToTable("issued_academic_documents");
            e.HasKey(x => x.Id); e.Property(x => x.Id).ValueGeneratedNever();
            e.HasIndex(x => x.ReferenceNumber).IsUnique().HasDatabaseName("IX_p17_document_reference");
            e.HasIndex(x => new { x.InstitutionId, x.StudentId, x.IssuedAtUtc })
                .HasDatabaseName("IX_p17_document_student");
            e.HasIndex(x => new { x.RequestId, x.InstitutionId }).IsUnique()
                .HasDatabaseName("IX_p17_document_one_per_request");
            e.Property(x => x.Kind).HasMaxLength(24).IsRequired();
            e.Property(x => x.ReferenceNumber).HasMaxLength(48).IsRequired();
            e.Property(x => x.SnapshotJson).HasColumnType("text").IsRequired();
            e.Property(x => x.RevocationReason).HasMaxLength(500);
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasOne<InstitutionalDocumentRequest>().WithMany()
                .HasForeignKey(x => new { x.RequestId, x.InstitutionId })
                .HasPrincipalKey(x => new { x.Id, x.InstitutionId })
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_p17_document_request");
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.StudentId)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_p17_document_student");
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.IssuedById)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_p17_document_issuer");
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.RevokedById)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_p17_document_revoker");
        });
        b.Entity<InstitutionalDocumentEvent>(e =>
        {
            e.ToTable("institutional_document_events");
            e.HasKey(x => x.Id); e.Property(x => x.Id).ValueGeneratedNever();
            e.HasIndex(x => new { x.InstitutionId, x.AtUtc }).HasDatabaseName("IX_p17_event_institution");
            e.Property(x => x.Action).HasMaxLength(25).IsRequired();
            e.Property(x => x.Detail).HasMaxLength(500).IsRequired();
            e.HasOne<InstitutionalDocumentRequest>().WithMany()
                .HasForeignKey(x => new { x.RequestId, x.InstitutionId })
                .HasPrincipalKey(x => new { x.Id, x.InstitutionId })
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_p17_event_request");
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.StudentId)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_p17_event_student");
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ActorId)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_p17_event_actor");
        });
    }
}
