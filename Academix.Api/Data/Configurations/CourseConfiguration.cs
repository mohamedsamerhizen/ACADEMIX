using Academix.Api.Domain.Courses;
using Academix.Api.Domain.Academic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Academix.Api.Data;

public sealed class CourseConfiguration : IEntityTypeConfiguration<Course>
{
    public static readonly Guid Cs101Id = Guid.Parse("8b217247-2c98-4b79-ac2e-bbe086a2ad11");
    public static readonly Guid Math101Id = Guid.Parse("fe9ed719-2e94-498a-988c-4c8e2450c2d2");
    public void Configure(EntityTypeBuilder<Course> b)
    {
        b.ToTable("courses");
        b.HasKey(x => x.Id).HasName("pk_courses");
        b.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        b.Property(x => x.InstitutionId).HasColumnName("institution_id").HasDefaultValue(AcademicIds.InitialInstitution);
        b.Property(x => x.Code).HasColumnName("code").HasMaxLength(20).IsRequired();
        b.Property(x => x.Title).HasColumnName("title").HasMaxLength(200).IsRequired();
        b.Property(x => x.IsActive).HasColumnName("is_active");
        b.Property(x => x.Version).HasColumnName("version").IsConcurrencyToken();
        b.HasIndex(x => new { x.InstitutionId, x.Code }).IsUnique().HasDatabaseName("ux_courses_institution_code");
        b.HasAlternateKey(x => new { x.Id, x.InstitutionId });
        b.HasOne<Institution>().WithMany().HasForeignKey(x => x.InstitutionId).OnDelete(DeleteBehavior.Restrict);
        b.HasData(
            new { Id = Cs101Id, InstitutionId = AcademicIds.InitialInstitution, Code = "CS101", Title = "Introduction to Computer Science", IsActive = true, Version = 1L },
            new { Id = Math101Id, InstitutionId = AcademicIds.InitialInstitution, Code = "MATH101", Title = "Calculus I", IsActive = true, Version = 1L });
    }
}
