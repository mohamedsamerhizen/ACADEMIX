using System;
using Microsoft.EntityFrameworkCore;

#nullable disable

namespace Academix.Api.Data.Migrations;

public partial class InitialCreate
{
    protected override void BuildTargetModel(ModelBuilder modelBuilder)
    {
        modelBuilder.HasAnnotation("ProductVersion", "10.0.3");

        modelBuilder.Entity("Academix.Api.Domain.Courses.Course", b =>
        {
            b.Property<Guid>("Id").ValueGeneratedNever().HasColumnType("uuid").HasColumnName("id");
            b.Property<string>("Code").IsRequired().HasMaxLength(20)
                .HasColumnType("character varying(20)").HasColumnName("code");
            b.Property<string>("Title").IsRequired().HasMaxLength(200)
                .HasColumnType("character varying(200)").HasColumnName("title");
            b.Property<bool>("IsActive").HasColumnType("boolean").HasColumnName("is_active");
            b.HasKey("Id").HasName("pk_courses");
            b.HasIndex("Code").IsUnique().HasDatabaseName("ux_courses_code");
            b.ToTable("courses");
            b.HasData(
                new { Id = new Guid("8b217247-2c98-4b79-ac2e-bbe086a2ad11"), Code = "CS101", Title = "Introduction to Computer Science", IsActive = true },
                new { Id = new Guid("fe9ed719-2e94-498a-988c-4c8e2450c2d2"), Code = "MATH101", Title = "Calculus I", IsActive = true });
        });
    }
}
