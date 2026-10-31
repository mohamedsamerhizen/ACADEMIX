using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Academix.Api.Data.Migrations;

public partial class Phase4ExamExecution : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "exam_attempts",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ExamId = table.Column<Guid>(type: "uuid", nullable: false),
                InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
                CourseId = table.Column<Guid>(type: "uuid", nullable: false),
                SectionId = table.Column<Guid>(type: "uuid", nullable: false),
                StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                AttemptNumber = table.Column<int>(type: "integer", nullable: false),
                Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                StartedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                DeadlineUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                SubmittedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                LastActivityAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                Version = table.Column<long>(type: "bigint", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_exam_attempts", x => x.Id);
                table.UniqueConstraint("AK_exam_attempts_Id_ExamId_InstitutionId_CourseId",
                    x => new { x.Id, x.ExamId, x.InstitutionId, x.CourseId });
                table.ForeignKey("FK_exam_attempts_exam",
                    x => new { x.ExamId, x.InstitutionId, x.CourseId },
                    "exam_definitions", new[] { "Id", "InstitutionId", "CourseId" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_exam_attempts_student", x => x.StudentId, "identity_users", "Id",
                    onDelete: ReferentialAction.Restrict);
                table.CheckConstraint("CK_exam_attempts_number", "\"AttemptNumber\" BETWEEN 1 AND 10");
                table.CheckConstraint("CK_exam_attempts_status", "\"Status\" IN ('InProgress','Submitted','Expired')");
                table.CheckConstraint("CK_exam_attempts_deadline", "\"DeadlineUtc\" > \"StartedAtUtc\"");
            });

        migrationBuilder.CreateTable(
            name: "exam_attempt_questions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                AttemptId = table.Column<Guid>(type: "uuid", nullable: false),
                ExamId = table.Column<Guid>(type: "uuid", nullable: false),
                InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
                CourseId = table.Column<Guid>(type: "uuid", nullable: false),
                PublishedQuestionId = table.Column<Guid>(type: "uuid", nullable: false),
                Position = table.Column<int>(type: "integer", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_exam_attempt_questions", x => x.Id);
                table.UniqueConstraint("AK_exam_attempt_questions_AttemptId_PublishedQuestionId",
                    x => new { x.AttemptId, x.PublishedQuestionId });
                table.ForeignKey("FK_exam_attempt_questions_attempt",
                    x => new { x.AttemptId, x.ExamId, x.InstitutionId, x.CourseId },
                    "exam_attempts", new[] { "Id", "ExamId", "InstitutionId", "CourseId" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_exam_attempt_questions_published", x => x.PublishedQuestionId,
                    "exam_published_questions", "Id", onDelete: ReferentialAction.Restrict);
                table.CheckConstraint("CK_exam_attempt_questions_position", "\"Position\" BETWEEN 1 AND 200");
            });

        migrationBuilder.CreateTable(
            name: "exam_attempt_answers",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                AttemptId = table.Column<Guid>(type: "uuid", nullable: false),
                ExamId = table.Column<Guid>(type: "uuid", nullable: false),
                InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
                CourseId = table.Column<Guid>(type: "uuid", nullable: false),
                PublishedQuestionId = table.Column<Guid>(type: "uuid", nullable: false),
                AnswerJson = table.Column<string>(type: "jsonb", nullable: false),
                ClientSequence = table.Column<long>(type: "bigint", nullable: false),
                SavedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_exam_attempt_answers", x => x.Id);
                table.ForeignKey("FK_exam_attempt_answers_attempt",
                    x => new { x.AttemptId, x.ExamId, x.InstitutionId, x.CourseId },
                    "exam_attempts", new[] { "Id", "ExamId", "InstitutionId", "CourseId" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_exam_attempt_answers_question",
                    x => new { x.AttemptId, x.PublishedQuestionId },
                    "exam_attempt_questions", new[] { "AttemptId", "PublishedQuestionId" },
                    onDelete: ReferentialAction.Restrict);
                table.CheckConstraint("CK_exam_attempt_answers_sequence", "\"ClientSequence\" BETWEEN 1 AND 1000000000");
            });

        migrationBuilder.CreateIndex(name: "IX_exam_attempts_ExamId_InstitutionId_CourseId",
            table: "exam_attempts", columns: new[] { "ExamId", "InstitutionId", "CourseId" });
        migrationBuilder.CreateIndex(name: "IX_exam_attempts_ExamId_StudentId_AttemptNumber",
            table: "exam_attempts", columns: new[] { "ExamId", "StudentId", "AttemptNumber" }, unique: true);
        migrationBuilder.CreateIndex(name: "IX_exam_attempts_StudentId",
            table: "exam_attempts", column: "StudentId");
        migrationBuilder.CreateIndex(name: "IX_exam_attempts_StudentId_ExamId",
            table: "exam_attempts", columns: new[] { "StudentId", "ExamId" });
        migrationBuilder.CreateIndex(name: "IX_exam_attempt_questions_AttemptId_ExamId_InstitutionId_CourseId",
            table: "exam_attempt_questions", columns: new[] { "AttemptId", "ExamId", "InstitutionId", "CourseId" });
        migrationBuilder.CreateIndex(name: "IX_exam_attempt_questions_AttemptId_Position",
            table: "exam_attempt_questions", columns: new[] { "AttemptId", "Position" }, unique: true);
        migrationBuilder.CreateIndex(name: "IX_exam_attempt_questions_PublishedQuestionId",
            table: "exam_attempt_questions", column: "PublishedQuestionId");
        migrationBuilder.CreateIndex(name: "IX_exam_attempt_answers_AttemptId_ExamId_InstitutionId_CourseId",
            table: "exam_attempt_answers", columns: new[] { "AttemptId", "ExamId", "InstitutionId", "CourseId" });
        migrationBuilder.CreateIndex(name: "IX_exam_attempt_answers_AttemptId_PublishedQuestionId",
            table: "exam_attempt_answers", columns: new[] { "AttemptId", "PublishedQuestionId" }, unique: true);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "exam_attempt_answers");
        migrationBuilder.DropTable(name: "exam_attempt_questions");
        migrationBuilder.DropTable(name: "exam_attempts");
    }
}
