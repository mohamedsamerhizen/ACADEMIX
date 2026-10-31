using Academix.Api.Domain.Governance;
using Academix.Api.Domain.Academic;
using Academix.Api.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Academix.Api.Data;

public static class GovernanceConfiguration
{
    public static void Apply(ModelBuilder b)
    {
        b.Entity<StaffChangeRequest>(e =>
        {
            e.ToTable("governance_staff_requests"); e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.InstitutionId, x.ClientRequestId }).IsUnique();
            e.HasIndex(x => new { x.InstitutionId, x.Status, x.CreatedAtUtc });
            e.Property(x => x.Action).HasMaxLength(20).IsRequired();
            e.Property(x => x.Role).HasMaxLength(40).IsRequired();
            e.Property(x => x.Reason).HasMaxLength(1000).IsRequired();
            e.Property(x => x.ReviewReason).HasMaxLength(1000).IsRequired();
            e.Property(x => x.Status).HasMaxLength(20).IsRequired();
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasOne<Institution>().WithMany().HasForeignKey(x => x.InstitutionId)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_p11_staff_institution");
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.TargetUserId)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_p11_staff_target");
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.RequestedBy)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_p11_staff_requester");
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ReviewedBy)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_p11_staff_reviewer");
        });
        b.Entity<EnrollmentBatch>(e =>
        {
            e.ToTable("governance_enrollment_batches"); e.HasKey(x => x.Id);
            e.HasAlternateKey(x => new { x.Id, x.InstitutionId });
            e.HasIndex(x => new { x.InstitutionId, x.ClientRequestId }).IsUnique();
            e.HasIndex(x => new { x.InstitutionId, x.Status, x.CreatedAtUtc }).HasDatabaseName("IX_p11_batch_listing");
            e.Property(x => x.Status).HasMaxLength(20).IsRequired();
            e.HasOne<CourseSection>().WithMany().HasForeignKey(x => new { x.SectionId, x.InstitutionId })
                .HasPrincipalKey(x => new { x.Id, x.InstitutionId })
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_p11_batch_section");
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.CreatedBy)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_p11_batch_creator");
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.CommittedBy)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_p11_batch_committer");
        });
        b.Entity<EnrollmentBatchItem>(e =>
        {
            e.ToTable("governance_enrollment_batch_items"); e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.BatchId, x.StudentId }).IsUnique();
            e.Property(x => x.Result).HasMaxLength(20).IsRequired();
            e.HasOne<EnrollmentBatch>().WithMany().HasForeignKey(x => new { x.BatchId, x.InstitutionId })
                .HasPrincipalKey(x => new { x.Id, x.InstitutionId })
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_p11_batch_item_batch");
            // Student IDs may be unresolved during draft import; preview checks eligibility before commit.
        });
    }
}
