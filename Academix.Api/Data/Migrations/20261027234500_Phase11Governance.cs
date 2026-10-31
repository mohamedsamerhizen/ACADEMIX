using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace Academix.Api.Data.Migrations;

public partial class Phase11Governance : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "governance_staff_requests",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
                ClientRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                TargetUserId = table.Column<Guid>(type: "uuid", nullable: false),
                RequestedBy = table.Column<Guid>(type: "uuid", nullable: false),
                Action = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                Role = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                ReviewedBy = table.Column<Guid>(type: "uuid", nullable: true),
                ReviewReason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                ReviewedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                Version = table.Column<long>(type: "bigint", nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_governance_staff_requests", x => x.Id);
                table.ForeignKey(name: "FK_p11_staff_institution", column: x => x.InstitutionId,
                    principalTable: "institutions", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey(name: "FK_p11_staff_target", column: x => x.TargetUserId,
                    principalTable: "identity_users", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey(name: "FK_p11_staff_requester", column: x => x.RequestedBy,
                    principalTable: "identity_users", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey(name: "FK_p11_staff_reviewer", column: x => x.ReviewedBy,
                    principalTable: "identity_users", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
            });
        migrationBuilder.CreateIndex(name: "IX_governance_staff_requests_InstitutionId_ClientRequestId",
            table: "governance_staff_requests", columns: new[] { "InstitutionId", "ClientRequestId" }, unique: true);
        migrationBuilder.CreateIndex(name: "IX_governance_staff_requests_InstitutionId_Status_CreatedAtUtc",
            table: "governance_staff_requests", columns: new[] { "InstitutionId", "Status", "CreatedAtUtc" });
        migrationBuilder.CreateIndex(name: "IX_governance_staff_requests_TargetUserId", table: "governance_staff_requests", column: "TargetUserId");
        migrationBuilder.CreateIndex(name: "IX_governance_staff_requests_RequestedBy", table: "governance_staff_requests", column: "RequestedBy");
        migrationBuilder.CreateIndex(name: "IX_governance_staff_requests_ReviewedBy", table: "governance_staff_requests", column: "ReviewedBy");

        migrationBuilder.CreateTable(
            name: "governance_enrollment_batches",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
                SectionId = table.Column<Guid>(type: "uuid", nullable: false),
                ClientRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                CommittedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CommittedBy = table.Column<Guid>(type: "uuid", nullable: true),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_governance_enrollment_batches", x => x.Id);
                table.UniqueConstraint("AK_governance_enrollment_batches_Id_InstitutionId", x => new { x.Id, x.InstitutionId });
                table.ForeignKey(name: "FK_p11_batch_section", columns: x => new { x.SectionId, x.InstitutionId },
                    principalTable: "course_sections", principalColumns: new[] { "Id", "InstitutionId" }, onDelete: ReferentialAction.Restrict);
                table.ForeignKey(name: "FK_p11_batch_creator", column: x => x.CreatedBy,
                    principalTable: "identity_users", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey(name: "FK_p11_batch_committer", column: x => x.CommittedBy,
                    principalTable: "identity_users", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
            });
        migrationBuilder.CreateIndex(name: "IX_governance_enrollment_batches_InstitutionId_ClientRequestId",
            table: "governance_enrollment_batches", columns: new[] { "InstitutionId", "ClientRequestId" }, unique: true);
        migrationBuilder.CreateIndex(name: "IX_p11_batch_listing", table: "governance_enrollment_batches",
            columns: new[] { "InstitutionId", "Status", "CreatedAtUtc" });
        migrationBuilder.CreateIndex(name: "IX_governance_enrollment_batches_SectionId_InstitutionId",
            table: "governance_enrollment_batches", columns: new[] { "SectionId", "InstitutionId" });
        migrationBuilder.CreateIndex(name: "IX_governance_enrollment_batches_CreatedBy",
            table: "governance_enrollment_batches", column: "CreatedBy");
        migrationBuilder.CreateIndex(name: "IX_governance_enrollment_batches_CommittedBy",
            table: "governance_enrollment_batches", column: "CommittedBy");

        migrationBuilder.CreateTable(
            name: "governance_enrollment_batch_items",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
                BatchId = table.Column<Guid>(type: "uuid", nullable: false),
                StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                Result = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_governance_enrollment_batch_items", x => x.Id);
                table.ForeignKey(name: "FK_p11_batch_item_batch", columns: x => new { x.BatchId, x.InstitutionId },
                    principalTable: "governance_enrollment_batches", principalColumns: new[] { "Id", "InstitutionId" },
                    onDelete: ReferentialAction.Restrict);
            });
        migrationBuilder.CreateIndex(name: "IX_governance_enrollment_batch_items_BatchId_StudentId",
            table: "governance_enrollment_batch_items", columns: new[] { "BatchId", "StudentId" }, unique: true);
        migrationBuilder.CreateIndex(name: "IX_governance_enrollment_batch_items_BatchId_InstitutionId",
            table: "governance_enrollment_batch_items", columns: new[] { "BatchId", "InstitutionId" });
    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "governance_enrollment_batch_items");
        migrationBuilder.DropTable(name: "governance_staff_requests");
        migrationBuilder.DropTable(name: "governance_enrollment_batches");
    }
}
