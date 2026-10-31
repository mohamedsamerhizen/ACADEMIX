using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Academix.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase20AcademicAdvisingAndGraduation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_p16_graduation_status",
                table: "graduation_applications");

            migrationBuilder.AddColumn<string>(
                name: "ApprovalEvidenceJson",
                table: "graduation_applications",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ClientRequestId",
                table: "graduation_applications",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DepartmentReviewReason",
                table: "graduation_applications",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DepartmentReviewedAtUtc",
                table: "graduation_applications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "DepartmentReviewedById",
                table: "graduation_applications",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FinalEvidenceJson",
                table: "graduation_applications",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresDepartmentReview",
                table: "graduation_applications",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddUniqueConstraint(
                name: "AK_curriculum_courses_PlanId_CourseId_InstitutionId",
                table: "curriculum_courses",
                columns: new[] { "PlanId", "CourseId", "InstitutionId" });

            migrationBuilder.CreateTable(
                name: "academic_advising_policies",
                columns: table => new
                {
                    InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    MaximumAdvisees = table.Column<int>(type: "integer", nullable: true),
                    RepeatedFailureThreshold = table.Column<int>(type: "integer", nullable: false),
                    MinimumProgressCredits = table.Column<decimal>(type: "numeric", nullable: false),
                    RequireGoodStanding = table.Column<bool>(type: "boolean", nullable: false),
                    RequireDepartmentReview = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_academic_advising_policies", x => x.InstitutionId);
                    table.CheckConstraint("CK_p20_advising_policy", "(\"MaximumAdvisees\" IS NULL OR \"MaximumAdvisees\" BETWEEN 1 AND 10000) AND \"RepeatedFailureThreshold\" BETWEEN 2 AND 20 AND \"MinimumProgressCredits\" BETWEEN 0 AND 500");
                    table.ForeignKey(
                        name: "FK_academic_advising_policies_institutions_InstitutionId",
                        column: x => x.InstitutionId,
                        principalTable: "institutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "academic_advising_sessions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    AdvisorId = table.Column<Guid>(type: "uuid", nullable: false),
                    ClientRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    ScheduledAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    InternalNotes = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Recommendation = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    FollowUpAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AcknowledgedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_academic_advising_sessions", x => x.Id);
                    table.UniqueConstraint("AK_academic_advising_sessions_Id_InstitutionId", x => new { x.Id, x.InstitutionId });
                    table.CheckConstraint("CK_p20_session", "\"Status\" IN ('Scheduled','Completed','Cancelled') AND \"StudentId\" <> \"AdvisorId\" AND (\"AcknowledgedAtUtc\" IS NULL OR \"Status\" = 'Completed')");
                    table.ForeignKey(
                        name: "FK_academic_advising_sessions_identity_users_AdvisorId",
                        column: x => x.AdvisorId,
                        principalTable: "identity_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_academic_advising_sessions_identity_users_StudentId",
                        column: x => x.StudentId,
                        principalTable: "identity_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_academic_advising_sessions_institutions_InstitutionId",
                        column: x => x.InstitutionId,
                        principalTable: "institutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "academic_advisor_assignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    AdvisorId = table.Column<Guid>(type: "uuid", nullable: false),
                    AssignedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    AssignedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_academic_advisor_assignments", x => x.Id);
                    table.UniqueConstraint("AK_academic_advisor_assignments_Id_InstitutionId", x => new { x.Id, x.InstitutionId });
                    table.CheckConstraint("CK_p20_advisor", "\"StudentId\" <> \"AdvisorId\" AND (\"EndedAtUtc\" IS NULL OR \"EndedAtUtc\" >= \"AssignedAtUtc\")");
                    table.ForeignKey(
                        name: "FK_academic_advisor_assignments_identity_users_AdvisorId",
                        column: x => x.AdvisorId,
                        principalTable: "identity_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_academic_advisor_assignments_identity_users_AssignedBy",
                        column: x => x.AssignedBy,
                        principalTable: "identity_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_academic_advisor_assignments_identity_users_StudentId",
                        column: x => x.StudentId,
                        principalTable: "identity_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_academic_advisor_assignments_institutions_InstitutionId",
                        column: x => x.InstitutionId,
                        principalTable: "institutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "academic_requirement_exceptions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetCourseId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceCourseId = table.Column<Guid>(type: "uuid", nullable: true),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    RequestedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    DecisionReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_academic_requirement_exceptions", x => x.Id);
                    table.UniqueConstraint("AK_academic_requirement_exceptions_Id_InstitutionId", x => new { x.Id, x.InstitutionId });
                    table.CheckConstraint("CK_p20_exception", "\"Status\" IN ('Submitted','Approved','Rejected') AND ((\"Kind\"='Waiver' AND \"SourceCourseId\" IS NULL) OR (\"Kind\"='Substitution' AND \"SourceCourseId\" IS NOT NULL AND \"SourceCourseId\" <> \"TargetCourseId\")) AND (\"Status\" <> 'Approved' OR (\"ReviewedBy\" IS NOT NULL AND \"ReviewedBy\" <> \"RequestedBy\" AND \"ReviewedBy\" <> \"StudentId\"))");
                    table.ForeignKey(
                        name: "FK_academic_requirement_exceptions_courses_SourceCourseId_Inst~",
                        columns: x => new { x.SourceCourseId, x.InstitutionId },
                        principalTable: "courses",
                        principalColumns: new[] { "id", "institution_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_academic_requirement_exceptions_curriculum_courses_PlanId_T~",
                        columns: x => new { x.PlanId, x.TargetCourseId, x.InstitutionId },
                        principalTable: "curriculum_courses",
                        principalColumns: new[] { "PlanId", "CourseId", "InstitutionId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_academic_requirement_exceptions_identity_users_RequestedBy",
                        column: x => x.RequestedBy,
                        principalTable: "identity_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_academic_requirement_exceptions_identity_users_ReviewedBy",
                        column: x => x.ReviewedBy,
                        principalTable: "identity_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_academic_requirement_exceptions_identity_users_StudentId",
                        column: x => x.StudentId,
                        principalTable: "identity_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_academic_requirement_exceptions_institutions_InstitutionId",
                        column: x => x.InstitutionId,
                        principalTable: "institutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "curriculum_elective_groups",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    MinimumCredits = table.Column<decimal>(type: "numeric", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_curriculum_elective_groups", x => x.Id);
                    table.UniqueConstraint("AK_curriculum_elective_groups_Id_InstitutionId", x => new { x.Id, x.InstitutionId });
                    table.UniqueConstraint("AK_curriculum_elective_groups_Id_PlanId_InstitutionId", x => new { x.Id, x.PlanId, x.InstitutionId });
                    table.CheckConstraint("CK_p20_group_credits", "\"MinimumCredits\" > 0 AND \"MinimumCredits\" <= 500");
                    table.ForeignKey(
                        name: "FK_curriculum_elective_groups_curriculum_plans_PlanId_Institut~",
                        columns: x => new { x.PlanId, x.InstitutionId },
                        principalTable: "curriculum_plans",
                        principalColumns: new[] { "Id", "InstitutionId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_curriculum_elective_groups_institutions_InstitutionId",
                        column: x => x.InstitutionId,
                        principalTable: "institutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "department_graduation_reviewers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    DepartmentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewerId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_department_graduation_reviewers", x => x.Id);
                    table.UniqueConstraint("AK_department_graduation_reviewers_Id_InstitutionId", x => new { x.Id, x.InstitutionId });
                    table.ForeignKey(
                        name: "FK_department_graduation_reviewers_departments_DepartmentId_In~",
                        columns: x => new { x.DepartmentId, x.InstitutionId },
                        principalTable: "departments",
                        principalColumns: new[] { "Id", "InstitutionId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_department_graduation_reviewers_identity_users_ReviewerId",
                        column: x => x.ReviewerId,
                        principalTable: "identity_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_department_graduation_reviewers_institutions_InstitutionId",
                        column: x => x.InstitutionId,
                        principalTable: "institutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "curriculum_elective_group_courses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    GroupId = table.Column<Guid>(type: "uuid", nullable: false),
                    CourseId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_curriculum_elective_group_courses", x => x.Id);
                    table.UniqueConstraint("AK_curriculum_elective_group_courses_Id_InstitutionId", x => new { x.Id, x.InstitutionId });
                    table.ForeignKey(
                        name: "FK_curriculum_elective_group_courses_curriculum_courses_PlanId~",
                        columns: x => new { x.PlanId, x.CourseId, x.InstitutionId },
                        principalTable: "curriculum_courses",
                        principalColumns: new[] { "PlanId", "CourseId", "InstitutionId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_curriculum_elective_group_courses_curriculum_elective_group~",
                        columns: x => new { x.GroupId, x.PlanId, x.InstitutionId },
                        principalTable: "curriculum_elective_groups",
                        principalColumns: new[] { "Id", "PlanId", "InstitutionId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_curriculum_elective_group_courses_institutions_InstitutionId",
                        column: x => x.InstitutionId,
                        principalTable: "institutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "student_study_plans",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    CurriculumPlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    CurrentRevision = table.Column<int>(type: "integer", nullable: false),
                    ApprovedRevision = table.Column<int>(type: "integer", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_student_study_plans", x => x.Id);
                    table.UniqueConstraint("AK_student_study_plans_Id_InstitutionId", x => new { x.Id, x.InstitutionId });
                    table.ForeignKey(
                        name: "FK_student_study_plans_curriculum_plans_CurriculumPlanId_Insti~",
                        columns: x => new { x.CurriculumPlanId, x.InstitutionId },
                        principalTable: "curriculum_plans",
                        principalColumns: new[] { "Id", "InstitutionId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_student_study_plans_identity_users_StudentId",
                        column: x => x.StudentId,
                        principalTable: "identity_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_student_study_plans_institutions_InstitutionId",
                        column: x => x.InstitutionId,
                        principalTable: "institutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "study_plan_revisions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudyPlanId = table.Column<Guid>(type: "uuid", nullable: false),
                    Revision = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Rationale = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Recommendation = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    ReviewedBy = table.Column<Guid>(type: "uuid", nullable: true),
                    CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AcknowledgedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_study_plan_revisions", x => x.Id);
                    table.UniqueConstraint("AK_study_plan_revisions_Id_InstitutionId", x => new { x.Id, x.InstitutionId });
                    table.UniqueConstraint("AK_study_plan_revisions_StudyPlanId_Revision_InstitutionId", x => new { x.StudyPlanId, x.Revision, x.InstitutionId });
                    table.CheckConstraint("CK_p20_plan_revision", "\"Revision\" > 0 AND \"Status\" IN ('Proposed','Approved','Rejected','Acknowledged') AND (\"Status\" NOT IN ('Approved','Acknowledged') OR \"ReviewedBy\" IS NOT NULL) AND (\"Status\" <> 'Acknowledged' OR \"AcknowledgedAtUtc\" IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_study_plan_revisions_identity_users_CreatedBy",
                        column: x => x.CreatedBy,
                        principalTable: "identity_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_study_plan_revisions_identity_users_ReviewedBy",
                        column: x => x.ReviewedBy,
                        principalTable: "identity_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_study_plan_revisions_institutions_InstitutionId",
                        column: x => x.InstitutionId,
                        principalTable: "institutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_study_plan_revisions_student_study_plans_StudyPlanId_Instit~",
                        columns: x => new { x.StudyPlanId, x.InstitutionId },
                        principalTable: "student_study_plans",
                        principalColumns: new[] { "Id", "InstitutionId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "study_plan_courses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    RevisionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CourseId = table.Column<Guid>(type: "uuid", nullable: false),
                    TermId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_study_plan_courses", x => x.Id);
                    table.UniqueConstraint("AK_study_plan_courses_Id_InstitutionId", x => new { x.Id, x.InstitutionId });
                    table.ForeignKey(
                        name: "FK_study_plan_courses_academic_terms_TermId_InstitutionId",
                        columns: x => new { x.TermId, x.InstitutionId },
                        principalTable: "academic_terms",
                        principalColumns: new[] { "Id", "InstitutionId" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_study_plan_courses_courses_CourseId_InstitutionId",
                        columns: x => new { x.CourseId, x.InstitutionId },
                        principalTable: "courses",
                        principalColumns: new[] { "id", "institution_id" },
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_study_plan_courses_institutions_InstitutionId",
                        column: x => x.InstitutionId,
                        principalTable: "institutions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_study_plan_courses_study_plan_revisions_RevisionId_Institut~",
                        columns: x => new { x.RevisionId, x.InstitutionId },
                        principalTable: "study_plan_revisions",
                        principalColumns: new[] { "Id", "InstitutionId" },
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_graduation_applications_DepartmentReviewedById",
                table: "graduation_applications",
                column: "DepartmentReviewedById");

            migrationBuilder.CreateIndex(
                name: "IX_graduation_applications_InstitutionId_StudentId_ClientReque~",
                table: "graduation_applications",
                columns: new[] { "InstitutionId", "StudentId", "ClientRequestId" },
                unique: true,
                filter: "\"ClientRequestId\" IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_p16_graduation_status",
                table: "graduation_applications",
                sql: "\"Status\" IN ('Submitted','UnderReview','Approved','Rejected','Issued')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_p20_graduation_evidence",
                table: "graduation_applications",
                sql: "\"ClientRequestId\" IS NULL OR \"Status\" <> 'Issued' OR \"FinalEvidenceJson\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_academic_advising_sessions_AdvisorId",
                table: "academic_advising_sessions",
                column: "AdvisorId");

            migrationBuilder.CreateIndex(
                name: "IX_academic_advising_sessions_InstitutionId_StudentId_ClientRe~",
                table: "academic_advising_sessions",
                columns: new[] { "InstitutionId", "StudentId", "ClientRequestId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_academic_advising_sessions_InstitutionId_StudentId_Schedule~",
                table: "academic_advising_sessions",
                columns: new[] { "InstitutionId", "StudentId", "ScheduledAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_academic_advising_sessions_StudentId",
                table: "academic_advising_sessions",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_academic_advisor_assignments_AdvisorId",
                table: "academic_advisor_assignments",
                column: "AdvisorId");

            migrationBuilder.CreateIndex(
                name: "IX_academic_advisor_assignments_AssignedBy",
                table: "academic_advisor_assignments",
                column: "AssignedBy");

            migrationBuilder.CreateIndex(
                name: "IX_academic_advisor_assignments_InstitutionId_AdvisorId_EndedA~",
                table: "academic_advisor_assignments",
                columns: new[] { "InstitutionId", "AdvisorId", "EndedAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_academic_advisor_assignments_InstitutionId_StudentId",
                table: "academic_advisor_assignments",
                columns: new[] { "InstitutionId", "StudentId" },
                unique: true,
                filter: "\"EndedAtUtc\" IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_academic_advisor_assignments_StudentId",
                table: "academic_advisor_assignments",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_academic_requirement_exceptions_InstitutionId_StudentId_Pl~1",
                table: "academic_requirement_exceptions",
                columns: new[] { "InstitutionId", "StudentId", "PlanId", "TargetCourseId" },
                unique: true,
                filter: "\"Status\" IN ('Submitted','Approved')");

            migrationBuilder.CreateIndex(
                name: "IX_academic_requirement_exceptions_InstitutionId_StudentId_Pla~",
                table: "academic_requirement_exceptions",
                columns: new[] { "InstitutionId", "StudentId", "PlanId", "SourceCourseId" },
                unique: true,
                filter: "\"Status\" = 'Approved' AND \"SourceCourseId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_academic_requirement_exceptions_PlanId_TargetCourseId_Insti~",
                table: "academic_requirement_exceptions",
                columns: new[] { "PlanId", "TargetCourseId", "InstitutionId" });

            migrationBuilder.CreateIndex(
                name: "IX_academic_requirement_exceptions_RequestedBy",
                table: "academic_requirement_exceptions",
                column: "RequestedBy");

            migrationBuilder.CreateIndex(
                name: "IX_academic_requirement_exceptions_ReviewedBy",
                table: "academic_requirement_exceptions",
                column: "ReviewedBy");

            migrationBuilder.CreateIndex(
                name: "IX_academic_requirement_exceptions_SourceCourseId_InstitutionId",
                table: "academic_requirement_exceptions",
                columns: new[] { "SourceCourseId", "InstitutionId" });

            migrationBuilder.CreateIndex(
                name: "IX_academic_requirement_exceptions_StudentId",
                table: "academic_requirement_exceptions",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_curriculum_elective_group_courses_GroupId_PlanId_Institutio~",
                table: "curriculum_elective_group_courses",
                columns: new[] { "GroupId", "PlanId", "InstitutionId" });

            migrationBuilder.CreateIndex(
                name: "IX_curriculum_elective_group_courses_InstitutionId",
                table: "curriculum_elective_group_courses",
                column: "InstitutionId");

            migrationBuilder.CreateIndex(
                name: "IX_curriculum_elective_group_courses_PlanId_CourseId",
                table: "curriculum_elective_group_courses",
                columns: new[] { "PlanId", "CourseId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_curriculum_elective_group_courses_PlanId_CourseId_Instituti~",
                table: "curriculum_elective_group_courses",
                columns: new[] { "PlanId", "CourseId", "InstitutionId" });

            migrationBuilder.CreateIndex(
                name: "IX_curriculum_elective_groups_InstitutionId",
                table: "curriculum_elective_groups",
                column: "InstitutionId");

            migrationBuilder.CreateIndex(
                name: "IX_curriculum_elective_groups_PlanId_InstitutionId",
                table: "curriculum_elective_groups",
                columns: new[] { "PlanId", "InstitutionId" });

            migrationBuilder.CreateIndex(
                name: "IX_curriculum_elective_groups_PlanId_Name",
                table: "curriculum_elective_groups",
                columns: new[] { "PlanId", "Name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_department_graduation_reviewers_DepartmentId_InstitutionId",
                table: "department_graduation_reviewers",
                columns: new[] { "DepartmentId", "InstitutionId" });

            migrationBuilder.CreateIndex(
                name: "IX_department_graduation_reviewers_DepartmentId_ReviewerId",
                table: "department_graduation_reviewers",
                columns: new[] { "DepartmentId", "ReviewerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_department_graduation_reviewers_InstitutionId",
                table: "department_graduation_reviewers",
                column: "InstitutionId");

            migrationBuilder.CreateIndex(
                name: "IX_department_graduation_reviewers_ReviewerId",
                table: "department_graduation_reviewers",
                column: "ReviewerId");

            migrationBuilder.CreateIndex(
                name: "IX_student_study_plans_CurriculumPlanId_InstitutionId",
                table: "student_study_plans",
                columns: new[] { "CurriculumPlanId", "InstitutionId" });

            migrationBuilder.CreateIndex(
                name: "IX_student_study_plans_Id_ApprovedRevision_InstitutionId",
                table: "student_study_plans",
                columns: new[] { "Id", "ApprovedRevision", "InstitutionId" });

            migrationBuilder.CreateIndex(
                name: "IX_student_study_plans_InstitutionId_StudentId",
                table: "student_study_plans",
                columns: new[] { "InstitutionId", "StudentId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_student_study_plans_StudentId",
                table: "student_study_plans",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_study_plan_courses_CourseId_InstitutionId",
                table: "study_plan_courses",
                columns: new[] { "CourseId", "InstitutionId" });

            migrationBuilder.CreateIndex(
                name: "IX_study_plan_courses_InstitutionId",
                table: "study_plan_courses",
                column: "InstitutionId");

            migrationBuilder.CreateIndex(
                name: "IX_study_plan_courses_RevisionId_CourseId",
                table: "study_plan_courses",
                columns: new[] { "RevisionId", "CourseId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_study_plan_courses_RevisionId_InstitutionId",
                table: "study_plan_courses",
                columns: new[] { "RevisionId", "InstitutionId" });

            migrationBuilder.CreateIndex(
                name: "IX_study_plan_courses_TermId_InstitutionId",
                table: "study_plan_courses",
                columns: new[] { "TermId", "InstitutionId" });

            migrationBuilder.CreateIndex(
                name: "IX_study_plan_revisions_CreatedBy",
                table: "study_plan_revisions",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_study_plan_revisions_InstitutionId",
                table: "study_plan_revisions",
                column: "InstitutionId");

            migrationBuilder.CreateIndex(
                name: "IX_study_plan_revisions_ReviewedBy",
                table: "study_plan_revisions",
                column: "ReviewedBy");

            migrationBuilder.CreateIndex(
                name: "IX_study_plan_revisions_StudyPlanId_InstitutionId",
                table: "study_plan_revisions",
                columns: new[] { "StudyPlanId", "InstitutionId" });

            migrationBuilder.CreateIndex(
                name: "IX_study_plan_revisions_StudyPlanId_Revision",
                table: "study_plan_revisions",
                columns: new[] { "StudyPlanId", "Revision" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_graduation_applications_identity_users_DepartmentReviewedBy~",
                table: "graduation_applications",
                column: "DepartmentReviewedById",
                principalTable: "identity_users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_student_study_plans_study_plan_revisions_Id_ApprovedRevisio~",
                table: "student_study_plans",
                columns: new[] { "Id", "ApprovedRevision", "InstitutionId" },
                principalTable: "study_plan_revisions",
                principalColumns: new[] { "StudyPlanId", "Revision", "InstitutionId" },
                onDelete: ReferentialAction.Restrict);
            migrationBuilder.AddColumn<Guid>(name: "CurriculumPlanId", table: "study_plan_revisions", type: "uuid", nullable: false);
            migrationBuilder.CreateIndex(name: "IX_study_plan_revisions_CurriculumPlanId_InstitutionId",
                table: "study_plan_revisions", columns: new[] { "CurriculumPlanId", "InstitutionId" });
            migrationBuilder.AddForeignKey(name: "FK_study_plan_revisions_curriculum_plans_CurriculumPlanId_Inst~",
                table: "study_plan_revisions", columns: new[] { "CurriculumPlanId", "InstitutionId" },
                principalTable: "curriculum_plans", principalColumns: new[] { "Id", "InstitutionId" }, onDelete: ReferentialAction.Restrict);
            migrationBuilder.Sql("""
CREATE FUNCTION p20_user_scope() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE field_name text; user_id uuid; scope_id uuid;
BEGIN
 FOREACH field_name IN ARRAY TG_ARGV LOOP
  user_id := (to_jsonb(NEW)->>field_name)::uuid;
  IF user_id IS NOT NULL THEN
   SELECT "InstitutionId" INTO scope_id FROM identity_users WHERE "Id"=user_id;
   IF scope_id IS DISTINCT FROM NEW."InstitutionId" AND
      (field_name IN ('StudentId','AdvisorId','ReviewerId') OR NOT EXISTS
       (SELECT 1 FROM "AspNetUserRoles" ur JOIN identity_roles r ON r."Id"=ur."RoleId"
        WHERE ur."UserId"=user_id AND r."Name"='SuperAdministrator'))
   THEN RAISE EXCEPTION 'Academic progress user belongs to a different institution' USING ERRCODE='23514'; END IF;
  END IF;
 END LOOP; RETURN NEW;
END $$;
CREATE TRIGGER p20_assignment_scope BEFORE INSERT OR UPDATE ON academic_advisor_assignments FOR EACH ROW EXECUTE FUNCTION p20_user_scope('StudentId','AdvisorId','AssignedBy');
CREATE TRIGGER p20_session_scope BEFORE INSERT OR UPDATE ON academic_advising_sessions FOR EACH ROW EXECUTE FUNCTION p20_user_scope('StudentId','AdvisorId');
CREATE TRIGGER p20_study_scope BEFORE INSERT OR UPDATE ON student_study_plans FOR EACH ROW EXECUTE FUNCTION p20_user_scope('StudentId');
CREATE TRIGGER p20_revision_scope BEFORE INSERT OR UPDATE ON study_plan_revisions FOR EACH ROW EXECUTE FUNCTION p20_user_scope('CreatedBy','ReviewedBy');
CREATE TRIGGER p20_exception_scope BEFORE INSERT OR UPDATE ON academic_requirement_exceptions FOR EACH ROW EXECUTE FUNCTION p20_user_scope('StudentId','RequestedBy','ReviewedBy');
CREATE TRIGGER p20_department_scope BEFORE INSERT OR UPDATE ON department_graduation_reviewers FOR EACH ROW EXECUTE FUNCTION p20_user_scope('ReviewerId');
CREATE TRIGGER p20_graduation_scope BEFORE INSERT OR UPDATE ON graduation_applications FOR EACH ROW EXECUTE FUNCTION p20_user_scope('StudentId','ReviewedById','IssuedById','DepartmentReviewedById');

CREATE FUNCTION p20_elective_draft() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE plan_id uuid;
BEGIN
 IF TG_OP='DELETE' THEN plan_id:=OLD."PlanId"; ELSE plan_id:=NEW."PlanId"; END IF;
 IF NOT EXISTS(SELECT 1 FROM curriculum_plans WHERE "Id"=plan_id AND "Status"='Draft')
 THEN RAISE EXCEPTION 'Published elective groups are immutable' USING ERRCODE='23514'; END IF;
 IF TG_OP='UPDATE' AND OLD."PlanId" IS DISTINCT FROM NEW."PlanId" THEN RAISE EXCEPTION 'Group curriculum cannot change' USING ERRCODE='23514'; END IF;
 IF TG_TABLE_NAME='curriculum_elective_group_courses' AND TG_OP<>'DELETE' AND NOT EXISTS
    (SELECT 1 FROM curriculum_courses c WHERE c."PlanId"=NEW."PlanId" AND c."CourseId"=(to_jsonb(NEW)->>'CourseId')::uuid AND c."InstitutionId"=NEW."InstitutionId" AND c."RequirementKind"='Elective')
 THEN RAISE EXCEPTION 'Group members must be curriculum electives' USING ERRCODE='23514'; END IF;
 IF TG_OP='DELETE' THEN RETURN OLD; END IF; RETURN NEW;
END $$;
CREATE TRIGGER p20_group_draft BEFORE INSERT OR UPDATE OR DELETE ON curriculum_elective_groups FOR EACH ROW EXECUTE FUNCTION p20_elective_draft();
CREATE TRIGGER p20_group_member_draft BEFORE INSERT OR UPDATE OR DELETE ON curriculum_elective_group_courses FOR EACH ROW EXECUTE FUNCTION p20_elective_draft();

CREATE FUNCTION p20_history_guard() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
 IF TG_OP='DELETE' THEN RAISE EXCEPTION 'Academic progress history cannot be deleted' USING ERRCODE='23514'; END IF;
 IF TG_TABLE_NAME='study_plan_courses' OR
    TG_TABLE_NAME='study_plan_revisions' AND (to_jsonb(NEW)-ARRAY['Status','Recommendation','ReviewedBy','AcknowledgedAtUtc']) IS DISTINCT FROM (to_jsonb(OLD)-ARRAY['Status','Recommendation','ReviewedBy','AcknowledgedAtUtc']) OR
    TG_TABLE_NAME='academic_advisor_assignments' AND (to_jsonb(NEW)-'EndedAtUtc') IS DISTINCT FROM (to_jsonb(OLD)-'EndedAtUtc') OR
    TG_TABLE_NAME='academic_requirement_exceptions' AND ((to_jsonb(NEW)-ARRAY['Status','ReviewedBy','DecisionReason','Version']) IS DISTINCT FROM (to_jsonb(OLD)-ARRAY['Status','ReviewedBy','DecisionReason','Version']) OR to_jsonb(OLD)->>'Status'<>'Submitted')
 THEN RAISE EXCEPTION 'Academic progress evidence is immutable' USING ERRCODE='23514'; END IF;
 RETURN NEW;
END $$;
CREATE TRIGGER p20_revision_history BEFORE UPDATE OR DELETE ON study_plan_revisions FOR EACH ROW EXECUTE FUNCTION p20_history_guard();
CREATE TRIGGER p20_plan_course_history BEFORE UPDATE OR DELETE ON study_plan_courses FOR EACH ROW EXECUTE FUNCTION p20_history_guard();
CREATE TRIGGER p20_assignment_history BEFORE UPDATE OR DELETE ON academic_advisor_assignments FOR EACH ROW EXECUTE FUNCTION p20_history_guard();
CREATE TRIGGER p20_exception_history BEFORE UPDATE OR DELETE ON academic_requirement_exceptions FOR EACH ROW EXECUTE FUNCTION p20_history_guard();

CREATE FUNCTION p20_graduation_evidence_guard() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
 IF NEW."Status" IS DISTINCT FROM OLD."Status" AND NOT
    (OLD."Status"='Submitted' AND NEW."Status" IN ('UnderReview','Approved','Rejected') OR
     OLD."Status"='UnderReview' AND NEW."Status" IN ('Approved','Rejected') OR
     OLD."Status"='Approved' AND NEW."Status"='Issued')
 THEN RAISE EXCEPTION 'Invalid graduation status transition' USING ERRCODE='23514'; END IF;
 IF NEW."ClientRequestId" IS NOT NULL AND NEW."Status"='Approved' AND
    (NEW."ApprovalEvidenceJson" IS NULL OR NEW."RequiresDepartmentReview" AND
     (NEW."DepartmentReviewedAtUtc" IS NULL OR NEW."DepartmentReviewedById"=NEW."ReviewedById"))
 THEN RAISE EXCEPTION 'Graduation approval requires evidence and independent configured review' USING ERRCODE='23514'; END IF;
 IF OLD."ApprovalEvidenceJson" IS NOT NULL AND NEW."ApprovalEvidenceJson" IS DISTINCT FROM OLD."ApprovalEvidenceJson" OR
    OLD."FinalEvidenceJson" IS NOT NULL AND NEW."FinalEvidenceJson" IS DISTINCT FROM OLD."FinalEvidenceJson" OR
    OLD."Status"='Issued' AND ROW(NEW."Status",NEW."CredentialNumber",NEW."IssuedAtUtc",NEW."IssuedById",NEW."PlanId",NEW."StudentId",NEW."InstitutionId",NEW."ProgramId")
      IS DISTINCT FROM ROW(OLD."Status",OLD."CredentialNumber",OLD."IssuedAtUtc",OLD."IssuedById",OLD."PlanId",OLD."StudentId",OLD."InstitutionId",OLD."ProgramId")
 THEN RAISE EXCEPTION 'Official graduation evidence is immutable' USING ERRCODE='23514'; END IF;
 RETURN NEW;
END $$;
CREATE TRIGGER p20_graduation_evidence BEFORE UPDATE ON graduation_applications FOR EACH ROW EXECUTE FUNCTION p20_graduation_evidence_guard();
""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
DROP TRIGGER p20_graduation_scope ON graduation_applications;
DROP TRIGGER p20_graduation_evidence ON graduation_applications;
DROP TRIGGER p20_assignment_scope ON academic_advisor_assignments;
DROP TRIGGER p20_session_scope ON academic_advising_sessions;
DROP TRIGGER p20_study_scope ON student_study_plans;
DROP TRIGGER p20_revision_scope ON study_plan_revisions;
DROP TRIGGER p20_exception_scope ON academic_requirement_exceptions;
DROP TRIGGER p20_department_scope ON department_graduation_reviewers;
DROP TRIGGER p20_group_draft ON curriculum_elective_groups;
DROP TRIGGER p20_group_member_draft ON curriculum_elective_group_courses;
DROP TRIGGER p20_revision_history ON study_plan_revisions;
DROP TRIGGER p20_plan_course_history ON study_plan_courses;
DROP TRIGGER p20_assignment_history ON academic_advisor_assignments;
DROP TRIGGER p20_exception_history ON academic_requirement_exceptions;
DROP FUNCTION p20_user_scope(); DROP FUNCTION p20_elective_draft(); DROP FUNCTION p20_history_guard(); DROP FUNCTION p20_graduation_evidence_guard();
""");
            migrationBuilder.DropForeignKey(
                name: "FK_graduation_applications_identity_users_DepartmentReviewedBy~",
                table: "graduation_applications");

            migrationBuilder.DropForeignKey(
                name: "FK_student_study_plans_study_plan_revisions_Id_ApprovedRevisio~",
                table: "student_study_plans");

            migrationBuilder.DropTable(
                name: "academic_advising_policies");

            migrationBuilder.DropTable(
                name: "academic_advising_sessions");

            migrationBuilder.DropTable(
                name: "academic_advisor_assignments");

            migrationBuilder.DropTable(
                name: "academic_requirement_exceptions");

            migrationBuilder.DropTable(
                name: "curriculum_elective_group_courses");

            migrationBuilder.DropTable(
                name: "department_graduation_reviewers");

            migrationBuilder.DropTable(
                name: "study_plan_courses");

            migrationBuilder.DropTable(
                name: "curriculum_elective_groups");

            migrationBuilder.DropTable(
                name: "study_plan_revisions");

            migrationBuilder.DropTable(
                name: "student_study_plans");

            migrationBuilder.DropIndex(
                name: "IX_graduation_applications_DepartmentReviewedById",
                table: "graduation_applications");

            migrationBuilder.DropIndex(
                name: "IX_graduation_applications_InstitutionId_StudentId_ClientReque~",
                table: "graduation_applications");

            migrationBuilder.DropCheckConstraint(
                name: "CK_p16_graduation_status",
                table: "graduation_applications");

            migrationBuilder.DropCheckConstraint(
                name: "CK_p20_graduation_evidence",
                table: "graduation_applications");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_curriculum_courses_PlanId_CourseId_InstitutionId",
                table: "curriculum_courses");

            migrationBuilder.DropColumn(
                name: "ApprovalEvidenceJson",
                table: "graduation_applications");

            migrationBuilder.DropColumn(
                name: "ClientRequestId",
                table: "graduation_applications");

            migrationBuilder.DropColumn(
                name: "DepartmentReviewReason",
                table: "graduation_applications");

            migrationBuilder.DropColumn(
                name: "DepartmentReviewedAtUtc",
                table: "graduation_applications");

            migrationBuilder.DropColumn(
                name: "DepartmentReviewedById",
                table: "graduation_applications");

            migrationBuilder.DropColumn(
                name: "FinalEvidenceJson",
                table: "graduation_applications");

            migrationBuilder.DropColumn(
                name: "RequiresDepartmentReview",
                table: "graduation_applications");

            migrationBuilder.AddCheckConstraint(
                name: "CK_p16_graduation_status",
                table: "graduation_applications",
                sql: "\"Status\" IN ('Submitted','Approved','Rejected','Issued')");
        }
    }
}
