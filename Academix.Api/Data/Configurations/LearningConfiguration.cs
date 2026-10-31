using Academix.Api.Domain.Academic;
using Academix.Api.Domain.Identity;
using Academix.Api.Domain.Learning;
using Microsoft.EntityFrameworkCore;

namespace Academix.Api.Data;

public static class LearningConfiguration
{
    public static void Apply(ModelBuilder b)
    {
        b.Entity<LearningUnit>(e =>
        {
            e.ToTable("lms_units"); e.HasKey(x => x.Id);
            e.HasAlternateKey(x => new { x.Id, x.SectionId });
            e.HasIndex(x => new { x.SectionId, x.Position }).IsUnique();
            e.Property(x => x.Title).HasMaxLength(200).IsRequired();
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasOne<CourseSection>().WithMany().HasForeignKey(x => new { x.SectionId, x.InstitutionId })
                .HasPrincipalKey(x => new { x.Id, x.InstitutionId }).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<LearningLesson>(e =>
        {
            e.ToTable("lms_lessons"); e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.UnitId, x.Position }).IsUnique();
            e.Property(x => x.Title).HasMaxLength(200).IsRequired();
            e.Property(x => x.Body).HasMaxLength(20000).IsRequired();
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasOne<LearningUnit>().WithMany().HasForeignKey(x => new { x.UnitId, x.SectionId })
                .HasPrincipalKey(x => new { x.Id, x.SectionId }).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<LearningAnnouncement>(e =>
        {
            e.ToTable("lms_announcements"); e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.SectionId, x.PublishedAtUtc });
            e.Property(x => x.Title).HasMaxLength(200).IsRequired();
            e.Property(x => x.Body).HasMaxLength(10000).IsRequired();
            e.HasOne<CourseSection>().WithMany().HasForeignKey(x => new { x.SectionId, x.InstitutionId })
                .HasPrincipalKey(x => new { x.Id, x.InstitutionId }).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<LearningAssignment>(e =>
        {
            e.ToTable("lms_assignments"); e.HasKey(x => x.Id);
            e.HasAlternateKey(x => new { x.Id, x.SectionId });
            e.Property(x => x.Title).HasMaxLength(200).IsRequired();
            e.Property(x => x.Instructions).HasMaxLength(20000).IsRequired();
            e.Property(x => x.MaxScore).HasPrecision(10, 2);
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasOne<CourseSection>().WithMany().HasForeignKey(x => new { x.SectionId, x.InstitutionId })
                .HasPrincipalKey(x => new { x.Id, x.InstitutionId }).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<LearningSubmission>(e =>
        {
            e.ToTable("lms_submissions"); e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.AssignmentId, x.StudentId }).IsUnique();
            e.Property(x => x.Content).HasMaxLength(20000).IsRequired();
            e.Property(x => x.Status).HasMaxLength(20).IsRequired();
            e.Property(x => x.Feedback).HasMaxLength(5000);
            e.Property(x => x.Score).HasPrecision(10, 2);
            e.HasOne<LearningAssignment>().WithMany().HasForeignKey(x => new { x.AssignmentId, x.SectionId })
                .HasPrincipalKey(x => new { x.Id, x.SectionId }).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<LearningFile>().WithMany().HasForeignKey(x => x.AttachmentId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.GradedBy).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<LessonCompletion>(e =>
        {
            e.ToTable("lms_lesson_completions"); e.HasKey(x => new { x.LessonId, x.StudentId });
            e.HasIndex(x => new { x.SectionId, x.StudentId });
            e.HasOne<LearningLesson>().WithMany().HasForeignKey(x => x.LessonId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<LearningFile>(e =>
        {
            e.ToTable("lms_files"); e.HasKey(x => x.Id);
            e.HasIndex(x => x.ObjectKey).IsUnique();
            e.Property(x => x.FileName).HasMaxLength(255).IsRequired();
            e.Property(x => x.ContentType).HasMaxLength(100).IsRequired();
            e.Property(x => x.ObjectKey).HasMaxLength(250).IsRequired();
            e.HasOne<CourseSection>().WithMany().HasForeignKey(x => new { x.SectionId, x.InstitutionId })
                .HasPrincipalKey(x => new { x.Id, x.InstitutionId }).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UploadedBy).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
