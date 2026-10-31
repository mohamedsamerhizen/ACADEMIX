using Academix.Api.Domain.Academic;
using Academix.Api.Domain.Assessment;
using Academix.Api.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Academix.Api.Data;

public static class GradebookConfiguration
{
    public static void Apply(ModelBuilder b)
    {
        b.Entity<SectionGradePolicy>(e =>
        {
            e.ToTable("gradebook_policies"); e.HasKey(x => x.SectionId);
            e.Property(x => x.SectionId).ValueGeneratedNever();
            e.HasAlternateKey(x => new { x.SectionId, x.InstitutionId });
            e.Property(x => x.PassingPercentage).HasPrecision(10,2);
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasOne<CourseSection>().WithMany().HasForeignKey(x => new { x.SectionId, x.InstitutionId })
                .HasPrincipalKey(x => new { x.Id, x.InstitutionId }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_p9_policy_section");
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UpdatedBy)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_p9_policy_actor");
        });
        b.Entity<GradeCategory>(e =>
        {
            e.ToTable("gradebook_categories"); e.HasKey(x => x.Id);
            e.HasAlternateKey(x => new { x.Id, x.SectionId, x.InstitutionId });
            e.HasIndex(x => new { x.SectionId, x.Name }).IsUnique();
            e.Property(x => x.Name).HasMaxLength(120).IsRequired();
            e.Property(x => x.WeightPercentage).HasPrecision(10,2);
            e.HasOne<SectionGradePolicy>().WithMany().HasForeignKey(x => new { x.SectionId, x.InstitutionId })
                .HasPrincipalKey(x => new { x.SectionId, x.InstitutionId }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_p9_category_policy");
        });
        b.Entity<GradeComponent>(e =>
        {
            e.ToTable("gradebook_components"); e.HasKey(x => x.Id);
            e.HasAlternateKey(x => new { x.Id, x.SectionId, x.InstitutionId });
            e.HasIndex(x => new { x.CategoryId, x.Name }).IsUnique();
            e.HasIndex(x => new { x.SectionId, x.SourceKind, x.SourceId }).IsUnique().HasFilter("\"SourceId\" IS NOT NULL");
            e.Property(x => x.Name).HasMaxLength(160).IsRequired();
            e.Property(x => x.SourceKind).HasMaxLength(24).IsRequired();
            e.Property(x => x.WeightPercentage).HasPrecision(10,2);
            e.Property(x => x.MaximumPoints).HasPrecision(10,2);
            e.HasOne<GradeCategory>().WithMany().HasForeignKey(x => new { x.CategoryId, x.SectionId, x.InstitutionId })
                .HasPrincipalKey(x => new { x.Id, x.SectionId, x.InstitutionId }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_p9_component_category");
        });
        b.Entity<GradeManualEntry>(e =>
        {
            e.ToTable("gradebook_manual_entries"); e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.ComponentId, x.StudentId }).IsUnique();
            e.Property(x => x.Points).HasPrecision(10,2);
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasOne<GradeComponent>().WithMany().HasForeignKey(x => new { x.ComponentId, x.SectionId, x.InstitutionId })
                .HasPrincipalKey(x => new { x.Id, x.SectionId, x.InstitutionId }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_p9_entry_component");
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.StudentId)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_p9_entry_student");
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.GradedBy)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_p9_entry_grader");
        });
        b.Entity<GradebookResult>(e =>
        {
            e.ToTable("gradebook_results",t=>t.HasCheckConstraint("CK_remediation_grade_evidence","jsonb_typeof(\"SourceEvidenceJson\"::jsonb) IN ('object','array')")); e.HasKey(x => x.Id);
            e.Property(x=>x.SourceEvidenceJson).HasColumnType("text").IsRequired();
            e.HasIndex(x => new { x.SectionId, x.StudentId }).IsUnique();
            e.Property(x => x.Percentage).HasPrecision(10,2);
            e.Property(x => x.Letter).HasMaxLength(2).IsRequired();
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasOne<SectionGradePolicy>().WithMany().HasForeignKey(x => new { x.SectionId, x.InstitutionId })
                .HasPrincipalKey(x => new { x.SectionId, x.InstitutionId }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_p9_result_policy");
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.StudentId)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_p9_result_student");
        });
        b.Entity<GradebookAudit>(e =>
        {
            e.ToTable("gradebook_audit",t=>t.HasCheckConstraint("CK_remediation_command","\"CommandId\" IS NULL OR (\"CommandFingerprint\" IS NOT NULL AND length(\"CommandFingerprint\")=64 AND \"ResultJson\" IS NOT NULL)")); e.HasKey(x => x.Id);
            e.Property(x=>x.EvidenceJson).HasColumnType("text").IsRequired();
            e.Property(x=>x.ResultJson).HasColumnType("text");
            e.Property(x=>x.CommandFingerprint).HasMaxLength(64);
            e.HasIndex(x=>new{x.InstitutionId,x.CommandId}).IsUnique().HasFilter("\"CommandId\" IS NOT NULL");
            e.HasIndex(x => new { x.SectionId, x.AtUtc });
            e.Property(x => x.Action).HasMaxLength(30).IsRequired();
            e.Property(x => x.PreviousPercentage).HasPrecision(10,2);
            e.Property(x => x.NewPercentage).HasPrecision(10,2);
            e.Property(x => x.Reason).HasMaxLength(1000).IsRequired();
            e.HasOne<SectionGradePolicy>().WithMany().HasForeignKey(x => new { x.SectionId, x.InstitutionId })
                .HasPrincipalKey(x => new { x.SectionId, x.InstitutionId }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_p9_audit_policy");
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.StudentId)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_p9_audit_student");
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ActorId)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_p9_audit_actor");
        });
    }
}
