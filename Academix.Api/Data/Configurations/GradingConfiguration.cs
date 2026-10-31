using Academix.Api.Domain.Examinations;
using Academix.Api.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Academix.Api.Data;

public static class GradingConfiguration
{
    public static void Apply(ModelBuilder b)
    {
        b.Entity<ExamGradeReport>(e =>
        {
            e.ToTable("exam_grade_reports", t =>
            {
                t.HasCheckConstraint("CK_remediation_score_version", "\"ScoreVersion\" > 0");
                t.HasCheckConstraint("CK_grade_report_status", "\"Status\" IN ('Draft','Published')");
                t.HasCheckConstraint("CK_grade_report_points", "\"EarnedPoints\" >= 0 AND \"MaximumPoints\" >= \"EarnedPoints\"");
            });
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.HasAlternateKey(x => new { x.Id, x.AttemptId });
            e.HasIndex(x => x.AttemptId).IsUnique();
            e.HasIndex(x => new { x.ExamId, x.Status });
            e.Property(x => x.Status).HasMaxLength(20).IsRequired();
            e.Property(x => x.Version).IsConcurrencyToken();
            e.Property(x => x.MaximumPoints).HasPrecision(10, 2);
            e.Property(x => x.EarnedPoints).HasPrecision(10, 2);
            e.HasOne<ExamAttempt>().WithMany()
                .HasForeignKey(x => new { x.AttemptId, x.ExamId, x.InstitutionId, x.CourseId })
                .HasPrincipalKey(x => new { x.Id, x.ExamId, x.InstitutionId, x.CourseId })
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_grade_reports_attempt");
        });
        b.Entity<ExamGradeItem>(e =>
        {
            e.ToTable("exam_grade_items", t =>
            {
                t.HasCheckConstraint("CK_grade_item_state", "\"State\" IN ('PendingManual','Auto','Manual')");
                t.HasCheckConstraint("CK_grade_item_points", "\"MaximumPoints\" >= 0 AND (\"EarnedPoints\" IS NULL OR (\"EarnedPoints\" >= 0 AND \"EarnedPoints\" <= \"MaximumPoints\"))");
            });
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.HasIndex(x => new { x.ReportId, x.PublishedQuestionId }).IsUnique();
            e.Property(x => x.State).HasMaxLength(20).IsRequired();
            e.Property(x => x.Feedback).HasMaxLength(4000).IsRequired();
            e.Property(x => x.MaximumPoints).HasPrecision(8, 2);
            e.Property(x => x.EarnedPoints).HasPrecision(8, 2);
            e.HasOne<ExamGradeReport>().WithMany()
                .HasForeignKey(x => new { x.ReportId, x.AttemptId })
                .HasPrincipalKey(x => new { x.Id, x.AttemptId })
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_grade_items_report");
            e.HasOne<ExamAttemptQuestion>().WithMany()
                .HasForeignKey(x => new { x.AttemptId, x.PublishedQuestionId })
                .HasPrincipalKey(x => new { x.AttemptId, x.PublishedQuestionId })
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_grade_items_attempt_question");
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.GradedBy)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_grade_items_grader");
        });
        b.Entity<ExamGradeAudit>(e =>
        {
            e.ToTable("exam_grade_audits");
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.HasIndex(x => new { x.ReportId, x.AtUtc });
            e.Property(x => x.Action).HasMaxLength(50).IsRequired();
            e.Property(x => x.Reason).HasMaxLength(2000).IsRequired();
            e.Property(x => x.PreviousPoints).HasPrecision(8, 2);
            e.Property(x => x.NewPoints).HasPrecision(8, 2);
            e.HasOne<ExamGradeReport>().WithMany().HasForeignKey(x => x.ReportId)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_grade_audits_report");
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ActorId)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_grade_audits_actor");
        });
        b.Entity<ExamGradeAppeal>(e =>
        {
            e.ToTable("exam_grade_appeals", t =>
                t.HasCheckConstraint("CK_grade_appeal_status", "\"Status\" IN ('Open','Accepted','Rejected')"));
            e.HasKey(x => x.Id);
            e.Property(x => x.Id).ValueGeneratedNever();
            e.HasIndex(x => x.ReportId).IsUnique();
            e.Property(x => x.Message).HasMaxLength(2000).IsRequired();
            e.Property(x => x.Status).HasMaxLength(20).IsRequired();
            e.Property(x => x.Resolution).HasMaxLength(2000).IsRequired();
            e.HasOne<ExamGradeReport>().WithMany().HasForeignKey(x => x.ReportId)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_grade_appeals_report");
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.StudentId)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_grade_appeals_student");
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ResolvedBy)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_grade_appeals_reviewer");
        });
    }
}
