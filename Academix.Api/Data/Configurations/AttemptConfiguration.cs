using Academix.Api.Domain.Examinations;
using Academix.Api.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Academix.Api.Data;

public static class AttemptConfiguration
{
    public static void Apply(ModelBuilder b)
    {
        b.Entity<ExamAttempt>(e =>
        {
            e.ToTable("exam_attempts", t =>
            {
                t.HasCheckConstraint("CK_exam_attempts_number", "\"AttemptNumber\" BETWEEN 1 AND 10");
                t.HasCheckConstraint("CK_exam_attempts_status", "\"Status\" IN ('InProgress','Submitted','Expired')");
                t.HasCheckConstraint("CK_exam_attempts_deadline", "\"DeadlineUtc\" > \"StartedAtUtc\"");
            });
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.HasAlternateKey(x => new { x.Id, x.ExamId, x.InstitutionId, x.CourseId });
            e.HasIndex(x => new { x.ExamId, x.StudentId, x.AttemptNumber }).IsUnique();
            e.HasIndex(x => new { x.StudentId, x.ExamId });
            e.Property(x => x.Status).HasMaxLength(20).IsRequired();
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasOne<ExamDefinition>().WithMany()
                .HasForeignKey(x => new { x.ExamId, x.InstitutionId, x.CourseId })
                .HasPrincipalKey(x => new { x.Id, x.InstitutionId, x.CourseId })
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_exam_attempts_exam");
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.StudentId)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_exam_attempts_student");
        });
        b.Entity<ExamAttemptQuestion>(e =>
        {
            e.ToTable("exam_attempt_questions", t =>
                t.HasCheckConstraint("CK_exam_attempt_questions_position", "\"Position\" BETWEEN 1 AND 200"));
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.HasAlternateKey(x => new { x.AttemptId, x.PublishedQuestionId });
            e.HasIndex(x => new { x.AttemptId, x.Position }).IsUnique();
            e.HasOne<ExamAttempt>().WithMany()
                .HasForeignKey(x => new { x.AttemptId, x.ExamId, x.InstitutionId, x.CourseId })
                .HasPrincipalKey(x => new { x.Id, x.ExamId, x.InstitutionId, x.CourseId })
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_exam_attempt_questions_attempt");
            e.HasOne<ExamPublishedQuestion>().WithMany().HasForeignKey(x => x.PublishedQuestionId)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_exam_attempt_questions_published");
        });
        b.Entity<ExamAttemptAnswer>(e =>
        {
            e.ToTable("exam_attempt_answers", t =>
                t.HasCheckConstraint("CK_exam_attempt_answers_sequence", "\"ClientSequence\" BETWEEN 1 AND 1000000000"));
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.HasIndex(x => new { x.AttemptId, x.PublishedQuestionId }).IsUnique();
            e.Property(x => x.AnswerJson).HasColumnType("jsonb").IsRequired();
            e.HasOne<ExamAttempt>().WithMany()
                .HasForeignKey(x => new { x.AttemptId, x.ExamId, x.InstitutionId, x.CourseId })
                .HasPrincipalKey(x => new { x.Id, x.ExamId, x.InstitutionId, x.CourseId })
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_exam_attempt_answers_attempt");
            e.HasOne<ExamAttemptQuestion>().WithMany()
                .HasForeignKey(x => new { x.AttemptId, x.PublishedQuestionId })
                .HasPrincipalKey(x => new { x.AttemptId, x.PublishedQuestionId })
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_exam_attempt_answers_question");
        });
    }
}
