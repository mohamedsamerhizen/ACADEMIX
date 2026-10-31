using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Academix.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase19WorkflowCompletion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_academic_request_status",
                table: "student_academic_requests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_request_history_to",
                table: "academic_request_history");

            migrationBuilder.AddColumn<Guid>(
                name: "ClientRequestId",
                table: "student_academic_requests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ExecutionDataJson",
                table: "student_academic_requests",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "FulfilledAtUtc",
                table: "student_academic_requests",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "FulfilledBy",
                table: "student_academic_requests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FulfillmentResult",
                table: "student_academic_requests",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ArtifactDigest",
                table: "issued_academic_documents",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ArtifactKey",
                table: "issued_academic_documents",
                type: "character varying(400)",
                maxLength: 400,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LegacyProof",
                table: "issued_academic_documents",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SourceInvalidatedAtUtc",
                table: "issued_academic_documents",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SupersededById",
                table: "issued_academic_documents",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TemplateId",
                table: "issued_academic_documents",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "VerificationKeyId",
                table: "issued_academic_documents",
                type: "uuid",
                nullable: true);

            migrationBuilder.AlterColumn<Guid>(
                name: "ImportRowId",
                table: "institutional_outbox_events",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<int>(
                name: "Attempts",
                table: "institutional_outbox_events",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "DeduplicationKey",
                table: "institutional_outbox_events",
                type: "character varying(150)",
                maxLength: 150,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "DeliveredAtUtc",
                table: "institutional_outbox_events",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LastError",
                table: "institutional_outbox_events",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "LeaseToken",
                table: "institutional_outbox_events",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LeaseUntilUtc",
                table: "institutional_outbox_events",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "NextAttemptAtUtc",
                table: "institutional_outbox_events",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "institutional_outbox_events",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "SourceInvalidatedAtUtc",
                table: "graduation_applications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_issued_academic_documents_Id_InstitutionId",
                table: "issued_academic_documents",
                columns: new[] { "Id", "InstitutionId" });

            migrationBuilder.Sql("""
                UPDATE institutional_outbox_events SET "Status"='Pending' WHERE "Status"='';
                UPDATE institutional_outbox_events SET "DeduplicationKey"='legacy:'||"Id"::text WHERE "DeduplicationKey"='';
                """);

            migrationBuilder.CreateTable(
                name: "institutional_document_templates",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    Heading = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Footer = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    AccentColor = table.Column<string>(type: "character varying(7)", maxLength: 7, nullable: false),
                    FontFamily = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    RightToLeft = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    PublishedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_institutional_document_templates", x => x.Id);
                    table.UniqueConstraint("AK_institutional_document_templates_Id_InstitutionId", x => new { x.Id, x.InstitutionId });
                    table.CheckConstraint("CK_document_template_revision", "\"Revision\" > 0");
                    table.ForeignKey(
                        name: "FK_institutional_document_templates_institutions_InstitutionId",
                        column: x => x.InstitutionId,
                        principalTable: "institutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "institutional_event_receipts",
                columns: table => new
                {
                    EventId = table.Column<Guid>(type: "uuid", nullable: false),
                    InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PayloadJson = table.Column<string>(type: "jsonb", nullable: false),
                    ReceivedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_institutional_event_receipts", x => x.EventId);
                    table.ForeignKey(
                        name: "FK_institutional_event_receipts_institutional_outbox_events_Ev~",
                        column: x => x.EventId,
                        principalTable: "institutional_outbox_events",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "institutional_verification_keys",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ProtectedSecret = table.Column<string>(type: "text", nullable: false),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RetiredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RevokedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_institutional_verification_keys", x => x.Id);
                    table.UniqueConstraint("AK_institutional_verification_keys_Id_InstitutionId", x => new { x.Id, x.InstitutionId });
                    table.ForeignKey(
                        name: "FK_institutional_verification_keys_institutions_InstitutionId",
                        column: x => x.InstitutionId,
                        principalTable: "institutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "registrar_applicants",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    NormalizedEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    FullName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ProgramId = table.Column<Guid>(type: "uuid", nullable: false),
                    TermId = table.Column<Guid>(type: "uuid", nullable: false),
                    Statement = table.Column<string>(type: "character varying(3000)", maxLength: 3000, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ProtectedInvitation = table.Column<string>(type: "text", nullable: false),
                    InvitationHash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    InvitationExpiresAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    VerifiedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: true),
                    AdmissionId = table.Column<Guid>(type: "uuid", nullable: true),
                    DecisionReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_registrar_applicants", x => x.Id);
                    table.UniqueConstraint("AK_registrar_applicants_Id_InstitutionId", x => new { x.Id, x.InstitutionId });
                    table.CheckConstraint("CK_applicant_status", "\"Status\" IN ('Invited','Submitted','Accepted','Rejected','Converted')");
                    table.ForeignKey(
                        name: "FK_registrar_applicants_academic_programs_ProgramId_Institutio~",
                        columns: x => new { x.ProgramId, x.InstitutionId },
                        principalTable: "academic_programs",
                        principalColumns: new[] { "Id", "InstitutionId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_registrar_applicants_academic_terms_TermId_InstitutionId",
                        columns: x => new { x.TermId, x.InstitutionId },
                        principalTable: "academic_terms",
                        principalColumns: new[] { "Id", "InstitutionId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_registrar_applicants_identity_users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "identity_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_registrar_applicants_identity_users_ReviewedBy",
                        column: x => x.ReviewedBy,
                        principalTable: "identity_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_registrar_applicants_identity_users_StudentId",
                        column: x => x.StudentId,
                        principalTable: "identity_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_registrar_applicants_institutions_InstitutionId",
                        column: x => x.InstitutionId,
                        principalTable: "institutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "registrar_curriculum_transitions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    FromPlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    ToPlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    MappingJson = table.Column<string>(type: "jsonb", nullable: false),
                    EffectiveAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RequestedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ApprovedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    AppliedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PriorAssignmentJson = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_registrar_curriculum_transitions", x => x.Id);
                    table.UniqueConstraint("AK_registrar_curriculum_transitions_Id_InstitutionId", x => new { x.Id, x.InstitutionId });
                    table.CheckConstraint("CK_transition_status", "\"Status\" IN ('Pending','Approved','Rejected','Applied') AND \"FromPlanId\" <> \"ToPlanId\"");
                    table.ForeignKey(
                        name: "FK_registrar_curriculum_transitions_curriculum_plans_FromPlanI~",
                        columns: x => new { x.FromPlanId, x.InstitutionId },
                        principalTable: "curriculum_plans",
                        principalColumns: new[] { "Id", "InstitutionId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_registrar_curriculum_transitions_curriculum_plans_ToPlanId_~",
                        columns: x => new { x.ToPlanId, x.InstitutionId },
                        principalTable: "curriculum_plans",
                        principalColumns: new[] { "Id", "InstitutionId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_registrar_curriculum_transitions_identity_users_StudentId",
                        column: x => x.StudentId,
                        principalTable: "identity_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_registrar_curriculum_transitions_institutions_InstitutionId",
                        column: x => x.InstitutionId,
                        principalTable: "institutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "registrar_student_profiles",
                columns: table => new
                {
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    FullName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ExternalReference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_registrar_student_profiles", x => x.StudentId);
                    table.UniqueConstraint("AK_registrar_student_profiles_StudentId_InstitutionId", x => new { x.StudentId, x.InstitutionId });
                    table.ForeignKey(
                        name: "FK_registrar_student_profiles_identity_users_StudentId",
                        column: x => x.StudentId,
                        principalTable: "identity_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_registrar_student_profiles_institutions_InstitutionId",
                        column: x => x.InstitutionId,
                        principalTable: "institutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "registrar_applicant_history",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ApplicantId = table.Column<Guid>(type: "uuid", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: true),
                    AtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_registrar_applicant_history", x => x.Id);
                    table.UniqueConstraint("AK_registrar_applicant_history_Id_InstitutionId", x => new { x.Id, x.InstitutionId });
                    table.ForeignKey(
                        name: "FK_registrar_applicant_history_institutions_InstitutionId",
                        column: x => x.InstitutionId,
                        principalTable: "institutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_registrar_applicant_history_registrar_applicants_ApplicantI~",
                        columns: x => new { x.ApplicantId, x.InstitutionId },
                        principalTable: "registrar_applicants",
                        principalColumns: new[] { "Id", "InstitutionId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "registrar_recognized_credits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    TransitionId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceCourseId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetCourseId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    CreditHours = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_registrar_recognized_credits", x => x.Id);
                    table.UniqueConstraint("AK_registrar_recognized_credits_Id_InstitutionId", x => new { x.Id, x.InstitutionId });
                    table.ForeignKey(
                        name: "FK_registrar_recognized_credits_courses_SourceCourseId_Institu~",
                        columns: x => new { x.SourceCourseId, x.InstitutionId },
                        principalTable: "courses",
                        principalColumns: new[] { "id", "institution_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_registrar_recognized_credits_courses_TargetCourseId_Institu~",
                        columns: x => new { x.TargetCourseId, x.InstitutionId },
                        principalTable: "courses",
                        principalColumns: new[] { "id", "institution_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_registrar_recognized_credits_curriculum_plans_PlanId_Instit~",
                        columns: x => new { x.PlanId, x.InstitutionId },
                        principalTable: "curriculum_plans",
                        principalColumns: new[] { "Id", "InstitutionId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_registrar_recognized_credits_institutions_InstitutionId",
                        column: x => x.InstitutionId,
                        principalTable: "institutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_registrar_recognized_credits_registrar_curriculum_transitio~",
                        columns: x => new { x.TransitionId, x.InstitutionId },
                        principalTable: "registrar_curriculum_transitions",
                        principalColumns: new[] { "Id", "InstitutionId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_student_academic_requests_InstitutionId_StudentId_ClientReq~",
                table: "student_academic_requests",
                columns: new[] { "InstitutionId", "StudentId", "ClientRequestId" },
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_academic_request_status",
                table: "student_academic_requests",
                sql: "\"Status\" IN ('Pending','UnderReview','Approved','Rejected','Cancelled','Completed')");

            migrationBuilder.CreateIndex(
                name: "IX_issued_academic_documents_SupersededById_InstitutionId",
                table: "issued_academic_documents",
                columns: new[] { "SupersededById", "InstitutionId" });

            migrationBuilder.CreateIndex(
                name: "IX_issued_academic_documents_TemplateId_InstitutionId",
                table: "issued_academic_documents",
                columns: new[] { "TemplateId", "InstitutionId" });

            migrationBuilder.CreateIndex(
                name: "IX_issued_academic_documents_VerificationKeyId_InstitutionId",
                table: "issued_academic_documents",
                columns: new[] { "VerificationKeyId", "InstitutionId" });

            migrationBuilder.CreateIndex(
                name: "IX_institutional_outbox_events_InstitutionId_DeduplicationKey",
                table: "institutional_outbox_events",
                columns: new[] { "InstitutionId", "DeduplicationKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_institutional_outbox_events_Status_NextAttemptAtUtc",
                table: "institutional_outbox_events",
                columns: new[] { "Status", "NextAttemptAtUtc" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_outbox_delivery",
                table: "institutional_outbox_events",
                sql: "\"Status\" IN ('Pending','Processing','Retry','Delivered','Cancelled') AND \"Attempts\" >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_request_history_to",
                table: "academic_request_history",
                sql: "\"ToStatus\" IN ('UnderReview','Approved','Rejected','Cancelled','Completed')");

            migrationBuilder.CreateIndex(
                name: "IX_institutional_document_templates_InstitutionId_Kind_Revision",
                table: "institutional_document_templates",
                columns: new[] { "InstitutionId", "Kind", "Revision" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_institutional_event_receipts_InstitutionId_ReceivedAtUtc",
                table: "institutional_event_receipts",
                columns: new[] { "InstitutionId", "ReceivedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_institutional_verification_keys_InstitutionId",
                table: "institutional_verification_keys",
                column: "InstitutionId",
                unique: true,
                filter: "\"RetiredAtUtc\" IS NULL AND \"RevokedAtUtc\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_registrar_applicant_history_ApplicantId_InstitutionId",
                table: "registrar_applicant_history",
                columns: new[] { "ApplicantId", "InstitutionId" });

            migrationBuilder.CreateIndex(
                name: "IX_registrar_applicant_history_InstitutionId",
                table: "registrar_applicant_history",
                column: "InstitutionId");

            migrationBuilder.CreateIndex(
                name: "IX_registrar_applicants_CreatedBy",
                table: "registrar_applicants",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_registrar_applicants_InstitutionId_ClientRequestId",
                table: "registrar_applicants",
                columns: new[] { "InstitutionId", "ClientRequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_registrar_applicants_InstitutionId_NormalizedEmail",
                table: "registrar_applicants",
                columns: new[] { "InstitutionId", "NormalizedEmail" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_registrar_applicants_InvitationHash",
                table: "registrar_applicants",
                column: "InvitationHash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_registrar_applicants_ProgramId_InstitutionId",
                table: "registrar_applicants",
                columns: new[] { "ProgramId", "InstitutionId" });

            migrationBuilder.CreateIndex(
                name: "IX_registrar_applicants_ReviewedBy",
                table: "registrar_applicants",
                column: "ReviewedBy");

            migrationBuilder.CreateIndex(
                name: "IX_registrar_applicants_StudentId",
                table: "registrar_applicants",
                column: "StudentId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_registrar_applicants_TermId_InstitutionId",
                table: "registrar_applicants",
                columns: new[] { "TermId", "InstitutionId" });

            migrationBuilder.CreateIndex(
                name: "IX_registrar_curriculum_transitions_FromPlanId_InstitutionId",
                table: "registrar_curriculum_transitions",
                columns: new[] { "FromPlanId", "InstitutionId" });

            migrationBuilder.CreateIndex(
                name: "IX_registrar_curriculum_transitions_InstitutionId_StudentId_Cl~",
                table: "registrar_curriculum_transitions",
                columns: new[] { "InstitutionId", "StudentId", "ClientRequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_registrar_curriculum_transitions_StudentId",
                table: "registrar_curriculum_transitions",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_registrar_curriculum_transitions_ToPlanId_InstitutionId",
                table: "registrar_curriculum_transitions",
                columns: new[] { "ToPlanId", "InstitutionId" });

            migrationBuilder.CreateIndex(
                name: "IX_registrar_recognized_credits_InstitutionId_StudentId_PlanId~",
                table: "registrar_recognized_credits",
                columns: new[] { "InstitutionId", "StudentId", "PlanId", "TargetCourseId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_registrar_recognized_credits_PlanId_InstitutionId",
                table: "registrar_recognized_credits",
                columns: new[] { "PlanId", "InstitutionId" });

            migrationBuilder.CreateIndex(
                name: "IX_registrar_recognized_credits_SourceCourseId_InstitutionId",
                table: "registrar_recognized_credits",
                columns: new[] { "SourceCourseId", "InstitutionId" });

            migrationBuilder.CreateIndex(
                name: "IX_registrar_recognized_credits_TargetCourseId_InstitutionId",
                table: "registrar_recognized_credits",
                columns: new[] { "TargetCourseId", "InstitutionId" });

            migrationBuilder.CreateIndex(
                name: "IX_registrar_recognized_credits_TransitionId_InstitutionId",
                table: "registrar_recognized_credits",
                columns: new[] { "TransitionId", "InstitutionId" });

            migrationBuilder.CreateIndex(
                name: "IX_registrar_recognized_credits_TransitionId_SourceCourseId",
                table: "registrar_recognized_credits",
                columns: new[] { "TransitionId", "SourceCourseId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_registrar_student_profiles_InstitutionId",
                table: "registrar_student_profiles",
                column: "InstitutionId");

            migrationBuilder.AddForeignKey(
                name: "FK_institutional_outbox_events_institutions_InstitutionId",
                table: "institutional_outbox_events",
                column: "InstitutionId",
                principalTable: "institutions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_issued_academic_documents_institutional_document_templates_~",
                table: "issued_academic_documents",
                columns: new[] { "TemplateId", "InstitutionId" },
                principalTable: "institutional_document_templates",
                principalColumns: new[] { "Id", "InstitutionId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_issued_academic_documents_institutional_verification_keys_V~",
                table: "issued_academic_documents",
                columns: new[] { "VerificationKeyId", "InstitutionId" },
                principalTable: "institutional_verification_keys",
                principalColumns: new[] { "Id", "InstitutionId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_issued_academic_documents_issued_academic_documents_Superse~",
                table: "issued_academic_documents",
                columns: new[] { "SupersededById", "InstitutionId" },
                principalTable: "issued_academic_documents",
                principalColumns: new[] { "Id", "InstitutionId" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_institutional_outbox_events_institutions_InstitutionId",
                table: "institutional_outbox_events");

            migrationBuilder.DropForeignKey(
                name: "FK_issued_academic_documents_institutional_document_templates_~",
                table: "issued_academic_documents");

            migrationBuilder.DropForeignKey(
                name: "FK_issued_academic_documents_institutional_verification_keys_V~",
                table: "issued_academic_documents");

            migrationBuilder.DropForeignKey(
                name: "FK_issued_academic_documents_issued_academic_documents_Superse~",
                table: "issued_academic_documents");

            migrationBuilder.DropTable(
                name: "institutional_document_templates");

            migrationBuilder.DropTable(
                name: "institutional_event_receipts");

            migrationBuilder.DropTable(
                name: "institutional_verification_keys");

            migrationBuilder.DropTable(
                name: "registrar_applicant_history");

            migrationBuilder.DropTable(
                name: "registrar_recognized_credits");

            migrationBuilder.DropTable(
                name: "registrar_student_profiles");

            migrationBuilder.DropTable(
                name: "registrar_applicants");

            migrationBuilder.DropTable(
                name: "registrar_curriculum_transitions");

            migrationBuilder.DropIndex(
                name: "IX_student_academic_requests_InstitutionId_StudentId_ClientReq~",
                table: "student_academic_requests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_academic_request_status",
                table: "student_academic_requests");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_issued_academic_documents_Id_InstitutionId",
                table: "issued_academic_documents");

            migrationBuilder.DropIndex(
                name: "IX_issued_academic_documents_SupersededById_InstitutionId",
                table: "issued_academic_documents");

            migrationBuilder.DropIndex(
                name: "IX_issued_academic_documents_TemplateId_InstitutionId",
                table: "issued_academic_documents");

            migrationBuilder.DropIndex(
                name: "IX_issued_academic_documents_VerificationKeyId_InstitutionId",
                table: "issued_academic_documents");

            migrationBuilder.DropIndex(
                name: "IX_institutional_outbox_events_InstitutionId_DeduplicationKey",
                table: "institutional_outbox_events");

            migrationBuilder.DropIndex(
                name: "IX_institutional_outbox_events_Status_NextAttemptAtUtc",
                table: "institutional_outbox_events");

            migrationBuilder.DropCheckConstraint(
                name: "CK_outbox_delivery",
                table: "institutional_outbox_events");

            migrationBuilder.DropCheckConstraint(
                name: "CK_request_history_to",
                table: "academic_request_history");

            migrationBuilder.DropColumn(
                name: "ClientRequestId",
                table: "student_academic_requests");

            migrationBuilder.DropColumn(
                name: "ExecutionDataJson",
                table: "student_academic_requests");

            migrationBuilder.DropColumn(
                name: "FulfilledAtUtc",
                table: "student_academic_requests");

            migrationBuilder.DropColumn(
                name: "FulfilledBy",
                table: "student_academic_requests");

            migrationBuilder.DropColumn(
                name: "FulfillmentResult",
                table: "student_academic_requests");

            migrationBuilder.DropColumn(
                name: "ArtifactDigest",
                table: "issued_academic_documents");

            migrationBuilder.DropColumn(
                name: "ArtifactKey",
                table: "issued_academic_documents");

            migrationBuilder.DropColumn(
                name: "LegacyProof",
                table: "issued_academic_documents");

            migrationBuilder.DropColumn(
                name: "SourceInvalidatedAtUtc",
                table: "issued_academic_documents");

            migrationBuilder.DropColumn(
                name: "SupersededById",
                table: "issued_academic_documents");

            migrationBuilder.DropColumn(
                name: "TemplateId",
                table: "issued_academic_documents");

            migrationBuilder.DropColumn(
                name: "VerificationKeyId",
                table: "issued_academic_documents");

            migrationBuilder.DropColumn(
                name: "Attempts",
                table: "institutional_outbox_events");

            migrationBuilder.DropColumn(
                name: "DeduplicationKey",
                table: "institutional_outbox_events");

            migrationBuilder.DropColumn(
                name: "DeliveredAtUtc",
                table: "institutional_outbox_events");

            migrationBuilder.DropColumn(
                name: "LastError",
                table: "institutional_outbox_events");

            migrationBuilder.DropColumn(
                name: "LeaseToken",
                table: "institutional_outbox_events");

            migrationBuilder.DropColumn(
                name: "LeaseUntilUtc",
                table: "institutional_outbox_events");

            migrationBuilder.DropColumn(
                name: "NextAttemptAtUtc",
                table: "institutional_outbox_events");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "institutional_outbox_events");

            migrationBuilder.DropColumn(
                name: "SourceInvalidatedAtUtc",
                table: "graduation_applications");

            migrationBuilder.AlterColumn<Guid>(
                name: "ImportRowId",
                table: "institutional_outbox_events",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_academic_request_status",
                table: "student_academic_requests",
                sql: "\"Status\" IN ('Pending','Approved','Rejected','Cancelled')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_request_history_to",
                table: "academic_request_history",
                sql: "\"ToStatus\" IN ('Approved','Rejected','Cancelled')");
        }
    }
}
