using Academix.Api.Domain.Academic;
using Academix.Api.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Academix.Api.Data;

public static class AcademicConfiguration
{
    public static void Apply(ModelBuilder b)
    {
        b.Entity<Institution>(e =>
        {
            e.ToTable("institutions"); e.HasKey(x => x.Id);
            e.HasIndex(x => x.Code).IsUnique();
            e.Property(x => x.Code).HasMaxLength(30).IsRequired();
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Version).IsConcurrencyToken();
        });
        b.Entity<Faculty>(e =>
        {
            e.ToTable("faculties"); e.HasKey(x => x.Id);
            e.HasAlternateKey(x => new { x.Id, x.InstitutionId });
            e.HasIndex(x => new { x.InstitutionId, x.Code }).IsUnique();
            e.Property(x => x.Code).HasMaxLength(30).IsRequired(); e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasOne<Institution>().WithMany().HasForeignKey(x => x.InstitutionId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<Department>(e =>
        {
            e.ToTable("departments"); e.HasKey(x => x.Id);
            e.HasAlternateKey(x => new { x.Id, x.InstitutionId });
            e.HasIndex(x => new { x.InstitutionId, x.Code }).IsUnique();
            e.Property(x => x.Code).HasMaxLength(30).IsRequired(); e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasOne<Institution>().WithMany().HasForeignKey(x => x.InstitutionId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Faculty>().WithMany().HasForeignKey(x => new { x.FacultyId, x.InstitutionId }).HasPrincipalKey(x => new { x.Id, x.InstitutionId }).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<AcademicProgram>(e =>
        {
            e.ToTable("academic_programs"); e.HasKey(x => x.Id);
            e.HasAlternateKey(x => new { x.Id, x.InstitutionId });
            e.HasIndex(x => new { x.InstitutionId, x.Code }).IsUnique();
            e.Property(x => x.Code).HasMaxLength(30).IsRequired(); e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasOne<Department>().WithMany().HasForeignKey(x => new { x.DepartmentId, x.InstitutionId }).HasPrincipalKey(x => new { x.Id, x.InstitutionId }).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<AcademicTerm>(e =>
        {
            e.ToTable("academic_terms"); e.HasKey(x => x.Id);
            e.HasAlternateKey(x => new { x.Id, x.InstitutionId });
            e.HasIndex(x => new { x.InstitutionId, x.Code }).IsUnique();
            e.Property(x => x.Code).HasMaxLength(30).IsRequired(); e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Status).HasMaxLength(20).IsRequired();
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasOne<Institution>().WithMany().HasForeignKey(x => x.InstitutionId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<ProgramCourse>(e =>
        {
            e.ToTable("program_courses"); e.HasKey(x => new { x.ProgramId, x.CourseId });
            e.HasOne<AcademicProgram>().WithMany().HasForeignKey(x => new { x.ProgramId, x.InstitutionId }).HasPrincipalKey(x => new { x.Id, x.InstitutionId }).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<Academix.Api.Domain.Courses.Course>().WithMany().HasForeignKey(x => new { x.CourseId, x.InstitutionId }).HasPrincipalKey(x => new { x.Id, x.InstitutionId }).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<CourseSection>(e =>
        {
            e.ToTable("course_sections"); e.HasKey(x => x.Id);
            e.HasAlternateKey(x => new { x.Id, x.InstitutionId });
            e.HasIndex(x => new { x.InstitutionId, x.CourseId, x.TermId, x.Code }).IsUnique();
            e.Property(x => x.Code).HasMaxLength(30).IsRequired(); e.Property(x => x.Status).HasMaxLength(20).IsRequired();
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasOne<Academix.Api.Domain.Courses.Course>().WithMany().HasForeignKey(x => new { x.CourseId, x.InstitutionId }).HasPrincipalKey(x => new { x.Id, x.InstitutionId }).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<AcademicTerm>().WithMany().HasForeignKey(x => new { x.TermId, x.InstitutionId }).HasPrincipalKey(x => new { x.Id, x.InstitutionId }).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<TeachingAssignment>(e =>
        {
            e.ToTable("teaching_assignments"); e.HasKey(x => new { x.SectionId, x.UserId });
            e.Property(x => x.Kind).HasMaxLength(30).IsRequired();
            e.HasIndex(x => new { x.SectionId, x.Kind }).IsUnique().HasFilter("\"Kind\" = 'Instructor'");
            e.HasOne<CourseSection>().WithMany().HasForeignKey(x => new { x.SectionId, x.InstitutionId }).HasPrincipalKey(x => new { x.Id, x.InstitutionId }).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<StudentEnrollment>(e =>
        {
            e.ToTable("student_enrollments"); e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.SectionId, x.StudentId }).IsUnique();
            e.HasIndex(x => new { x.StudentId, x.Status });
            e.Property(x => x.Status).HasMaxLength(20).IsRequired();
            e.HasOne<CourseSection>().WithMany().HasForeignKey(x => new { x.SectionId, x.InstitutionId }).HasPrincipalKey(x => new { x.Id, x.InstitutionId }).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
