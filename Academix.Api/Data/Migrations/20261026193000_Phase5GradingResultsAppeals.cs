using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Academix.Api.Data.Migrations;

public partial class Phase5GradingResultsAppeals : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "exam_grade_reports",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                AttemptId = table.Column<Guid>(type: "uuid", nullable: false),
                ExamId = table.Column<Guid>(type: "uuid", nullable: false),
                InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
                CourseId = table.Column<Guid>(type: "uuid", nullable: false),
                SectionId = table.Column<Guid>(type: "uuid", nullable: false),
                StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                EarnedPoints = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                MaximumPoints = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                Version = table.Column<long>(type: "bigint", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                PublishedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_exam_grade_reports", x => x.Id);
                table.UniqueConstraint("AK_exam_grade_reports_Id_AttemptId", x => new { x.Id, x.AttemptId });
                table.ForeignKey("FK_grade_reports_attempt",
                    x => new { x.AttemptId, x.ExamId, x.InstitutionId, x.CourseId },
                    "exam_attempts", new[] { "Id", "ExamId", "InstitutionId", "CourseId" },
                    onDelete: ReferentialAction.Restrict);
                table.CheckConstraint("CK_grade_report_status", "\"Status\" IN ('Draft','Published')");
                table.CheckConstraint("CK_grade_report_points", "\"EarnedPoints\" >= 0 AND \"MaximumPoints\" >= \"EarnedPoints\"");
            });

        migrationBuilder.CreateTable(
            name: "exam_grade_items",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ReportId = table.Column<Guid>(type: "uuid", nullable: false),
                AttemptId = table.Column<Guid>(type: "uuid", nullable: false),
                PublishedQuestionId = table.Column<Guid>(type: "uuid", nullable: false),
                MaximumPoints = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                EarnedPoints = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: true),
                State = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                Feedback = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                GradedBy = table.Column<Guid>(type: "uuid", nullable: true),
                GradedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_exam_grade_items", x => x.Id);
                table.ForeignKey("FK_grade_items_report", x => new { x.ReportId, x.AttemptId },
                    "exam_grade_reports", new[] { "Id", "AttemptId" }, onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_grade_items_attempt_question", x => new { x.AttemptId, x.PublishedQuestionId },
                    "exam_attempt_questions", new[] { "AttemptId", "PublishedQuestionId" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_grade_items_grader", x => x.GradedBy,
                    "identity_users", "Id", onDelete: ReferentialAction.Restrict);
                table.CheckConstraint("CK_grade_item_state", "\"State\" IN ('PendingManual','Auto','Manual')");
                table.CheckConstraint("CK_grade_item_points", "\"MaximumPoints\" >= 0 AND (\"EarnedPoints\" IS NULL OR (\"EarnedPoints\" >= 0 AND \"EarnedPoints\" <= \"MaximumPoints\"))");
            });

        migrationBuilder.CreateTable(
            name: "exam_grade_audits",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ReportId = table.Column<Guid>(type: "uuid", nullable: false),
                PublishedQuestionId = table.Column<Guid>(type: "uuid", nullable: true),
                ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                Action = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                PreviousPoints = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: true),
                NewPoints = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: true),
                Reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                AtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_exam_grade_audits", x => x.Id);
                table.ForeignKey("FK_grade_audits_report", x => x.ReportId, "exam_grade_reports", "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_grade_audits_actor", x => x.ActorId, "identity_users", "Id",
                    onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "exam_grade_appeals",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ReportId = table.Column<Guid>(type: "uuid", nullable: false),
                StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                Message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                Resolution = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                ResolvedBy = table.Column<Guid>(type: "uuid", nullable: true),
                CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                ResolvedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_exam_grade_appeals", x => x.Id);
                table.ForeignKey("FK_grade_appeals_report", x => x.ReportId, "exam_grade_reports", "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_grade_appeals_student", x => x.StudentId, "identity_users", "Id",
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_grade_appeals_reviewer", x => x.ResolvedBy, "identity_users", "Id",
                    onDelete: ReferentialAction.Restrict);
                table.CheckConstraint("CK_grade_appeal_status", "\"Status\" IN ('Open','Accepted','Rejected')");
            });

        migrationBuilder.CreateIndex(name: "IX_exam_grade_reports_AttemptId", table: "exam_grade_reports",
            column: "AttemptId", unique: true);
        migrationBuilder.CreateIndex(name: "IX_exam_grade_reports_ExamId_Status", table: "exam_grade_reports",
            columns: new[] { "ExamId", "Status" });
        migrationBuilder.CreateIndex(name: "IX_exam_grade_reports_AttemptId_ExamId_InstitutionId_CourseId", table: "exam_grade_reports",
            columns: new[] { "AttemptId", "ExamId", "InstitutionId", "CourseId" });
        migrationBuilder.CreateIndex(name: "IX_exam_grade_items_ReportId_PublishedQuestionId", table: "exam_grade_items",
            columns: new[] { "ReportId", "PublishedQuestionId" }, unique: true);
        migrationBuilder.CreateIndex(name: "IX_exam_grade_items_ReportId_AttemptId", table: "exam_grade_items",
            columns: new[] { "ReportId", "AttemptId" });
        migrationBuilder.CreateIndex(name: "IX_exam_grade_items_AttemptId_PublishedQuestionId", table: "exam_grade_items",
            columns: new[] { "AttemptId", "PublishedQuestionId" });
        migrationBuilder.CreateIndex(name: "IX_exam_grade_items_GradedBy", table: "exam_grade_items", column: "GradedBy");
        migrationBuilder.CreateIndex(name: "IX_exam_grade_audits_ReportId_AtUtc", table: "exam_grade_audits",
            columns: new[] { "ReportId", "AtUtc" });
        migrationBuilder.CreateIndex(name: "IX_exam_grade_audits_ActorId", table: "exam_grade_audits", column: "ActorId");
        migrationBuilder.CreateIndex(name: "IX_exam_grade_appeals_ReportId", table: "exam_grade_appeals",
            column: "ReportId", unique: true);
        migrationBuilder.CreateIndex(name: "IX_exam_grade_appeals_StudentId", table: "exam_grade_appeals", column: "StudentId");
        migrationBuilder.CreateIndex(name: "IX_exam_grade_appeals_ResolvedBy", table: "exam_grade_appeals", column: "ResolvedBy");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "exam_grade_appeals");
        migrationBuilder.DropTable(name: "exam_grade_audits");
        migrationBuilder.DropTable(name: "exam_grade_items");
        migrationBuilder.DropTable(name: "exam_grade_reports");
    }
}
