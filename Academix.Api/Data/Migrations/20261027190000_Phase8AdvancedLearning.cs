using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Academix.Api.Data.Migrations;

public partial class Phase8AdvancedLearning : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "lms_discussion_topics",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
                SectionId = table.Column<Guid>(type: "uuid", nullable: false),
                CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Body = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                IsLocked = table.Column<bool>(type: "boolean", nullable: false),
                IsPinned = table.Column<bool>(type: "boolean", nullable: false),
                Version = table.Column<long>(type: "bigint", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_lms_discussion_topics", x => x.Id);
                table.UniqueConstraint("AK_lms_discussion_topics_Id_SectionId_InstitutionId", x => new { x.Id, x.SectionId, x.InstitutionId });
                table.ForeignKey("FK_p8_topic_section", x => new { x.SectionId, x.InstitutionId }, "course_sections", new[] { "Id", "InstitutionId" }, onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_p8_topic_creator", x => x.CreatedBy, "identity_users", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(name: "IX_lms_discussion_topics_SectionId_CreatedAtUtc", table: "lms_discussion_topics", columns: new[] { "SectionId", "CreatedAtUtc" });
        migrationBuilder.CreateIndex(name: "IX_lms_discussion_topics_SectionId_InstitutionId", table: "lms_discussion_topics", columns: new[] { "SectionId", "InstitutionId" });
        migrationBuilder.CreateIndex(name: "IX_lms_discussion_topics_CreatedBy", table: "lms_discussion_topics", column: "CreatedBy");

        migrationBuilder.CreateTable(
            name: "lms_discussion_posts",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
                SectionId = table.Column<Guid>(type: "uuid", nullable: false),
                TopicId = table.Column<Guid>(type: "uuid", nullable: false),
                ParentId = table.Column<Guid?>(type: "uuid", nullable: true),
                AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
                Body = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                IsHidden = table.Column<bool>(type: "boolean", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_lms_discussion_posts", x => x.Id);
                table.UniqueConstraint("AK_lms_discussion_posts_Id_TopicId", x => new { x.Id, x.TopicId });
                table.ForeignKey("FK_p8_post_topic", x => new { x.TopicId, x.SectionId, x.InstitutionId }, "lms_discussion_topics", new[] { "Id", "SectionId", "InstitutionId" }, onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_p8_post_parent", x => new { x.ParentId, x.TopicId }, "lms_discussion_posts", new[] { "Id", "TopicId" }, onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_p8_post_author", x => x.AuthorId, "identity_users", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(name: "IX_lms_discussion_posts_TopicId_CreatedAtUtc", table: "lms_discussion_posts", columns: new[] { "TopicId", "CreatedAtUtc" });
        migrationBuilder.CreateIndex(name: "IX_lms_discussion_posts_TopicId_SectionId_InstitutionId", table: "lms_discussion_posts", columns: new[] { "TopicId", "SectionId", "InstitutionId" });
        migrationBuilder.CreateIndex(name: "IX_lms_discussion_posts_ParentId_TopicId", table: "lms_discussion_posts", columns: new[] { "ParentId", "TopicId" });
        migrationBuilder.CreateIndex(name: "IX_lms_discussion_posts_AuthorId", table: "lms_discussion_posts", column: "AuthorId");

        migrationBuilder.CreateTable(
            name: "lms_project_teams",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
                SectionId = table.Column<Guid>(type: "uuid", nullable: false),
                CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_lms_project_teams", x => x.Id);
                table.UniqueConstraint("AK_lms_project_teams_Id_SectionId_InstitutionId", x => new { x.Id, x.SectionId, x.InstitutionId });
                table.ForeignKey("FK_p8_team_section", x => new { x.SectionId, x.InstitutionId }, "course_sections", new[] { "Id", "InstitutionId" }, onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_p8_team_creator", x => x.CreatedBy, "identity_users", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(name: "IX_lms_project_teams_SectionId_Name", table: "lms_project_teams", columns: new[] { "SectionId", "Name" }, unique: true);
        migrationBuilder.CreateIndex(name: "IX_lms_project_teams_SectionId_InstitutionId", table: "lms_project_teams", columns: new[] { "SectionId", "InstitutionId" });
        migrationBuilder.CreateIndex(name: "IX_lms_project_teams_CreatedBy", table: "lms_project_teams", column: "CreatedBy");

        migrationBuilder.CreateTable(
            name: "lms_project_team_members",
            columns: table => new
            {
                TeamId = table.Column<Guid>(type: "uuid", nullable: false),
                InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
                SectionId = table.Column<Guid>(type: "uuid", nullable: false),
                StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                JoinedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_lms_project_team_members", x => new { x.TeamId, x.StudentId });
                table.ForeignKey("FK_p8_member_team", x => new { x.TeamId, x.SectionId, x.InstitutionId }, "lms_project_teams", new[] { "Id", "SectionId", "InstitutionId" }, onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_p8_member_student", x => x.StudentId, "identity_users", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(name: "IX_lms_project_team_members_SectionId_StudentId", table: "lms_project_team_members", columns: new[] { "SectionId", "StudentId" }, unique: true);
        migrationBuilder.CreateIndex(name: "IX_lms_project_team_members_TeamId_SectionId_InstitutionId", table: "lms_project_team_members", columns: new[] { "TeamId", "SectionId", "InstitutionId" });
        migrationBuilder.CreateIndex(name: "IX_lms_project_team_members_StudentId", table: "lms_project_team_members", column: "StudentId");

        migrationBuilder.CreateTable(
            name: "lms_grading_rubrics",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
                SectionId = table.Column<Guid>(type: "uuid", nullable: false),
                CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_lms_grading_rubrics", x => x.Id);
                table.UniqueConstraint("AK_lms_grading_rubrics_Id_SectionId_InstitutionId", x => new { x.Id, x.SectionId, x.InstitutionId });
                table.ForeignKey("FK_p8_rubric_section", x => new { x.SectionId, x.InstitutionId }, "course_sections", new[] { "Id", "InstitutionId" }, onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_p8_rubric_creator", x => x.CreatedBy, "identity_users", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(name: "IX_lms_grading_rubrics_SectionId_InstitutionId", table: "lms_grading_rubrics", columns: new[] { "SectionId", "InstitutionId" });
        migrationBuilder.CreateIndex(name: "IX_lms_grading_rubrics_CreatedBy", table: "lms_grading_rubrics", column: "CreatedBy");

        migrationBuilder.CreateTable(
            name: "lms_rubric_criteria",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
                SectionId = table.Column<Guid>(type: "uuid", nullable: false),
                RubricId = table.Column<Guid>(type: "uuid", nullable: false),
                Position = table.Column<int>(type: "integer", nullable: false),
                Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                MaxPoints = table.Column<decimal>(type: "numeric(10,2)", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_lms_rubric_criteria", x => x.Id);
                table.UniqueConstraint("AK_lms_rubric_criteria_Id_RubricId", x => new { x.Id, x.RubricId });
                table.ForeignKey("FK_p8_criterion_rubric", x => new { x.RubricId, x.SectionId, x.InstitutionId }, "lms_grading_rubrics", new[] { "Id", "SectionId", "InstitutionId" }, onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(name: "IX_lms_rubric_criteria_RubricId_Position", table: "lms_rubric_criteria", columns: new[] { "RubricId", "Position" }, unique: true);
        migrationBuilder.CreateIndex(name: "IX_lms_rubric_criteria_RubricId_SectionId_InstitutionId", table: "lms_rubric_criteria", columns: new[] { "RubricId", "SectionId", "InstitutionId" });

        migrationBuilder.CreateTable(
            name: "lms_group_projects",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
                SectionId = table.Column<Guid>(type: "uuid", nullable: false),
                RubricId = table.Column<Guid?>(type: "uuid", nullable: true),
                Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Instructions = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: false),
                DueAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                MaxScore = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                IsPublished = table.Column<bool>(type: "boolean", nullable: false),
                Version = table.Column<long>(type: "bigint", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_lms_group_projects", x => x.Id);
                table.UniqueConstraint("AK_lms_group_projects_Id_SectionId_InstitutionId", x => new { x.Id, x.SectionId, x.InstitutionId });
                table.ForeignKey("FK_p8_project_section", x => new { x.SectionId, x.InstitutionId }, "course_sections", new[] { "Id", "InstitutionId" }, onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_p8_project_rubric", x => new { x.RubricId, x.SectionId, x.InstitutionId }, "lms_grading_rubrics", new[] { "Id", "SectionId", "InstitutionId" }, onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(name: "IX_lms_group_projects_SectionId_DueAtUtc", table: "lms_group_projects", columns: new[] { "SectionId", "DueAtUtc" });
        migrationBuilder.CreateIndex(name: "IX_lms_group_projects_SectionId_InstitutionId", table: "lms_group_projects", columns: new[] { "SectionId", "InstitutionId" });
        migrationBuilder.CreateIndex(name: "IX_lms_group_projects_RubricId_SectionId_InstitutionId", table: "lms_group_projects", columns: new[] { "RubricId", "SectionId", "InstitutionId" });

        migrationBuilder.CreateTable(
            name: "lms_group_submissions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
                SectionId = table.Column<Guid>(type: "uuid", nullable: false),
                ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                TeamId = table.Column<Guid>(type: "uuid", nullable: false),
                LastEditedBy = table.Column<Guid>(type: "uuid", nullable: false),
                Content = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: false),
                Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                SubmittedAtUtc = table.Column<DateTime?>(type: "timestamp with time zone", nullable: true),
                Score = table.Column<decimal?>(type: "numeric(10,2)", nullable: true),
                Feedback = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: false),
                GradedBy = table.Column<Guid?>(type: "uuid", nullable: true),
                GradedAtUtc = table.Column<DateTime?>(type: "timestamp with time zone", nullable: true),
                Version = table.Column<long>(type: "bigint", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_lms_group_submissions", x => x.Id);
                table.ForeignKey("FK_p8_submission_project", x => new { x.ProjectId, x.SectionId, x.InstitutionId }, "lms_group_projects", new[] { "Id", "SectionId", "InstitutionId" }, onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_p8_submission_team", x => new { x.TeamId, x.SectionId, x.InstitutionId }, "lms_project_teams", new[] { "Id", "SectionId", "InstitutionId" }, onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_p8_submission_editor", x => x.LastEditedBy, "identity_users", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_p8_submission_grader", x => x.GradedBy, "identity_users", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(name: "IX_lms_group_submissions_ProjectId_TeamId", table: "lms_group_submissions", columns: new[] { "ProjectId", "TeamId" }, unique: true);
        migrationBuilder.CreateIndex(name: "IX_lms_group_submissions_ProjectId_SectionId_InstitutionId", table: "lms_group_submissions", columns: new[] { "ProjectId", "SectionId", "InstitutionId" });
        migrationBuilder.CreateIndex(name: "IX_lms_group_submissions_TeamId_SectionId_InstitutionId", table: "lms_group_submissions", columns: new[] { "TeamId", "SectionId", "InstitutionId" });
        migrationBuilder.CreateIndex(name: "IX_lms_group_submissions_LastEditedBy", table: "lms_group_submissions", column: "LastEditedBy");
        migrationBuilder.CreateIndex(name: "IX_lms_group_submissions_GradedBy", table: "lms_group_submissions", column: "GradedBy");

        migrationBuilder.CreateTable(
            name: "lms_rubric_scores",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                SubmissionId = table.Column<Guid>(type: "uuid", nullable: false),
                CriterionId = table.Column<Guid>(type: "uuid", nullable: false),
                Points = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                Comment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_lms_rubric_scores", x => x.Id);
                table.ForeignKey("FK_p8_score_submission", x => x.SubmissionId, "lms_group_submissions", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_p8_score_criterion", x => x.CriterionId, "lms_rubric_criteria", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(name: "IX_lms_rubric_scores_SubmissionId_CriterionId", table: "lms_rubric_scores", columns: new[] { "SubmissionId", "CriterionId" }, unique: true);
        migrationBuilder.CreateIndex(name: "IX_lms_rubric_scores_CriterionId", table: "lms_rubric_scores", column: "CriterionId");

        migrationBuilder.CreateTable(
            name: "lms_project_grade_audit",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                SubmissionId = table.Column<Guid>(type: "uuid", nullable: false),
                ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                PreviousScore = table.Column<decimal?>(type: "numeric(10,2)", nullable: true),
                NewScore = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                AtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_lms_project_grade_audit", x => x.Id);
                table.ForeignKey("FK_p8_audit_submission", x => x.SubmissionId, "lms_group_submissions", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_p8_audit_actor", x => x.ActorId, "identity_users", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex(name: "IX_lms_project_grade_audit_SubmissionId_AtUtc", table: "lms_project_grade_audit", columns: new[] { "SubmissionId", "AtUtc" });
        migrationBuilder.CreateIndex(name: "IX_lms_project_grade_audit_ActorId", table: "lms_project_grade_audit", column: "ActorId");

    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "lms_project_grade_audit");
        migrationBuilder.DropTable(name: "lms_rubric_scores");
        migrationBuilder.DropTable(name: "lms_group_submissions");
        migrationBuilder.DropTable(name: "lms_group_projects");
        migrationBuilder.DropTable(name: "lms_rubric_criteria");
        migrationBuilder.DropTable(name: "lms_grading_rubrics");
        migrationBuilder.DropTable(name: "lms_project_team_members");
        migrationBuilder.DropTable(name: "lms_project_teams");
        migrationBuilder.DropTable(name: "lms_discussion_posts");
        migrationBuilder.DropTable(name: "lms_discussion_topics");
    }
}
