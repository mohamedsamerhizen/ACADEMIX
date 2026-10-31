using Academix.Api.Domain.Courses;
using Academix.Api.Domain.Examinations;
using Academix.Api.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Academix.Api.Data;

public static class ExaminationConfiguration
{
    public static void Apply(ModelBuilder b)
    {
        b.Entity<ExamDefinition>(e =>
        {
            e.ToTable("exam_definitions");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.HasAlternateKey(x => new { x.Id, x.InstitutionId, x.CourseId });
            e.HasIndex(x => new { x.InstitutionId, x.SectionId, x.Status });
            e.Property(x => x.Title).HasMaxLength(200).IsRequired();
            e.Property(x => x.Description).HasMaxLength(2000).IsRequired();
            e.Property(x => x.Status).HasMaxLength(20).IsRequired();
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasOne<Academix.Api.Domain.Academic.CourseSection>().WithMany()
                .HasForeignKey(x => new { x.SectionId, x.InstitutionId })
                .HasPrincipalKey(x => new { x.Id, x.InstitutionId }).OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_exam_definitions_section");
            e.HasOne<Course>().WithMany()
                .HasForeignKey(x => new { x.CourseId, x.InstitutionId })
                .HasPrincipalKey(x => new { x.Id, x.InstitutionId }).OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_exam_definitions_course");
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.CreatedBy)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_exam_definitions_creator");
        });
        b.Entity<ExamQuestionPool>(e =>
        {
            e.ToTable("exam_question_pools", table =>
            {
                table.HasCheckConstraint("CK_exam_question_pools_quantity", "\"Quantity\" BETWEEN 1 AND 200");
                table.HasCheckConstraint("CK_exam_question_pools_points", "\"Points\" > 0 AND \"Points\" <= 1000");
            });
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.HasIndex(x => new { x.ExamId, x.Position }).IsUnique();
            e.Property(x => x.Kind).HasMaxLength(20).IsRequired();
            e.Property(x => x.Difficulty).HasMaxLength(20);
            e.Property(x => x.Points).HasPrecision(8, 2);
            e.HasOne<ExamDefinition>().WithMany()
                .HasForeignKey(x => new { x.ExamId, x.InstitutionId, x.CourseId })
                .HasPrincipalKey(x => new { x.Id, x.InstitutionId, x.CourseId })
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_exam_question_pools_exam");
        });
        b.Entity<ExamDraftQuestion>(e =>
        {
            e.ToTable("exam_draft_questions");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.HasIndex(x => new { x.ExamId, x.Position }).IsUnique();
            e.Property(x => x.Points).HasPrecision(8, 2);
            e.HasOne<ExamDefinition>().WithMany()
                .HasForeignKey(x => new { x.ExamId, x.InstitutionId, x.CourseId })
                .HasPrincipalKey(x => new { x.Id, x.InstitutionId, x.CourseId })
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_exam_draft_questions_exam");
            e.HasOne<BankQuestion>().WithMany()
                .HasForeignKey(x => new { x.QuestionId, x.InstitutionId, x.CourseId })
                .HasPrincipalKey(x => new { x.Id, x.InstitutionId, x.CourseId })
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_exam_draft_questions_bank");
        });
        b.Entity<ExamPublishedQuestion>(e =>
        {
            e.ToTable("exam_published_questions");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.HasIndex(x => new { x.ExamId, x.Position }).IsUnique();
            e.Property(x => x.Points).HasPrecision(8, 2);
            e.Property(x => x.BodyJson).HasColumnType("jsonb").IsRequired();
            e.Property(x => x.AnswerKeyJson).HasColumnType("jsonb").IsRequired();
            e.HasOne<ExamDefinition>().WithMany()
                .HasForeignKey(x => new { x.ExamId, x.InstitutionId, x.CourseId })
                .HasPrincipalKey(x => new { x.Id, x.InstitutionId, x.CourseId })
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_exam_published_questions_exam");
            e.HasOne<BankQuestion>().WithMany()
                .HasForeignKey(x => new { x.QuestionId, x.InstitutionId, x.CourseId })
                .HasPrincipalKey(x => new { x.Id, x.InstitutionId, x.CourseId })
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_exam_published_questions_bank");
        });
        b.Entity<BankQuestion>(e =>
        {
            e.ToTable("exam_bank_questions");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.HasAlternateKey(x => new { x.Id, x.InstitutionId, x.CourseId });
            e.HasIndex(x => new { x.InstitutionId, x.CourseId, x.IsArchived });
            e.Property(x => x.Kind).HasMaxLength(20).IsRequired();
            e.Property(x => x.Difficulty).HasMaxLength(20).IsRequired();
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasOne<Course>().WithMany().HasForeignKey(x => new { x.CourseId, x.InstitutionId })
                .HasPrincipalKey(x => new { x.Id, x.InstitutionId }).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);
        });
        b.Entity<BankQuestionRevision>(e =>
        {
            e.ToTable("exam_bank_revisions");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.HasIndex(x => new { x.QuestionId, x.RevisionNumber }).IsUnique();
            e.Property(x => x.BodyJson).HasColumnType("jsonb").IsRequired();
            e.Property(x => x.AnswerKeyJson).HasColumnType("jsonb").IsRequired();
            e.HasOne<BankQuestion>().WithMany().HasForeignKey(x => new { x.QuestionId, x.InstitutionId, x.CourseId })
                .HasPrincipalKey(x => new { x.Id, x.InstitutionId, x.CourseId })
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_exam_bank_revisions_question_scope");
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);
        });
    }
}
