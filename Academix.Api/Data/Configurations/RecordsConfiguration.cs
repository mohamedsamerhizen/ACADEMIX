using Academix.Api.Domain.Academic;
using Academix.Api.Domain.Courses;
using Academix.Api.Domain.Identity;
using Academix.Api.Domain.Records;
using Microsoft.EntityFrameworkCore;

namespace Academix.Api.Data;

public static class RecordsConfiguration
{
    public static void Apply(ModelBuilder b)
    {
        b.Entity<StudentProgramMembership>(e =>
        {
            e.ToTable("student_program_memberships", t => t.HasCheckConstraint("CK_p14_membership_status",
                "\"Status\" IN ('Active','Completed')"));
            e.HasKey(x => x.Id); e.Property(x => x.Id).ValueGeneratedNever();
            e.HasIndex(x => new { x.InstitutionId, x.StudentId }).IsUnique().HasDatabaseName("IX_p14_membership_student");
            e.Property(x => x.Status).HasMaxLength(20).IsRequired();
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasOne<AcademicProgram>().WithMany().HasForeignKey(x => new { x.ProgramId, x.InstitutionId })
                .HasPrincipalKey(x => new { x.Id, x.InstitutionId }).OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_p14_membership_program");
            e.HasOne<AcademicTerm>().WithMany().HasForeignKey(x => new { x.AdmissionTermId, x.InstitutionId })
                .HasPrincipalKey(x => new { x.Id, x.InstitutionId }).OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_p14_membership_admission_term");
            e.HasOne<AcademicTerm>().WithMany().HasForeignKey(x => new { x.CurrentTermId, x.InstitutionId })
                .HasPrincipalKey(x => new { x.Id, x.InstitutionId }).OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_p14_membership_current_term");
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.StudentId)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_p14_membership_student");
        });
        b.Entity<CoursePrerequisite>(e =>
        {
            e.ToTable("course_prerequisites", t => t.HasCheckConstraint("CK_p14_prereq_not_self",
                "\"CourseId\" <> \"RequiredCourseId\""));
            e.HasKey(x => x.Id); e.Property(x => x.Id).ValueGeneratedNever();
            e.HasIndex(x => new { x.InstitutionId, x.CourseId, x.RequiredCourseId }).IsUnique()
                .HasDatabaseName("IX_p14_prerequisite_unique");
            e.Property(x => x.MinimumPercentage).HasPrecision(5, 2);
            e.HasOne<Course>().WithMany().HasForeignKey(x => new { x.CourseId, x.InstitutionId })
                .HasPrincipalKey(x => new { x.Id, x.InstitutionId }).OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_p14_prereq_course");
            e.HasOne<Course>().WithMany().HasForeignKey(x => new { x.RequiredCourseId, x.InstitutionId })
                .HasPrincipalKey(x => new { x.Id, x.InstitutionId }).OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_p14_prereq_required_course");
        });
        b.Entity<StudentTranscriptEntry>(e =>
        {
            e.ToTable("student_transcript_entries");
            e.HasKey(x => x.Id); e.Property(x => x.Id).ValueGeneratedNever();
            e.HasIndex(x => new { x.InstitutionId, x.SectionId, x.StudentId }).IsUnique()
                .HasDatabaseName("IX_p14_transcript_unique");
            e.HasIndex(x => new { x.InstitutionId, x.StudentId, x.TermId }).HasDatabaseName("IX_p14_transcript_student_term");
            e.Property(x => x.CourseCode).HasMaxLength(30).IsRequired();
            e.Property(x => x.CourseTitle).HasMaxLength(200).IsRequired();
            e.Property(x => x.Letter).HasMaxLength(10).IsRequired();
            e.Property(x => x.Percentage).HasPrecision(5, 2);
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasOne<CourseSection>().WithMany().HasForeignKey(x => new { x.SectionId, x.InstitutionId })
                .HasPrincipalKey(x => new { x.Id, x.InstitutionId }).OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_p14_transcript_section");
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.StudentId)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_p14_transcript_student");
        });
        b.Entity<StudentTermStanding>(e =>
        {
            e.ToTable("student_term_standings", t => t.HasCheckConstraint("CK_p14_standing_status",
                "\"Status\" IN ('InsufficientData','Probation','AtRisk','GoodStanding')"));
            e.HasKey(x => x.Id); e.Property(x => x.Id).ValueGeneratedNever();
            e.HasIndex(x => new { x.InstitutionId, x.StudentId, x.TermId }).IsUnique()
                .HasDatabaseName("IX_p14_standing_unique");
            e.Property(x => x.Status).HasMaxLength(30).IsRequired();
            e.Property(x => x.AveragePercentage).HasPrecision(5, 2);
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasOne<AcademicTerm>().WithMany().HasForeignKey(x => new { x.TermId, x.InstitutionId })
                .HasPrincipalKey(x => new { x.Id, x.InstitutionId }).OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_p14_standing_term");
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.StudentId)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_p14_standing_student");
        });
        b.Entity<StudentLifecycleEvent>(e =>
        {
            e.ToTable("student_lifecycle_events");
            e.HasKey(x => x.Id); e.Property(x => x.Id).ValueGeneratedNever();
            e.HasIndex(x => new { x.InstitutionId, x.AtUtc }).HasDatabaseName("IX_p14_event_institution");
            e.HasIndex(x => new { x.InstitutionId, x.StudentId, x.AtUtc }).HasDatabaseName("IX_p14_event_student");
            e.Property(x => x.Kind).HasMaxLength(80).IsRequired();
            e.Property(x => x.Detail).HasMaxLength(800).IsRequired();
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.StudentId)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_p14_event_student");
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ActorId)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_p14_event_actor");
        });
    }
}
