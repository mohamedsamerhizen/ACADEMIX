using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Academix.Api.Data.Migrations;

public partial class Phase9AcademicAssessment : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
            migrationBuilder.CreateTable(
                name: "gradebook_policies",
                columns: table => new
                {
                    SectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    PassingPercentage = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    PublishedAtUtc = table.Column<DateTime?>(type: "timestamp with time zone", nullable: true),
                    UpdatedBy = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gradebook_policies", x => x.SectionId);
                    table.UniqueConstraint("AK_gradebook_policies_SectionId_InstitutionId", x => new { x.SectionId, x.InstitutionId });
                    table.ForeignKey(name: "FK_p9_policy_section", columns: x => new { x.SectionId, x.InstitutionId }, principalTable: "course_sections", principalColumns: new[] { "Id", "InstitutionId" }, onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(name: "FK_p9_policy_actor", column: x => x.UpdatedBy, principalTable: "identity_users", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(name: "IX_gradebook_policies_UpdatedBy", table: "gradebook_policies", column: "UpdatedBy");

            migrationBuilder.CreateTable(
                name: "gradebook_categories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    WeightPercentage = table.Column<decimal>(type: "numeric(10,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gradebook_categories", x => x.Id);
                    table.UniqueConstraint("AK_gradebook_categories_Id_SectionId_InstitutionId", x => new { x.Id, x.SectionId, x.InstitutionId });
                    table.ForeignKey(name: "FK_p9_category_policy", columns: x => new { x.SectionId, x.InstitutionId }, principalTable: "gradebook_policies", principalColumns: new[] { "SectionId", "InstitutionId" }, onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(name: "IX_gradebook_categories_SectionId_Name", table: "gradebook_categories", columns: new[] { "SectionId", "Name" }, unique: true);
            migrationBuilder.CreateIndex(name: "IX_gradebook_categories_SectionId_InstitutionId", table: "gradebook_categories", columns: new[] { "SectionId", "InstitutionId" });

            migrationBuilder.CreateTable(
                name: "gradebook_components",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                    SourceKind = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    SourceId = table.Column<Guid?>(type: "uuid", nullable: true),
                    WeightPercentage = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    MaximumPoints = table.Column<decimal>(type: "numeric(10,2)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gradebook_components", x => x.Id);
                    table.UniqueConstraint("AK_gradebook_components_Id_SectionId_InstitutionId", x => new { x.Id, x.SectionId, x.InstitutionId });
                    table.ForeignKey(name: "FK_p9_component_category", columns: x => new { x.CategoryId, x.SectionId, x.InstitutionId }, principalTable: "gradebook_categories", principalColumns: new[] { "Id", "SectionId", "InstitutionId" }, onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(name: "IX_gradebook_components_CategoryId_Name", table: "gradebook_components", columns: new[] { "CategoryId", "Name" }, unique: true);
            migrationBuilder.CreateIndex(name: "IX_gradebook_components_SectionId_SourceKind_SourceId", table: "gradebook_components", columns: new[] { "SectionId", "SourceKind", "SourceId" }, unique: true, filter: "\"SourceId\" IS NOT NULL");
            migrationBuilder.CreateIndex(name: "IX_gradebook_components_CategoryId_SectionId_InstitutionId", table: "gradebook_components", columns: new[] { "CategoryId", "SectionId", "InstitutionId" });

            migrationBuilder.CreateTable(
                name: "gradebook_manual_entries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    ComponentId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Points = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    GradedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gradebook_manual_entries", x => x.Id);
                    table.ForeignKey(name: "FK_p9_entry_component", columns: x => new { x.ComponentId, x.SectionId, x.InstitutionId }, principalTable: "gradebook_components", principalColumns: new[] { "Id", "SectionId", "InstitutionId" }, onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(name: "FK_p9_entry_student", column: x => x.StudentId, principalTable: "identity_users", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(name: "FK_p9_entry_grader", column: x => x.GradedBy, principalTable: "identity_users", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(name: "IX_gradebook_manual_entries_ComponentId_StudentId", table: "gradebook_manual_entries", columns: new[] { "ComponentId", "StudentId" }, unique: true);
            migrationBuilder.CreateIndex(name: "IX_gradebook_manual_entries_ComponentId_SectionId_InstitutionId", table: "gradebook_manual_entries", columns: new[] { "ComponentId", "SectionId", "InstitutionId" });
            migrationBuilder.CreateIndex(name: "IX_gradebook_manual_entries_StudentId", table: "gradebook_manual_entries", column: "StudentId");
            migrationBuilder.CreateIndex(name: "IX_gradebook_manual_entries_GradedBy", table: "gradebook_manual_entries", column: "GradedBy");

            migrationBuilder.CreateTable(
                name: "gradebook_results",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    Percentage = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    Passed = table.Column<bool>(type: "boolean", nullable: false),
                    Letter = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false),
                    PublishedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gradebook_results", x => x.Id);
                    table.ForeignKey(name: "FK_p9_result_policy", columns: x => new { x.SectionId, x.InstitutionId }, principalTable: "gradebook_policies", principalColumns: new[] { "SectionId", "InstitutionId" }, onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(name: "FK_p9_result_student", column: x => x.StudentId, principalTable: "identity_users", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(name: "IX_gradebook_results_SectionId_StudentId", table: "gradebook_results", columns: new[] { "SectionId", "StudentId" }, unique: true);
            migrationBuilder.CreateIndex(name: "IX_gradebook_results_SectionId_InstitutionId", table: "gradebook_results", columns: new[] { "SectionId", "InstitutionId" });
            migrationBuilder.CreateIndex(name: "IX_gradebook_results_StudentId", table: "gradebook_results", column: "StudentId");

            migrationBuilder.CreateTable(
                name: "gradebook_audit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    SectionId = table.Column<Guid>(type: "uuid", nullable: false),
                    InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
                    StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                    Action = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    PreviousPercentage = table.Column<decimal?>(type: "numeric(10,2)", nullable: true),
                    NewPercentage = table.Column<decimal>(type: "numeric(10,2)", nullable: false),
                    Reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    AtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_gradebook_audit", x => x.Id);
                    table.ForeignKey(name: "FK_p9_audit_policy", columns: x => new { x.SectionId, x.InstitutionId }, principalTable: "gradebook_policies", principalColumns: new[] { "SectionId", "InstitutionId" }, onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(name: "FK_p9_audit_student", column: x => x.StudentId, principalTable: "identity_users", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(name: "FK_p9_audit_actor", column: x => x.ActorId, principalTable: "identity_users", principalColumn: "Id", onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(name: "IX_gradebook_audit_SectionId_AtUtc", table: "gradebook_audit", columns: new[] { "SectionId", "AtUtc" });
            migrationBuilder.CreateIndex(name: "IX_gradebook_audit_SectionId_InstitutionId", table: "gradebook_audit", columns: new[] { "SectionId", "InstitutionId" });
            migrationBuilder.CreateIndex(name: "IX_gradebook_audit_StudentId", table: "gradebook_audit", column: "StudentId");
            migrationBuilder.CreateIndex(name: "IX_gradebook_audit_ActorId", table: "gradebook_audit", column: "ActorId");

    }
    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "gradebook_audit");
        migrationBuilder.DropTable(name: "gradebook_results");
        migrationBuilder.DropTable(name: "gradebook_manual_entries");
        migrationBuilder.DropTable(name: "gradebook_components");
        migrationBuilder.DropTable(name: "gradebook_categories");
        migrationBuilder.DropTable(name: "gradebook_policies");
    }
}
