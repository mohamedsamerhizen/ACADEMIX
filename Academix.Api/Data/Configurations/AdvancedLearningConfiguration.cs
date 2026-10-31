using Academix.Api.Domain.Academic;
using Academix.Api.Domain.AdvancedLearning;
using Academix.Api.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Academix.Api.Data;

public static class AdvancedLearningConfiguration
{
    public static void Apply(ModelBuilder b)
    {
        b.Entity<DiscussionTopic>(e =>
        {
            e.ToTable("lms_discussion_topics"); e.HasKey(x => x.Id);
            e.HasAlternateKey(x => new { x.Id, x.SectionId, x.InstitutionId });
            e.HasIndex(x => new { x.SectionId, x.CreatedAtUtc });
            e.Property(x => x.Title).HasMaxLength(200).IsRequired();
            e.Property(x => x.Body).HasMaxLength(10000).IsRequired();
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasOne<CourseSection>().WithMany().HasForeignKey(x => new { x.SectionId, x.InstitutionId })
                .HasPrincipalKey(x => new { x.Id, x.InstitutionId }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_p8_topic_section");
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_p8_topic_creator");
        });
        b.Entity<DiscussionPost>(e =>
        {
            e.ToTable("lms_discussion_posts"); e.HasKey(x => x.Id);
            e.HasAlternateKey(x => new { x.Id, x.TopicId });
            e.HasIndex(x => new { x.TopicId, x.CreatedAtUtc });
            e.Property(x => x.Body).HasMaxLength(10000).IsRequired();
            e.HasOne<DiscussionTopic>().WithMany().HasForeignKey(x => new { x.TopicId, x.SectionId, x.InstitutionId })
                .HasPrincipalKey(x => new { x.Id, x.SectionId, x.InstitutionId }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_p8_post_topic");
            e.HasOne<DiscussionPost>().WithMany().HasForeignKey(x => new { x.ParentId, x.TopicId })
                .HasPrincipalKey(x => new { x.Id, x.TopicId }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_p8_post_parent");
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.AuthorId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_p8_post_author");
        });
        b.Entity<ProjectTeam>(e =>
        {
            e.ToTable("lms_project_teams"); e.HasKey(x => x.Id);
            e.HasAlternateKey(x => new { x.Id, x.SectionId, x.InstitutionId });
            e.HasIndex(x => new { x.SectionId, x.Name }).IsUnique();
            e.Property(x => x.Name).HasMaxLength(120).IsRequired();
            e.HasOne<CourseSection>().WithMany().HasForeignKey(x => new { x.SectionId, x.InstitutionId })
                .HasPrincipalKey(x => new { x.Id, x.InstitutionId }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_p8_team_section");
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_p8_team_creator");
        });
        b.Entity<ProjectTeamMember>(e =>
        {
            e.ToTable("lms_project_team_members"); e.HasKey(x => new { x.TeamId, x.StudentId });
            e.HasIndex(x => new { x.SectionId, x.StudentId }).IsUnique();
            e.HasOne<ProjectTeam>().WithMany().HasForeignKey(x => new { x.TeamId, x.SectionId, x.InstitutionId })
                .HasPrincipalKey(x => new { x.Id, x.SectionId, x.InstitutionId }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_p8_member_team");
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_p8_member_student");
        });
        b.Entity<GradingRubric>(e =>
        {
            e.ToTable("lms_grading_rubrics"); e.HasKey(x => x.Id);
            e.HasAlternateKey(x => new { x.Id, x.SectionId, x.InstitutionId });
            e.Property(x => x.Name).HasMaxLength(160).IsRequired();
            e.HasOne<CourseSection>().WithMany().HasForeignKey(x => new { x.SectionId, x.InstitutionId })
                .HasPrincipalKey(x => new { x.Id, x.InstitutionId }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_p8_rubric_section");
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_p8_rubric_creator");
        });
        b.Entity<RubricCriterion>(e =>
        {
            e.ToTable("lms_rubric_criteria"); e.HasKey(x => x.Id);
            e.HasAlternateKey(x => new { x.Id, x.RubricId });
            e.HasIndex(x => new { x.RubricId, x.Position }).IsUnique();
            e.Property(x => x.Name).HasMaxLength(160).IsRequired();
            e.Property(x => x.Description).HasMaxLength(2000).IsRequired();
            e.Property(x => x.MaxPoints).HasPrecision(10, 2);
            e.HasOne<GradingRubric>().WithMany().HasForeignKey(x => new { x.RubricId, x.SectionId, x.InstitutionId })
                .HasPrincipalKey(x => new { x.Id, x.SectionId, x.InstitutionId }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_p8_criterion_rubric");
        });
        b.Entity<GroupProject>(e =>
        {
            e.ToTable("lms_group_projects"); e.HasKey(x => x.Id);
            e.HasAlternateKey(x => new { x.Id, x.SectionId, x.InstitutionId });
            e.HasIndex(x => new { x.SectionId, x.DueAtUtc });
            e.Property(x => x.Title).HasMaxLength(200).IsRequired();
            e.Property(x => x.Instructions).HasMaxLength(20000).IsRequired();
            e.Property(x => x.MaxScore).HasPrecision(10, 2);
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasOne<CourseSection>().WithMany().HasForeignKey(x => new { x.SectionId, x.InstitutionId })
                .HasPrincipalKey(x => new { x.Id, x.InstitutionId }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_p8_project_section");
            e.HasOne<GradingRubric>().WithMany().HasForeignKey(x => new { x.RubricId, x.SectionId, x.InstitutionId })
                .HasPrincipalKey(x => new { x.Id, x.SectionId, x.InstitutionId }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_p8_project_rubric");
        });
        b.Entity<GroupProjectSubmission>(e =>
        {
            e.ToTable("lms_group_submissions"); e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.ProjectId, x.TeamId }).IsUnique();
            e.Property(x => x.Content).HasMaxLength(20000).IsRequired();
            e.Property(x => x.Feedback).HasMaxLength(5000).IsRequired();
            e.Property(x => x.Status).HasMaxLength(20).IsRequired();
            e.Property(x => x.Score).HasPrecision(10, 2);
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasOne<GroupProject>().WithMany().HasForeignKey(x => new { x.ProjectId, x.SectionId, x.InstitutionId })
                .HasPrincipalKey(x => new { x.Id, x.SectionId, x.InstitutionId }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_p8_submission_project");
            e.HasOne<ProjectTeam>().WithMany().HasForeignKey(x => new { x.TeamId, x.SectionId, x.InstitutionId })
                .HasPrincipalKey(x => new { x.Id, x.SectionId, x.InstitutionId }).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_p8_submission_team");
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.LastEditedBy).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_p8_submission_editor");
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.GradedBy).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_p8_submission_grader");
        });
        b.Entity<RubricCriterionScore>(e =>
        {
            e.ToTable("lms_rubric_scores"); e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.SubmissionId, x.CriterionId }).IsUnique();
            e.Property(x => x.Points).HasPrecision(10, 2);
            e.Property(x => x.Comment).HasMaxLength(2000).IsRequired();
            e.HasOne<GroupProjectSubmission>().WithMany().HasForeignKey(x => x.SubmissionId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_p8_score_submission");
            e.HasOne<RubricCriterion>().WithMany().HasForeignKey(x => x.CriterionId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_p8_score_criterion");
        });
        b.Entity<ProjectGradeAudit>(e =>
        {
            e.ToTable("lms_project_grade_audit"); e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.SubmissionId, x.AtUtc });
            e.Property(x => x.PreviousScore).HasPrecision(10, 2);
            e.Property(x => x.NewScore).HasPrecision(10, 2);
            e.Property(x => x.Reason).HasMaxLength(1000).IsRequired();
            e.HasOne<GroupProjectSubmission>().WithMany().HasForeignKey(x => x.SubmissionId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_p8_audit_submission");
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ActorId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_p8_audit_actor");
        });
    }
}
