using Academix.Api.Domain.Academic;
using Academix.Api.Domain.Identity;
using Academix.Api.Domain.Quality;
using Academix.Api.Domain.AdvancedLearning;
using Microsoft.EntityFrameworkCore;

namespace Academix.Api.Data;

public static class QualityConfiguration
{
    public static void Apply(ModelBuilder b)
    {
        b.Entity<ProgramQualityOutcome>(e =>
        {
            e.ToTable("quality_program_outcomes", t => t.HasCheckConstraint("CK_p24_target", "\"TargetPassingRate\" BETWEEN 0 AND 100"));
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.HasAlternateKey(x => new { x.Id, x.ProgramId, x.InstitutionId });
            e.Property(x => x.Code).HasMaxLength(30).IsRequired();
            e.Property(x => x.Description).HasMaxLength(1500).IsRequired();
            e.Property(x => x.TargetPassingRate).HasPrecision(5, 2);
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasIndex(x => new { x.InstitutionId, x.ProgramId, x.Code }).IsUnique();
            e.HasOne<AcademicProgram>().WithMany().HasForeignKey(x => new { x.ProgramId, x.InstitutionId })
                .HasPrincipalKey(x => new { x.Id, x.InstitutionId }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_p24_outcome_program");
        });
        b.Entity<ProgramOutcomeCourseMap>(e =>
        {
            e.ToTable("quality_course_alignments");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.HasIndex(x => new { x.InstitutionId, x.OutcomeId, x.CourseId }).IsUnique();
            e.HasOne<ProgramQualityOutcome>().WithMany()
                .HasForeignKey(x => new { x.OutcomeId, x.ProgramId, x.InstitutionId })
                .HasPrincipalKey(x => new { x.Id, x.ProgramId, x.InstitutionId }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_p24_map_outcome");
            e.HasOne<ProgramCourse>().WithMany().HasForeignKey(x => new { x.ProgramId, x.CourseId })
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_p24_map_course");
        });
        b.Entity<ProgramQualityReview>(e =>
        {
            e.ToTable("quality_program_reviews", t => t.HasCheckConstraint("CK_p24_review_status", "\"Status\" IN ('Draft','Published')"));
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.HasAlternateKey(x => new { x.Id, x.InstitutionId });
            e.Property(x => x.Status).HasMaxLength(20).IsRequired();
            e.Property(x => x.SnapshotJson).HasColumnType("text").IsRequired();
            e.Property(x => x.DirectSnapshotJson).HasColumnType("text").IsRequired();
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasIndex(x => new { x.InstitutionId, x.ProgramId, x.TermId }).IsUnique();
            e.HasOne<AcademicProgram>().WithMany().HasForeignKey(x => new { x.ProgramId, x.InstitutionId })
                .HasPrincipalKey(x => new { x.Id, x.InstitutionId }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_p24_review_program");
            e.HasOne<AcademicTerm>().WithMany().HasForeignKey(x => new { x.TermId, x.InstitutionId })
                .HasPrincipalKey(x => new { x.Id, x.InstitutionId }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_p24_review_term");
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_p24_review_actor");
        });
        b.Entity<QualityImprovementAction>(e =>
        {
            e.ToTable("quality_improvement_actions", t => t.HasCheckConstraint("CK_p24_action_status", "\"Status\" IN ('Open','InProgress','Completed')"));
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.HasAlternateKey(x => new { x.Id, x.InstitutionId });
            e.Property(x => x.Status).HasMaxLength(20).IsRequired();
            e.Property(x => x.Description).HasMaxLength(1500).IsRequired();
            e.Property(x => x.ResolutionNote).HasMaxLength(1500).IsRequired();
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasIndex(x => new { x.InstitutionId, x.ReviewId, x.Status });
            e.HasOne<ProgramQualityReview>().WithMany().HasForeignKey(x => new { x.ReviewId, x.InstitutionId })
                .HasPrincipalKey(x => new { x.Id, x.InstitutionId }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_p24_action_review");
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_p24_action_created_by");
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.CompletedBy).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_p24_action_completed_by");
        });
        b.Entity<QualityCriterionAlignment>(e =>
        {
            e.ToTable("quality_rubric_alignments", t => t.HasCheckConstraint("CK_p24_proficiency", "\"ProficiencyThresholdPercent\" BETWEEN 0 AND 100"));
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.ProficiencyThresholdPercent).HasPrecision(5, 2);
            e.HasIndex(x => new { x.InstitutionId, x.OutcomeId, x.CriterionId }).IsUnique();
            e.HasOne<ProgramQualityOutcome>().WithMany().HasForeignKey(x => new { x.OutcomeId, x.ProgramId, x.InstitutionId })
                .HasPrincipalKey(x => new { x.Id, x.ProgramId, x.InstitutionId }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_p24_rubric_outcome");
            e.HasOne<RubricCriterion>().WithMany().HasForeignKey(x => x.CriterionId)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_p24_rubric_criterion");
            e.HasOne<CourseSection>().WithMany().HasForeignKey(x => new { x.SectionId, x.InstitutionId })
                .HasPrincipalKey(x => new { x.Id, x.InstitutionId }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_p24_rubric_section");
        });
        b.Entity<QualityActionEvent>(e =>
        {
            e.ToTable("quality_action_events");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.FromStatus).HasMaxLength(20).IsRequired();
            e.Property(x => x.ToStatus).HasMaxLength(20).IsRequired();
            e.Property(x => x.Note).HasMaxLength(1500).IsRequired();
            e.HasIndex(x => new { x.InstitutionId, x.ActionId, x.AtUtc });
            e.HasOne<QualityImprovementAction>().WithMany().HasForeignKey(x => new { x.ActionId, x.InstitutionId })
                .HasPrincipalKey(x => new { x.Id, x.InstitutionId }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_p24_event_action");
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ActorId)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_p24_event_actor");
        });
        b.Entity<QualityAccreditationPack>(e =>
        {
            e.ToTable("quality_accreditation_packs", t => t.HasCheckConstraint("CK_p24_pack_status", "\"Status\" IN ('Draft','Sealed')"));
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.HasAlternateKey(x => new { x.Id, x.InstitutionId });
            e.Property(x => x.Title).HasMaxLength(200).IsRequired();
            e.Property(x => x.Status).HasMaxLength(20).IsRequired();
            e.Property(x => x.ManifestJson).HasColumnType("text").IsRequired();
            e.Property(x => x.ManifestSha256).HasMaxLength(64).IsRequired();
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasIndex(x => new { x.InstitutionId, x.ReviewId, x.CreatedAtUtc });
            e.HasOne<ProgramQualityReview>().WithMany().HasForeignKey(x => new { x.ReviewId, x.InstitutionId })
                .HasPrincipalKey(x => new { x.Id, x.InstitutionId }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_p24_pack_review");
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.CreatedBy)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_p24_pack_creator");
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.SealedBy)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_p24_pack_sealer");
        });
        b.Entity<QualityPackItem>(e =>
        {
            e.ToTable("quality_pack_items");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.Property(x => x.EvidenceKind).HasMaxLength(40).IsRequired();
            e.Property(x => x.Label).HasMaxLength(200).IsRequired();
            e.Property(x => x.EvidenceJson).HasColumnType("text").IsRequired();
            e.Property(x => x.EvidenceSha256).HasMaxLength(64).IsRequired();
            e.HasIndex(x => new { x.InstitutionId, x.PackId, x.EvidenceKind, x.SourceId }).IsUnique();
            e.HasOne<QualityAccreditationPack>().WithMany().HasForeignKey(x => new { x.PackId, x.InstitutionId })
                .HasPrincipalKey(x => new { x.Id, x.InstitutionId }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_p24_item_pack");
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.AddedBy)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_p24_item_actor");
        });
    }
}
