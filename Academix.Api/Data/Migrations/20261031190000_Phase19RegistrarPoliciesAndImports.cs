using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Academix.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase19RegistrarPoliciesAndImports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_exam_attempts_StudentId",
                table: "exam_attempts");

            migrationBuilder.CreateTable(
                name: "institutional_import_jobs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceSystem = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Digest = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ApprovedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_institutional_import_jobs", x => x.Id);
                    table.UniqueConstraint("AK_institutional_import_jobs_Id_InstitutionId", x => new { x.Id, x.InstitutionId });
                    table.CheckConstraint("CK_import_job_state", "\"Status\" IN ('Draft','Approved') AND (\"Status\" <> 'Approved' OR (\"ApprovedBy\" IS NOT NULL AND \"ApprovedAtUtc\" IS NOT NULL AND \"ApprovedBy\" <> \"CreatedBy\"))");
                    table.ForeignKey(
                        name: "FK_institutional_import_jobs_institutions_InstitutionId",
                        column: x => x.InstitutionId,
                        principalTable: "institutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "registrar_policy_revisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProgramId = table.Column<Guid>(type: "uuid", nullable: true),
                    RulesJson = table.Column<string>(type: "jsonb", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    EffectiveFromUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EffectiveUntilUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    PublishedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PublishedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_registrar_policy_revisions", x => x.Id);
                    table.UniqueConstraint("AK_registrar_policy_revisions_Id_InstitutionId", x => new { x.Id, x.InstitutionId });
                    table.CheckConstraint("CK_registrar_policy_interval", "\"EffectiveFromUtc\" < \"EffectiveUntilUtc\"");
                    table.CheckConstraint("CK_registrar_policy_state", "\"Status\" IN ('Draft','Published') AND ((\"Status\" = 'Draft' AND \"PublishedBy\" IS NULL AND \"PublishedAtUtc\" IS NULL) OR (\"Status\" = 'Published' AND \"PublishedBy\" IS NOT NULL AND \"PublishedAtUtc\" IS NOT NULL AND \"PublishedBy\" <> \"CreatedBy\"))");
                    table.ForeignKey(
                        name: "FK_registrar_policy_revisions_academic_programs_ProgramId_Inst~",
                        columns: x => new { x.ProgramId, x.InstitutionId },
                        principalTable: "academic_programs",
                        principalColumns: new[] { "Id", "InstitutionId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_registrar_policy_revisions_institutions_InstitutionId",
                        column: x => x.InstitutionId,
                        principalTable: "institutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "registrar_student_holds",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReleasedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReleasedBy = table.Column<Guid>(type: "uuid", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_registrar_student_holds", x => x.Id);
                    table.UniqueConstraint("AK_registrar_student_holds_Id_InstitutionId", x => new { x.Id, x.InstitutionId });
                    table.ForeignKey(
                        name: "FK_registrar_student_holds_institutions_InstitutionId",
                        column: x => x.InstitutionId,
                        principalTable: "institutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "institutional_import_rows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    JobId = table.Column<Guid>(type: "uuid", nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    PayloadJson = table.Column<string>(type: "jsonb", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Issue = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: true),
                    AppliedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_institutional_import_rows", x => x.Id);
                    table.UniqueConstraint("AK_institutional_import_rows_Id_InstitutionId", x => new { x.Id, x.InstitutionId });
                    table.CheckConstraint("CK_import_row_state", "\"Status\" IN ('Pending','Valid','Invalid','Applied','Failed') AND \"Number\" BETWEEN 1 AND 1000");
                    table.ForeignKey(
                        name: "FK_institutional_import_rows_institutional_import_jobs_JobId_I~",
                        columns: x => new { x.JobId, x.InstitutionId },
                        principalTable: "institutional_import_jobs",
                        principalColumns: new[] { "Id", "InstitutionId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "registrar_eligibility_decisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    SectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    PolicyId = table.Column<Guid>(type: "uuid", nullable: true),
                    Eligible = table.Column<bool>(type: "boolean", nullable: false),
                    SnapshotJson = table.Column<string>(type: "jsonb", nullable: false),
                    AtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_registrar_eligibility_decisions", x => x.Id);
                    table.UniqueConstraint("AK_registrar_eligibility_decisions_Id_InstitutionId", x => new { x.Id, x.InstitutionId });
                    table.ForeignKey(
                        name: "FK_registrar_eligibility_decisions_course_sections_SectionId_I~",
                        columns: x => new { x.SectionId, x.InstitutionId },
                        principalTable: "course_sections",
                        principalColumns: new[] { "Id", "InstitutionId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_registrar_eligibility_decisions_institutions_InstitutionId",
                        column: x => x.InstitutionId,
                        principalTable: "institutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_registrar_eligibility_decisions_registrar_policy_revisions_~",
                        columns: x => new { x.PolicyId, x.InstitutionId },
                        principalTable: "registrar_policy_revisions",
                        principalColumns: new[] { "Id", "InstitutionId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "registrar_exceptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    SectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    PolicyId = table.Column<Guid>(type: "uuid", nullable: false),
                    Rule = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RequestedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    ReviewedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_registrar_exceptions", x => x.Id);
                    table.UniqueConstraint("AK_registrar_exceptions_Id_InstitutionId", x => new { x.Id, x.InstitutionId });
                    table.CheckConstraint("CK_registrar_exception", "\"Status\" IN ('Pending','Approved','Rejected') AND \"Rule\" IN ('Prerequisites','CreditLoad','EnrollmentApproval')");
                    table.ForeignKey(
                        name: "FK_registrar_exceptions_course_sections_SectionId_InstitutionId",
                        columns: x => new { x.SectionId, x.InstitutionId },
                        principalTable: "course_sections",
                        principalColumns: new[] { "Id", "InstitutionId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_registrar_exceptions_institutions_InstitutionId",
                        column: x => x.InstitutionId,
                        principalTable: "institutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_registrar_exceptions_registrar_policy_revisions_PolicyId_In~",
                        columns: x => new { x.PolicyId, x.InstitutionId },
                        principalTable: "registrar_policy_revisions",
                        principalColumns: new[] { "Id", "InstitutionId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "institutional_external_identities",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceSystem = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    ExternalId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ImportRowId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_institutional_external_identities", x => x.Id);
                    table.ForeignKey(
                        name: "FK_institutional_external_identities_institutional_import_rows~",
                        columns: x => new { x.ImportRowId, x.InstitutionId },
                        principalTable: "institutional_import_rows",
                        principalColumns: new[] { "Id", "InstitutionId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "institutional_outbox_events",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ImportRowId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PayloadJson = table.Column<string>(type: "jsonb", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_institutional_outbox_events", x => x.Id);
                    table.ForeignKey(
                        name: "FK_institutional_outbox_events_institutional_import_rows_Impor~",
                        columns: x => new { x.ImportRowId, x.InstitutionId },
                        principalTable: "institutional_import_rows",
                        principalColumns: new[] { "Id", "InstitutionId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_institutional_external_identities_ImportRowId_InstitutionId",
                table: "institutional_external_identities",
                columns: new[] { "ImportRowId", "InstitutionId" });

            migrationBuilder.CreateIndex(
                name: "IX_institutional_external_identities_InstitutionId_SourceSyste~",
                table: "institutional_external_identities",
                columns: new[] { "InstitutionId", "SourceSystem", "ExternalId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_institutional_import_jobs_InstitutionId_ClientRequestId",
                table: "institutional_import_jobs",
                columns: new[] { "InstitutionId", "ClientRequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_institutional_import_rows_JobId_InstitutionId",
                table: "institutional_import_rows",
                columns: new[] { "JobId", "InstitutionId" });

            migrationBuilder.CreateIndex(
                name: "IX_institutional_import_rows_JobId_Number",
                table: "institutional_import_rows",
                columns: new[] { "JobId", "Number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_institutional_outbox_events_ImportRowId",
                table: "institutional_outbox_events",
                column: "ImportRowId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_institutional_outbox_events_ImportRowId_InstitutionId",
                table: "institutional_outbox_events",
                columns: new[] { "ImportRowId", "InstitutionId" });

            migrationBuilder.CreateIndex(
                name: "IX_registrar_eligibility_decisions_InstitutionId_StudentId_AtU~",
                table: "registrar_eligibility_decisions",
                columns: new[] { "InstitutionId", "StudentId", "AtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_registrar_eligibility_decisions_PolicyId_InstitutionId",
                table: "registrar_eligibility_decisions",
                columns: new[] { "PolicyId", "InstitutionId" });

            migrationBuilder.CreateIndex(
                name: "IX_registrar_eligibility_decisions_SectionId_InstitutionId",
                table: "registrar_eligibility_decisions",
                columns: new[] { "SectionId", "InstitutionId" });

            migrationBuilder.CreateIndex(
                name: "IX_registrar_exceptions_InstitutionId_StudentId_SectionId",
                table: "registrar_exceptions",
                columns: new[] { "InstitutionId", "StudentId", "SectionId" });

            migrationBuilder.CreateIndex(
                name: "IX_registrar_exceptions_PolicyId_InstitutionId",
                table: "registrar_exceptions",
                columns: new[] { "PolicyId", "InstitutionId" });

            migrationBuilder.CreateIndex(
                name: "IX_registrar_exceptions_SectionId_InstitutionId",
                table: "registrar_exceptions",
                columns: new[] { "SectionId", "InstitutionId" });

            migrationBuilder.CreateIndex(
                name: "IX_registrar_policy_revisions_InstitutionId_ProgramId_Effectiv~",
                table: "registrar_policy_revisions",
                columns: new[] { "InstitutionId", "ProgramId", "EffectiveFromUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_registrar_policy_revisions_ProgramId_InstitutionId",
                table: "registrar_policy_revisions",
                columns: new[] { "ProgramId", "InstitutionId" });

            migrationBuilder.CreateIndex(
                name: "IX_registrar_student_holds_InstitutionId_StudentId_ReleasedAtU~",
                table: "registrar_student_holds",
                columns: new[] { "InstitutionId", "StudentId", "ReleasedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "institutional_external_identities");

            migrationBuilder.DropTable(
                name: "institutional_outbox_events");

            migrationBuilder.DropTable(
                name: "registrar_eligibility_decisions");

            migrationBuilder.DropTable(
                name: "registrar_exceptions");

            migrationBuilder.DropTable(
                name: "registrar_student_holds");

            migrationBuilder.DropTable(
                name: "institutional_import_rows");

            migrationBuilder.DropTable(
                name: "registrar_policy_revisions");

            migrationBuilder.DropTable(
                name: "institutional_import_jobs");

            migrationBuilder.CreateIndex(
                name: "IX_exam_attempts_StudentId",
                table: "exam_attempts",
                column: "StudentId");
        }
    }
}
