using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Academix.Api.Data.Migrations;

public partial class Phase3QuestionBank : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "exam_bank_questions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
                CourseId = table.Column<Guid>(type: "uuid", nullable: false),
                CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                CurrentRevision = table.Column<int>(type: "integer", nullable: false),
                Version = table.Column<long>(type: "bigint", nullable: false),
                Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                Difficulty = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                IsArchived = table.Column<bool>(type: "boolean", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_exam_bank_questions", x => x.Id);
                table.UniqueConstraint("AK_exam_bank_questions_Id_InstitutionId_CourseId", x => new { x.Id, x.InstitutionId, x.CourseId });
                table.ForeignKey("FK_exam_bank_questions_courses_CourseId_InstitutionId", x => new { x.CourseId, x.InstitutionId },
                    "courses", new[] { "id", "institution_id" }, onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_exam_bank_questions_AspNetUsers_CreatedBy", x => x.CreatedBy,
                    "identity_users", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "exam_bank_revisions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                QuestionId = table.Column<Guid>(type: "uuid", nullable: false),
                InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
                CourseId = table.Column<Guid>(type: "uuid", nullable: false),
                RevisionNumber = table.Column<int>(type: "integer", nullable: false),
                BodyJson = table.Column<string>(type: "jsonb", nullable: false),
                AnswerKeyJson = table.Column<string>(type: "jsonb", nullable: false),
                CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_exam_bank_revisions", x => x.Id);
                table.ForeignKey("FK_exam_bank_revisions_question_scope",
                    x => new { x.QuestionId, x.InstitutionId, x.CourseId },
                    "exam_bank_questions", new[] { "Id", "InstitutionId", "CourseId" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_exam_bank_revisions_AspNetUsers_CreatedBy", x => x.CreatedBy,
                    "identity_users", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateIndex("IX_exam_bank_questions_CourseId_InstitutionId", "exam_bank_questions", new[] { "CourseId", "InstitutionId" });
        migrationBuilder.CreateIndex("IX_exam_bank_questions_CreatedBy", "exam_bank_questions", "CreatedBy");
        migrationBuilder.CreateIndex("IX_exam_bank_questions_InstitutionId_CourseId_IsArchived", "exam_bank_questions",
            new[] { "InstitutionId", "CourseId", "IsArchived" });
        migrationBuilder.CreateIndex("IX_exam_bank_revisions_CreatedBy", "exam_bank_revisions", "CreatedBy");
        migrationBuilder.CreateIndex("IX_exam_bank_revisions_QuestionId_RevisionNumber", "exam_bank_revisions",
            new[] { "QuestionId", "RevisionNumber" }, unique: true);
        migrationBuilder.CreateIndex("IX_exam_bank_revisions_QuestionId_InstitutionId_CourseId", "exam_bank_revisions",
            new[] { "QuestionId", "InstitutionId", "CourseId" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "exam_bank_revisions");
        migrationBuilder.DropTable(name: "exam_bank_questions");
    }
}
