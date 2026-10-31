using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Academix.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class FinalEngineeringRemediation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsManualOverride",
                table: "gradebook_results",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "RequiresReconciliation",
                table: "gradebook_results",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "SourceEvidenceJson",
                table: "gradebook_results",
                type: "text",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<string>(
                name: "CommandFingerprint",
                table: "gradebook_audit",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "CommandId",
                table: "gradebook_audit",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EvidenceJson",
                table: "gradebook_audit",
                type: "text",
                nullable: false,
                defaultValue: "{}");

            migrationBuilder.AddColumn<string>(
                name: "ResultJson",
                table: "gradebook_audit",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_gradebook_audit_InstitutionId_CommandId",
                table: "gradebook_audit",
                columns: new[] { "InstitutionId", "CommandId" },
                unique: true,
                filter: "\"CommandId\" IS NOT NULL");
            migrationBuilder.AddColumn<long>(name:"ScoreVersion",table:"exam_grade_reports",type:"bigint",nullable:false,defaultValue:1L);
            migrationBuilder.Sql("""
                UPDATE exam_grade_reports SET "ScoreVersion"="Version";
                UPDATE gradebook_results g SET "IsManualOverride"=true WHERE EXISTS
                  (SELECT 1 FROM gradebook_audit a WHERE a."InstitutionId"=g."InstitutionId" AND a."SectionId"=g."SectionId" AND a."StudentId"=g."StudentId" AND a."Action"='Corrected');
                -- No historical source versions were captured. Do not invent evidence.
                UPDATE gradebook_results g SET "RequiresReconciliation"=true,"Version"="Version"+1 WHERE EXISTS
                  (SELECT 1 FROM gradebook_components c WHERE c."InstitutionId"=g."InstitutionId" AND c."SectionId"=g."SectionId" AND c."SourceKind"<>'Manual');
                UPDATE issued_academic_documents d SET "SourceInvalidatedAtUtc"=CURRENT_TIMESTAMP
                  WHERE d."SourceInvalidatedAtUtc" IS NULL AND d."Kind" IN ('Transcript','Standing','Graduation') AND
                    (EXISTS(SELECT 1 FROM gradebook_results g WHERE g."InstitutionId"=d."InstitutionId" AND g."StudentId"=d."StudentId" AND g."RequiresReconciliation") OR
                     d."Kind"='Graduation' AND EXISTS(SELECT 1 FROM graduation_applications a WHERE a."InstitutionId"=d."InstitutionId" AND a."StudentId"=d."StudentId" AND a."Status"='Issued' AND a."SourceInvalidatedAtUtc" IS NOT NULL));
                UPDATE graduation_applications a SET "SourceInvalidatedAtUtc"=CURRENT_TIMESTAMP
                  WHERE a."Status"='Issued' AND a."SourceInvalidatedAtUtc" IS NULL AND EXISTS
                    (SELECT 1 FROM gradebook_results g WHERE g."InstitutionId"=a."InstitutionId" AND g."StudentId"=a."StudentId" AND g."RequiresReconciliation");
                CREATE FUNCTION remediation_grade_audit_immutable() RETURNS trigger LANGUAGE plpgsql AS $$
                BEGIN RAISE EXCEPTION 'Gradebook audit history is append-only' USING ERRCODE='23514'; END; $$;
                CREATE TRIGGER remediation_grade_audit_immutable BEFORE UPDATE OR DELETE ON gradebook_audit
                  FOR EACH ROW EXECUTE FUNCTION remediation_grade_audit_immutable();
                """);
            migrationBuilder.AddCheckConstraint("CK_remediation_score_version","exam_grade_reports","\"ScoreVersion\" > 0");
            migrationBuilder.AddCheckConstraint("CK_remediation_grade_evidence","gradebook_results","jsonb_typeof(\"SourceEvidenceJson\"::jsonb) IN ('object','array')");
            migrationBuilder.AddCheckConstraint("CK_remediation_command","gradebook_audit","\"CommandId\" IS NULL OR (\"CommandFingerprint\" IS NOT NULL AND length(\"CommandFingerprint\")=64 AND \"ResultJson\" IS NOT NULL)");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
            =>throw new NotSupportedException("Forward-only remediation preserves official source evidence and audit history.");
    }
}
