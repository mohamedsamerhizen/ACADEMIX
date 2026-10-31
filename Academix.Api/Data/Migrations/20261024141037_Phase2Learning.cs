using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Academix.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase2Learning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "lms_announcements",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Body = table.Column<string>(type: "character varying(10000)", maxLength: 10000, nullable: false),
                    PublishedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lms_announcements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_lms_announcements_course_sections_SectionId_InstitutionId",
                        columns: x => new { x.SectionId, x.InstitutionId },
                        principalTable: "course_sections",
                        principalColumns: new[] { "Id", "InstitutionId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "lms_assignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Instructions = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: false),
                    DueAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    MaxScore = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: false),
                    IsPublished = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lms_assignments", x => x.Id);
                    table.UniqueConstraint("AK_lms_assignments_Id_SectionId", x => new { x.Id, x.SectionId });
                    table.ForeignKey(
                        name: "FK_lms_assignments_course_sections_SectionId_InstitutionId",
                        columns: x => new { x.SectionId, x.InstitutionId },
                        principalTable: "course_sections",
                        principalColumns: new[] { "Id", "InstitutionId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "lms_files",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    UploadedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    IsStudentUpload = table.Column<bool>(type: "boolean", nullable: false),
                    ObjectKey = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Size = table.Column<long>(type: "bigint", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lms_files", x => x.Id);
                    table.ForeignKey(
                        name: "FK_lms_files_course_sections_SectionId_InstitutionId",
                        columns: x => new { x.SectionId, x.InstitutionId },
                        principalTable: "course_sections",
                        principalColumns: new[] { "Id", "InstitutionId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_lms_files_identity_users_UploadedBy",
                        column: x => x.UploadedBy,
                        principalTable: "identity_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "lms_units",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    IsPublished = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lms_units", x => x.Id);
                    table.UniqueConstraint("AK_lms_units_Id_SectionId", x => new { x.Id, x.SectionId });
                    table.ForeignKey(
                        name: "FK_lms_units_course_sections_SectionId_InstitutionId",
                        columns: x => new { x.SectionId, x.InstitutionId },
                        principalTable: "course_sections",
                        principalColumns: new[] { "Id", "InstitutionId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "lms_submissions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Content = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: false),
                    AttachmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    SubmittedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Score = table.Column<decimal>(type: "numeric(10,2)", precision: 10, scale: 2, nullable: true),
                    Feedback = table.Column<string>(type: "character varying(5000)", maxLength: 5000, nullable: true),
                    GradedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    GradedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lms_submissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_lms_submissions_identity_users_GradedBy",
                        column: x => x.GradedBy,
                        principalTable: "identity_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_lms_submissions_identity_users_StudentId",
                        column: x => x.StudentId,
                        principalTable: "identity_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_lms_submissions_lms_assignments_AssignmentId_SectionId",
                        columns: x => new { x.AssignmentId, x.SectionId },
                        principalTable: "lms_assignments",
                        principalColumns: new[] { "Id", "SectionId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_lms_submissions_lms_files_AttachmentId",
                        column: x => x.AttachmentId,
                        principalTable: "lms_files",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "lms_lessons",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    UnitId = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Body = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: false),
                    Position = table.Column<int>(type: "integer", nullable: false),
                    IsPublished = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lms_lessons", x => x.Id);
                    table.ForeignKey(
                        name: "FK_lms_lessons_lms_units_UnitId_SectionId",
                        columns: x => new { x.UnitId, x.SectionId },
                        principalTable: "lms_units",
                        principalColumns: new[] { "Id", "SectionId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "lms_lesson_completions",
                columns: table => new
                {
                    LessonId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CompletedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_lms_lesson_completions", x => new { x.LessonId, x.StudentId });
                    table.ForeignKey(
                        name: "FK_lms_lesson_completions_identity_users_StudentId",
                        column: x => x.StudentId,
                        principalTable: "identity_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_lms_lesson_completions_lms_lessons_LessonId",
                        column: x => x.LessonId,
                        principalTable: "lms_lessons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_lms_announcements_SectionId_InstitutionId",
                table: "lms_announcements",
                columns: new[] { "SectionId", "InstitutionId" });

            migrationBuilder.CreateIndex(
                name: "IX_lms_announcements_SectionId_PublishedAtUtc",
                table: "lms_announcements",
                columns: new[] { "SectionId", "PublishedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_lms_assignments_SectionId_InstitutionId",
                table: "lms_assignments",
                columns: new[] { "SectionId", "InstitutionId" });

            migrationBuilder.CreateIndex(
                name: "IX_lms_files_ObjectKey",
                table: "lms_files",
                column: "ObjectKey",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_lms_files_SectionId_InstitutionId",
                table: "lms_files",
                columns: new[] { "SectionId", "InstitutionId" });

            migrationBuilder.CreateIndex(
                name: "IX_lms_files_UploadedBy",
                table: "lms_files",
                column: "UploadedBy");

            migrationBuilder.CreateIndex(
                name: "IX_lms_lesson_completions_SectionId_StudentId",
                table: "lms_lesson_completions",
                columns: new[] { "SectionId", "StudentId" });

            migrationBuilder.CreateIndex(
                name: "IX_lms_lesson_completions_StudentId",
                table: "lms_lesson_completions",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_lms_lessons_UnitId_Position",
                table: "lms_lessons",
                columns: new[] { "UnitId", "Position" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_lms_lessons_UnitId_SectionId",
                table: "lms_lessons",
                columns: new[] { "UnitId", "SectionId" });

            migrationBuilder.CreateIndex(
                name: "IX_lms_submissions_AssignmentId_SectionId",
                table: "lms_submissions",
                columns: new[] { "AssignmentId", "SectionId" });

            migrationBuilder.CreateIndex(
                name: "IX_lms_submissions_AssignmentId_StudentId",
                table: "lms_submissions",
                columns: new[] { "AssignmentId", "StudentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_lms_submissions_AttachmentId",
                table: "lms_submissions",
                column: "AttachmentId");

            migrationBuilder.CreateIndex(
                name: "IX_lms_submissions_GradedBy",
                table: "lms_submissions",
                column: "GradedBy");

            migrationBuilder.CreateIndex(
                name: "IX_lms_submissions_StudentId",
                table: "lms_submissions",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_lms_units_SectionId_InstitutionId",
                table: "lms_units",
                columns: new[] { "SectionId", "InstitutionId" });

            migrationBuilder.CreateIndex(
                name: "IX_lms_units_SectionId_Position",
                table: "lms_units",
                columns: new[] { "SectionId", "Position" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "lms_announcements");

            migrationBuilder.DropTable(
                name: "lms_lesson_completions");

            migrationBuilder.DropTable(
                name: "lms_submissions");

            migrationBuilder.DropTable(
                name: "lms_lessons");

            migrationBuilder.DropTable(
                name: "lms_assignments");

            migrationBuilder.DropTable(
                name: "lms_files");

            migrationBuilder.DropTable(
                name: "lms_units");
        }
    }
}
