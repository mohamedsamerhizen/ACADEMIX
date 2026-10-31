using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Academix.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase19ApplicantAndIssuanceInvariants : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_registrar_applicants_AdmissionId",
                table: "registrar_applicants",
                column: "AdmissionId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_applicant_conversion",
                table: "registrar_applicants",
                sql: "\"Status\" <> 'Converted' OR (\"StudentId\" IS NOT NULL AND \"AdmissionId\" IS NOT NULL AND \"VerifiedAtUtc\" IS NOT NULL AND \"ReviewedBy\" IS NOT NULL)");

            migrationBuilder.AddCheckConstraint(
                name: "CK_p19_pdf_issuance",
                table: "issued_academic_documents",
                sql: "\"VerificationKeyId\" IS NULL OR (\"TemplateId\" IS NOT NULL AND \"ArtifactKey\" IS NOT NULL AND \"ArtifactDigest\" IS NOT NULL AND \"LegacyProof\" IS NOT NULL AND \"ArtifactDigest\" ~ '^[0-9A-F]{64}$' AND \"LegacyProof\" ~ '^[0-9A-F]{64}$')");

            migrationBuilder.AddForeignKey(
                name: "FK_registrar_applicants_admission_applications_AdmissionId",
                table: "registrar_applicants",
                column: "AdmissionId",
                principalTable: "admission_applications",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
            migrationBuilder.Sql("""

UPDATE issued_academic_documents d SET "SourceInvalidatedAtUtc"=(SELECT MAX(a."AtUtc") FROM gradebook_audit a WHERE a."InstitutionId"=d."InstitutionId" AND a."StudentId"=d."StudentId" AND a."Action"='Corrected' AND a."AtUtc">d."IssuedAtUtc")
WHERE d."Kind" IN ('Transcript','Standing','Graduation') AND d."SourceInvalidatedAtUtc" IS NULL AND EXISTS(SELECT 1 FROM gradebook_audit a WHERE a."InstitutionId"=d."InstitutionId" AND a."StudentId"=d."StudentId" AND a."Action"='Corrected' AND a."AtUtc">d."IssuedAtUtc");
UPDATE graduation_applications g SET "SourceInvalidatedAtUtc"=(SELECT MAX(a."AtUtc") FROM gradebook_audit a WHERE a."InstitutionId"=g."InstitutionId" AND a."StudentId"=g."StudentId" AND a."Action"='Corrected' AND a."AtUtc">g."IssuedAtUtc")
WHERE g."Status"='Issued' AND g."SourceInvalidatedAtUtc" IS NULL AND EXISTS(SELECT 1 FROM gradebook_audit a WHERE a."InstitutionId"=g."InstitutionId" AND a."StudentId"=g."StudentId" AND a."Action"='Corrected' AND a."AtUtc">g."IssuedAtUtc");
CREATE FUNCTION p19_applicant_admission_scope() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
 IF NEW."AdmissionId" IS NOT NULL AND NOT EXISTS(SELECT 1 FROM admission_applications a WHERE a."Id"=NEW."AdmissionId" AND a."InstitutionId"=NEW."InstitutionId" AND a."StudentId"=NEW."StudentId" AND a."ProgramId"=NEW."ProgramId" AND a."TermId"=NEW."TermId" AND a."Status"='Approved')
 THEN RAISE EXCEPTION 'Applicant admission must match its accepted institutional student scope' USING ERRCODE='23514'; END IF; RETURN NEW;
END $$;
CREATE TRIGGER p19_applicant_admission_scope BEFORE INSERT OR UPDATE ON registrar_applicants FOR EACH ROW EXECUTE FUNCTION p19_applicant_admission_scope();
""");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP TRIGGER p19_applicant_admission_scope ON registrar_applicants; DROP FUNCTION p19_applicant_admission_scope();");
            migrationBuilder.DropForeignKey(
                name: "FK_registrar_applicants_admission_applications_AdmissionId",
                table: "registrar_applicants");

            migrationBuilder.DropIndex(
                name: "IX_registrar_applicants_AdmissionId",
                table: "registrar_applicants");

            migrationBuilder.DropCheckConstraint(
                name: "CK_applicant_conversion",
                table: "registrar_applicants");

            migrationBuilder.DropCheckConstraint(
                name: "CK_p19_pdf_issuance",
                table: "issued_academic_documents");
        }
    }
}
