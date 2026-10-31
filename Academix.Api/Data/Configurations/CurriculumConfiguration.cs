using Academix.Api.Domain.Academic;
using Academix.Api.Domain.Courses;
using Academix.Api.Domain.Curriculum;
using Academix.Api.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Academix.Api.Data;

public static class CurriculumConfiguration
{
    public static void Apply(ModelBuilder b)
    {
        b.Entity<CurriculumPlan>(e =>
        {
            e.ToTable("curriculum_plans", t => t.HasCheckConstraint("CK_p15_plan_status",
                "\"Status\" IN ('Draft','Published')"));
            e.HasKey(x => x.Id); e.Property(x => x.Id).ValueGeneratedNever();
            e.HasAlternateKey(x => new { x.Id, x.InstitutionId });
            e.HasIndex(x => new { x.InstitutionId, x.ProgramId, x.CatalogYear, x.Revision })
                .IsUnique().HasDatabaseName("IX_p15_plan_revision");
            e.Property(x => x.Name).HasMaxLength(120).IsRequired();
            e.Property(x => x.Status).HasMaxLength(20).IsRequired();
            e.Property(x => x.Version).IsConcurrencyToken();
            e.Property(x => x.MinimumTotalCredits).HasPrecision(6, 2);
            e.Property(x => x.MinimumElectiveCredits).HasPrecision(6, 2);
            e.Property(x => x.MinimumCumulativeGpa).HasPrecision(3, 2);
            e.HasOne<AcademicProgram>().WithMany().HasForeignKey(x => new { x.ProgramId, x.InstitutionId })
                .HasPrincipalKey(x => new { x.Id, x.InstitutionId }).OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_p15_plan_program");
        });
        b.Entity<CurriculumCourse>(e =>
        {
            e.ToTable("curriculum_courses", t => t.HasCheckConstraint("CK_p15_course_kind",
                "\"RequirementKind\" IN ('Required','Elective')"));
            e.HasKey(x => x.Id); e.Property(x => x.Id).ValueGeneratedNever();
            e.HasIndex(x => new { x.InstitutionId, x.PlanId, x.CourseId }).IsUnique()
                .HasDatabaseName("IX_p15_plan_course_unique");
            e.Property(x => x.CreditHours).HasPrecision(5, 2);
            e.Property(x => x.RequirementKind).HasMaxLength(16).IsRequired();
            e.HasOne<CurriculumPlan>().WithMany().HasForeignKey(x => new { x.PlanId, x.InstitutionId })
                .HasPrincipalKey(x => new { x.Id, x.InstitutionId }).OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_p15_course_plan");
            e.HasOne<Course>().WithMany().HasForeignKey(x => new { x.CourseId, x.InstitutionId })
                .HasPrincipalKey(x => new { x.Id, x.InstitutionId }).OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_p15_course_course");
        });
        b.Entity<CurriculumGradeBand>(e =>
        {
            e.ToTable("curriculum_grade_bands");
            e.HasKey(x => x.Id); e.Property(x => x.Id).ValueGeneratedNever();
            e.HasIndex(x => new { x.InstitutionId, x.PlanId, x.MinimumPercentage }).IsUnique()
                .HasDatabaseName("IX_p15_plan_grade_band");
            e.Property(x => x.MinimumPercentage).HasPrecision(5, 2);
            e.Property(x => x.GradePoints).HasPrecision(3, 2);
            e.Property(x => x.Letter).HasMaxLength(10).IsRequired();
            e.HasOne<CurriculumPlan>().WithMany().HasForeignKey(x => new { x.PlanId, x.InstitutionId })
                .HasPrincipalKey(x => new { x.Id, x.InstitutionId }).OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_p15_grade_band_plan");
        });
        b.Entity<StudentCurriculumAssignment>(e =>
        {
            e.ToTable("student_curriculum_assignments");
            e.HasKey(x => x.Id); e.Property(x => x.Id).ValueGeneratedNever();
            e.HasIndex(x => new { x.InstitutionId, x.StudentId }).IsUnique()
                .HasDatabaseName("IX_p15_student_assignment_unique");
            e.HasOne<CurriculumPlan>().WithMany().HasForeignKey(x => new { x.PlanId, x.InstitutionId })
                .HasPrincipalKey(x => new { x.Id, x.InstitutionId }).OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_p15_assignment_plan");
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.StudentId)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_p15_assignment_student");
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.AssignedById)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_p15_assignment_actor");
        });
    }
}
