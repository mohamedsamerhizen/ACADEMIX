using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Academix.Api.Data.Migrations;

public partial class Phase3QuestionPools : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "exam_question_pools",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                ExamId = table.Column<Guid>(type: "uuid", nullable: false),
                InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
                CourseId = table.Column<Guid>(type: "uuid", nullable: false),
                Position = table.Column<int>(type: "integer", nullable: false),
                Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                Difficulty = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                Quantity = table.Column<int>(type: "integer", nullable: false),
                Points = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_exam_question_pools", x => x.Id);
                table.ForeignKey("FK_exam_question_pools_exam",
                    x => new { x.ExamId, x.InstitutionId, x.CourseId },
                    "exam_definitions", new[] { "Id", "InstitutionId", "CourseId" },
                    onDelete: ReferentialAction.Restrict);
                table.CheckConstraint("CK_exam_question_pools_quantity", "\"Quantity\" BETWEEN 1 AND 200");
                table.CheckConstraint("CK_exam_question_pools_points", "\"Points\" > 0 AND \"Points\" <= 1000");
            });
        migrationBuilder.CreateIndex(
            name: "IX_exam_question_pools_ExamId_Position", table: "exam_question_pools",
            columns: new[] { "ExamId", "Position" }, unique: true);
        migrationBuilder.CreateIndex(
            name: "IX_exam_question_pools_ExamId_InstitutionId_CourseId", table: "exam_question_pools",
            columns: new[] { "ExamId", "InstitutionId", "CourseId" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.DropTable(name: "exam_question_pools");
}
