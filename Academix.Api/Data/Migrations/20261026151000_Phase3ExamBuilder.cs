using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Academix.Api.Data.Migrations;

public partial class Phase3ExamBuilder : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "exam_definitions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
                CourseId = table.Column<Guid>(type: "uuid", nullable: false),
                SectionId = table.Column<Guid>(type: "uuid", nullable: false),
                CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                Version = table.Column<long>(type: "bigint", nullable: false),
                DurationMinutes = table.Column<int>(type: "integer", nullable: false),
                MaxAttempts = table.Column<int>(type: "integer", nullable: false),
                OpensAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                ClosesAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                ShuffleQuestions = table.Column<bool>(type: "boolean", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                PublishedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_exam_definitions", x => x.Id);
                table.UniqueConstraint("AK_exam_definitions_Id_InstitutionId_CourseId", x => new { x.Id, x.InstitutionId, x.CourseId });
                table.ForeignKey("FK_exam_definitions_section", x => new { x.SectionId, x.InstitutionId },
                    "course_sections", new[] { "Id", "InstitutionId" }, onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_exam_definitions_course", x => new { x.CourseId, x.InstitutionId },
                    "courses", new[] { "id", "institution_id" }, onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_exam_definitions_creator", x => x.CreatedBy,
                    "identity_users", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "exam_draft_questions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ExamId = table.Column<Guid>(type: "uuid", nullable: false),
                InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
                CourseId = table.Column<Guid>(type: "uuid", nullable: false),
                QuestionId = table.Column<Guid>(type: "uuid", nullable: false),
                RevisionNumber = table.Column<int>(type: "integer", nullable: false),
                Position = table.Column<int>(type: "integer", nullable: false),
                Points = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_exam_draft_questions", x => x.Id);
                table.ForeignKey("FK_exam_draft_questions_exam", x => new { x.ExamId, x.InstitutionId, x.CourseId },
                    "exam_definitions", new[] { "Id", "InstitutionId", "CourseId" }, onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_exam_draft_questions_bank", x => new { x.QuestionId, x.InstitutionId, x.CourseId },
                    "exam_bank_questions", new[] { "Id", "InstitutionId", "CourseId" }, onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "exam_published_questions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ExamId = table.Column<Guid>(type: "uuid", nullable: false),
                InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
                CourseId = table.Column<Guid>(type: "uuid", nullable: false),
                QuestionId = table.Column<Guid>(type: "uuid", nullable: false),
                RevisionNumber = table.Column<int>(type: "integer", nullable: false),
                Position = table.Column<int>(type: "integer", nullable: false),
                Points = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false),
                BodyJson = table.Column<string>(type: "jsonb", nullable: false),
                AnswerKeyJson = table.Column<string>(type: "jsonb", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_exam_published_questions", x => x.Id);
                table.ForeignKey("FK_exam_published_questions_exam", x => new { x.ExamId, x.InstitutionId, x.CourseId },
                    "exam_definitions", new[] { "Id", "InstitutionId", "CourseId" }, onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_exam_published_questions_bank", x => new { x.QuestionId, x.InstitutionId, x.CourseId },
                    "exam_bank_questions", new[] { "Id", "InstitutionId", "CourseId" }, onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex("IX_exam_definitions_CourseId_InstitutionId", "exam_definitions", new[] { "CourseId", "InstitutionId" });
        migrationBuilder.CreateIndex("IX_exam_definitions_CreatedBy", "exam_definitions", "CreatedBy");
        migrationBuilder.CreateIndex("IX_exam_definitions_InstitutionId_SectionId_Status", "exam_definitions", new[] { "InstitutionId", "SectionId", "Status" });
        migrationBuilder.CreateIndex("IX_exam_definitions_SectionId_InstitutionId", "exam_definitions", new[] { "SectionId", "InstitutionId" });
        migrationBuilder.CreateIndex("IX_exam_draft_questions_ExamId_Position", "exam_draft_questions", new[] { "ExamId", "Position" }, unique: true);
        migrationBuilder.CreateIndex("IX_exam_draft_questions_ExamId_InstitutionId_CourseId", "exam_draft_questions", new[] { "ExamId", "InstitutionId", "CourseId" });
        migrationBuilder.CreateIndex("IX_exam_draft_questions_QuestionId_InstitutionId_CourseId", "exam_draft_questions", new[] { "QuestionId", "InstitutionId", "CourseId" });
        migrationBuilder.CreateIndex("IX_exam_published_questions_ExamId_Position", "exam_published_questions", new[] { "ExamId", "Position" }, unique: true);
        migrationBuilder.CreateIndex("IX_exam_published_questions_ExamId_InstitutionId_CourseId", "exam_published_questions", new[] { "ExamId", "InstitutionId", "CourseId" });
        migrationBuilder.CreateIndex("IX_exam_published_questions_QuestionId_InstitutionId_CourseId", "exam_published_questions", new[] { "QuestionId", "InstitutionId", "CourseId" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "exam_draft_questions");
        migrationBuilder.DropTable(name: "exam_published_questions");
        migrationBuilder.DropTable(name: "exam_definitions");
    }
}
