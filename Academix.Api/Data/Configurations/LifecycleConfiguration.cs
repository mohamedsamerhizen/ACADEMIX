using Academix.Api.Domain.Academic;
using Academix.Api.Domain.Courses;
using Academix.Api.Domain.Identity;
using Academix.Api.Domain.Lifecycle;
using Microsoft.EntityFrameworkCore;

namespace Academix.Api.Data;

public static class LifecycleConfiguration
{
    public static void Apply(ModelBuilder b)
    {
        b.Entity<AdmissionApplication>(e =>
        {
            e.ToTable("admission_applications", t => t.HasCheckConstraint("CK_p16_admission_status",
                "\"Status\" IN ('Submitted','Approved','Rejected')"));
            e.HasKey(x => x.Id); e.Property(x => x.Id).ValueGeneratedNever();
            e.HasIndex(x => new { x.InstitutionId, x.StudentId, x.Status }).HasDatabaseName("IX_p16_admission_student");
            e.HasIndex(x => new { x.InstitutionId, x.CreatedAtUtc });
            e.HasIndex(x => new { x.InstitutionId, x.StudentId }).IsUnique()
                .HasFilter("\"Status\" = 'Submitted'").HasDatabaseName("IX_p16_admission_one_pending");
            e.Property(x => x.Statement).HasMaxLength(1500).IsRequired();
            e.Property(x => x.DecisionReason).HasMaxLength(500);
            e.Property(x => x.Status).HasMaxLength(20).IsRequired();
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasOne<AcademicProgram>().WithMany().HasForeignKey(x => new { x.ProgramId, x.InstitutionId })
                .HasPrincipalKey(x => new { x.Id, x.InstitutionId }).OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_p16_admission_program");
            e.HasOne<AcademicTerm>().WithMany().HasForeignKey(x => new { x.TermId, x.InstitutionId })
                .HasPrincipalKey(x => new { x.Id, x.InstitutionId }).OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_p16_admission_term");
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.StudentId)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_p16_admission_student");
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ReviewedById)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_p16_admission_reviewer");
        });
        b.Entity<TransferCreditPetition>(e =>
        {
            e.ToTable("transfer_credit_petitions", t => t.HasCheckConstraint("CK_p16_transfer_status",
                "\"Status\" IN ('Submitted','Approved','Rejected')"));
            e.HasKey(x => x.Id); e.Property(x => x.Id).ValueGeneratedNever();
            e.HasAlternateKey(x => new { x.Id, x.InstitutionId });
            e.HasIndex(x => new { x.InstitutionId, x.StudentId, x.CreatedAtUtc }).HasDatabaseName("IX_p16_transfer_student");
            e.Property(x => x.OriginInstitution).HasMaxLength(150).IsRequired();
            e.Property(x => x.Status).HasMaxLength(20).IsRequired();
            e.Property(x => x.DecisionReason).HasMaxLength(500);
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasOne<AcademicProgram>().WithMany().HasForeignKey(x => new { x.ProgramId, x.InstitutionId })
                .HasPrincipalKey(x => new { x.Id, x.InstitutionId }).OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_p16_transfer_program");
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.StudentId)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_p16_transfer_student");
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ReviewedById)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_p16_transfer_reviewer");
        });
        b.Entity<TransferCreditLine>(e =>
        {
            e.ToTable("transfer_credit_lines");
            e.HasKey(x => x.Id); e.Property(x => x.Id).ValueGeneratedNever();
            e.HasIndex(x => new { x.InstitutionId, x.PetitionId, x.TargetCourseId }).IsUnique()
                .HasDatabaseName("IX_p16_transfer_line_unique");
            e.Property(x => x.OriginCourseCode).HasMaxLength(30).IsRequired();
            e.Property(x => x.OriginCourseTitle).HasMaxLength(200).IsRequired();
            e.Property(x => x.ProposedCreditHours).HasPrecision(5, 2);
            e.HasOne<TransferCreditPetition>().WithMany().HasForeignKey(x => new { x.PetitionId, x.InstitutionId })
                .HasPrincipalKey(x => new { x.Id, x.InstitutionId }).OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_p16_line_transfer");
            e.HasOne<Course>().WithMany().HasForeignKey(x => new { x.TargetCourseId, x.InstitutionId })
                .HasPrincipalKey(x => new { x.Id, x.InstitutionId }).OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_p16_line_course");
        });
        b.Entity<GraduationApplication>(e =>
        {
            e.ToTable("graduation_applications", t => t.HasCheckConstraint("CK_p16_graduation_status",
                "\"Status\" IN ('Submitted','UnderReview','Approved','Rejected','Issued')"));
            e.HasKey(x => x.Id); e.Property(x => x.Id).ValueGeneratedNever();
            e.HasIndex(x => new { x.InstitutionId, x.StudentId, x.CreatedAtUtc }).HasDatabaseName("IX_p16_graduation_student");
            e.HasIndex(x => new { x.InstitutionId, x.StudentId }).IsUnique()
                .HasFilter("\"Status\" <> 'Rejected'").HasDatabaseName("IX_p16_graduation_one_active");
            e.HasIndex(x => x.CredentialNumber).IsUnique().HasDatabaseName("IX_p16_credential_unique");
            e.Property(x => x.Status).HasMaxLength(20).IsRequired();
            e.Property(x => x.DecisionReason).HasMaxLength(500);
            e.Property(x => x.CredentialNumber).HasMaxLength(64);
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasOne<AcademicProgram>().WithMany().HasForeignKey(x => new { x.ProgramId, x.InstitutionId })
                .HasPrincipalKey(x => new { x.Id, x.InstitutionId }).OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_p16_graduation_program");
            e.HasOne<Academix.Api.Domain.Curriculum.CurriculumPlan>().WithMany()
                .HasForeignKey(x => new { x.PlanId, x.InstitutionId })
                .HasPrincipalKey(x => new { x.Id, x.InstitutionId }).OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_p16_graduation_plan");
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.StudentId)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_p16_graduation_student");
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ReviewedById)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_p16_graduation_reviewer");
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.IssuedById)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_p16_graduation_issuer");
        });
        b.Entity<LifecycleDecisionEvent>(e =>
        {
            e.ToTable("lifecycle_decision_events");
            e.HasKey(x => x.Id); e.Property(x => x.Id).ValueGeneratedNever();
            e.HasIndex(x => new { x.InstitutionId, x.AtUtc }).HasDatabaseName("IX_p16_event_institution");
            e.Property(x => x.EntityKind).HasMaxLength(30).IsRequired();
            e.Property(x => x.Action).HasMaxLength(30).IsRequired();
            e.Property(x => x.Reason).HasMaxLength(500).IsRequired();
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.StudentId)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_p16_event_student");
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ActorId)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_p16_event_actor");
        });
    }
}
