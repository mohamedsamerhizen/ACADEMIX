using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Academix.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase19ArtifactContentSignature : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_p19_pdf_issuance",
                table: "issued_academic_documents");

            migrationBuilder.AddColumn<string>(
                name: "ArtifactSignature",
                table: "issued_academic_documents",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_p19_pdf_issuance",
                table: "issued_academic_documents",
                sql: "\"VerificationKeyId\" IS NULL OR (\"TemplateId\" IS NOT NULL AND \"ArtifactKey\" IS NOT NULL AND \"ArtifactDigest\" IS NOT NULL AND \"LegacyProof\" IS NOT NULL AND \"ArtifactSignature\" IS NOT NULL AND \"ArtifactSignature\" ~ '^[0-9A-F]{64}$' AND \"ArtifactDigest\" ~ '^[0-9A-F]{64}$' AND \"LegacyProof\" ~ '^[0-9A-F]{64}$')");
            migrationBuilder.Sql("""
CREATE OR REPLACE FUNCTION p19_artifact_immutable() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
 IF ROW(NEW."Id",NEW."InstitutionId",NEW."StudentId",NEW."RequestId",NEW."Kind",NEW."ReferenceNumber",NEW."SnapshotJson",NEW."IssuedById",NEW."IssuedAtUtc",NEW."ExpiresAtUtc",NEW."TemplateId",NEW."VerificationKeyId",NEW."ArtifactKey",NEW."ArtifactDigest",NEW."ArtifactSignature")
 IS DISTINCT FROM ROW(OLD."Id",OLD."InstitutionId",OLD."StudentId",OLD."RequestId",OLD."Kind",OLD."ReferenceNumber",OLD."SnapshotJson",OLD."IssuedById",OLD."IssuedAtUtc",OLD."ExpiresAtUtc",OLD."TemplateId",OLD."VerificationKeyId",OLD."ArtifactKey",OLD."ArtifactDigest",OLD."ArtifactSignature")
 OR (OLD."LegacyProof" IS NOT NULL AND NEW."LegacyProof" IS DISTINCT FROM OLD."LegacyProof") THEN
 RAISE EXCEPTION 'Issued document content is immutable' USING ERRCODE='23514'; END IF;
 RETURN NEW;
END $$;
""");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
CREATE OR REPLACE FUNCTION p19_artifact_immutable() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
 IF ROW(NEW."Id",NEW."InstitutionId",NEW."StudentId",NEW."RequestId",NEW."Kind",NEW."ReferenceNumber",NEW."SnapshotJson",NEW."IssuedById",NEW."IssuedAtUtc",NEW."ExpiresAtUtc",NEW."TemplateId",NEW."VerificationKeyId",NEW."ArtifactKey",NEW."ArtifactDigest")
 IS DISTINCT FROM ROW(OLD."Id",OLD."InstitutionId",OLD."StudentId",OLD."RequestId",OLD."Kind",OLD."ReferenceNumber",OLD."SnapshotJson",OLD."IssuedById",OLD."IssuedAtUtc",OLD."ExpiresAtUtc",OLD."TemplateId",OLD."VerificationKeyId",OLD."ArtifactKey",OLD."ArtifactDigest")
 OR (OLD."LegacyProof" IS NOT NULL AND NEW."LegacyProof" IS DISTINCT FROM OLD."LegacyProof") THEN
 RAISE EXCEPTION 'Issued document content is immutable' USING ERRCODE='23514'; END IF;
 RETURN NEW;
END $$;
""");

            migrationBuilder.DropCheckConstraint(
                name: "CK_p19_pdf_issuance",
                table: "issued_academic_documents");

            migrationBuilder.DropColumn(
                name: "ArtifactSignature",
                table: "issued_academic_documents");

            migrationBuilder.AddCheckConstraint(
                name: "CK_p19_pdf_issuance",
                table: "issued_academic_documents",
                sql: "\"VerificationKeyId\" IS NULL OR (\"TemplateId\" IS NOT NULL AND \"ArtifactKey\" IS NOT NULL AND \"ArtifactDigest\" IS NOT NULL AND \"LegacyProof\" IS NOT NULL AND \"ArtifactDigest\" IS NOT NULL AND \"LegacyProof\" IS NOT NULL AND \"ArtifactDigest\" ~ '^[0-9A-F]{64}$' AND \"LegacyProof\" ~ '^[0-9A-F]{64}$')");
        }
    }
}
