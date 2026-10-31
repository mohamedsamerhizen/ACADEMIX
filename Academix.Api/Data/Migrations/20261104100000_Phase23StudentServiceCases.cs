using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Academix.Api.Data.Migrations;

public partial class Phase23StudentServiceCases : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "student_service_definitions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
                Code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                SlaHours = table.Column<int>(type: "integer", nullable: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false),
                Version = table.Column<long>(type: "bigint", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_student_service_definitions", x => x.Id);
                table.UniqueConstraint("AK_student_service_definitions_Id_InstitutionId", x => new { x.Id, x.InstitutionId });
                table.CheckConstraint("CK_p23_sla", "\"SlaHours\" BETWEEN 1 AND 720");
                table.ForeignKey("FK_student_service_definitions_institutions_InstitutionId", x => x.InstitutionId,
                    "institutions", "Id", onDelete: ReferentialAction.Restrict);
            });
        migrationBuilder.CreateIndex("IX_student_service_definitions_InstitutionId_Code",
            "student_service_definitions", new[] { "InstitutionId", "Code" }, unique: true);
        migrationBuilder.CreateTable(
            name: "student_service_cases",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
                DefinitionId = table.Column<Guid>(type: "uuid", nullable: false),
                StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                ClientRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                Subject = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                Details = table.Column<string>(type: "character varying(3000)", maxLength: 3000, nullable: false),
                Status = table.Column<string>(type: "character varying(25)", maxLength: 25, nullable: false),
                SubmittedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                DueAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                ClosedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                Version = table.Column<long>(type: "bigint", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_student_service_cases", x => x.Id);
                table.UniqueConstraint("AK_student_service_cases_Id_InstitutionId", x => new { x.Id, x.InstitutionId });
                table.CheckConstraint("CK_p23_case_status", "\"Status\" IN ('Submitted','InReview','AwaitingStudent','Resolved','Rejected','Closed','Cancelled')");
                table.ForeignKey("FK_student_service_cases_student_service_definitions_DefinitionId_InstitutionId",
                    x => new { x.DefinitionId, x.InstitutionId },
                    "student_service_definitions", new[] { "Id", "InstitutionId" }, onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_student_service_cases_identity_users_StudentId", x => x.StudentId,
                    "identity_users", "Id", onDelete: ReferentialAction.Restrict);
            });
        migrationBuilder.CreateIndex("IX_student_service_cases_DefinitionId_InstitutionId",
            "student_service_cases", new[] { "DefinitionId", "InstitutionId" });
        migrationBuilder.CreateIndex("IX_student_service_cases_StudentId",
            "student_service_cases", "StudentId");
        migrationBuilder.CreateIndex("IX_student_service_cases_InstitutionId_StudentId_ClientRequestId",
            "student_service_cases", new[] { "InstitutionId", "StudentId", "ClientRequestId" }, unique: true);
        migrationBuilder.CreateIndex("IX_student_service_cases_InstitutionId_Status_DueAtUtc",
            "student_service_cases", new[] { "InstitutionId", "Status", "DueAtUtc" });
        migrationBuilder.CreateTable(
            name: "student_service_case_events",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
                CaseId = table.Column<Guid>(type: "uuid", nullable: false),
                ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                Action = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                FromStatus = table.Column<string>(type: "character varying(25)", maxLength: 25, nullable: false),
                ToStatus = table.Column<string>(type: "character varying(25)", maxLength: 25, nullable: false),
                PublicMessage = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                PrivateStaffNote = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                OccurredAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_student_service_case_events", x => x.Id);
                table.ForeignKey("FK_student_service_case_events_student_service_cases_CaseId_InstitutionId",
                    x => new { x.CaseId, x.InstitutionId },
                    "student_service_cases", new[] { "Id", "InstitutionId" }, onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_student_service_case_events_identity_users_ActorId", x => x.ActorId,
                    "identity_users", "Id", onDelete: ReferentialAction.Restrict);
            });
        migrationBuilder.CreateIndex("IX_student_service_case_events_ActorId",
            "student_service_case_events", "ActorId");
        migrationBuilder.CreateIndex("IX_student_service_case_events_InstitutionId_CaseId_OccurredAtUtc",
            "student_service_case_events", new[] { "InstitutionId", "CaseId", "OccurredAtUtc" });
        migrationBuilder.CreateIndex("IX_student_service_case_events_CaseId_InstitutionId",
            "student_service_case_events", new[] { "CaseId", "InstitutionId" });
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "student_service_case_events");
        migrationBuilder.DropTable(name: "student_service_cases");
        migrationBuilder.DropTable(name: "student_service_definitions");
    }
}
