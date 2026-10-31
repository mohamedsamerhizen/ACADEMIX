using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Academix.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase19RegistrarForeignKeys : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_registrar_student_holds_CreatedBy",
                table: "registrar_student_holds",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_registrar_student_holds_ReleasedBy",
                table: "registrar_student_holds",
                column: "ReleasedBy");

            migrationBuilder.CreateIndex(
                name: "IX_registrar_student_holds_StudentId",
                table: "registrar_student_holds",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_registrar_policy_revisions_CreatedBy",
                table: "registrar_policy_revisions",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_registrar_policy_revisions_PublishedBy",
                table: "registrar_policy_revisions",
                column: "PublishedBy");

            migrationBuilder.CreateIndex(
                name: "IX_registrar_exceptions_RequestedBy",
                table: "registrar_exceptions",
                column: "RequestedBy");

            migrationBuilder.CreateIndex(
                name: "IX_registrar_exceptions_ReviewedBy",
                table: "registrar_exceptions",
                column: "ReviewedBy");

            migrationBuilder.CreateIndex(
                name: "IX_registrar_exceptions_StudentId",
                table: "registrar_exceptions",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_registrar_eligibility_decisions_StudentId",
                table: "registrar_eligibility_decisions",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_institutional_import_rows_StudentId",
                table: "institutional_import_rows",
                column: "StudentId");

            migrationBuilder.CreateIndex(
                name: "IX_institutional_import_jobs_ApprovedBy",
                table: "institutional_import_jobs",
                column: "ApprovedBy");

            migrationBuilder.CreateIndex(
                name: "IX_institutional_import_jobs_CreatedBy",
                table: "institutional_import_jobs",
                column: "CreatedBy");

            migrationBuilder.CreateIndex(
                name: "IX_institutional_external_identities_StudentId",
                table: "institutional_external_identities",
                column: "StudentId");

            migrationBuilder.AddForeignKey(
                name: "FK_institutional_external_identities_identity_users_StudentId",
                table: "institutional_external_identities",
                column: "StudentId",
                principalTable: "identity_users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_institutional_import_jobs_identity_users_ApprovedBy",
                table: "institutional_import_jobs",
                column: "ApprovedBy",
                principalTable: "identity_users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_institutional_import_jobs_identity_users_CreatedBy",
                table: "institutional_import_jobs",
                column: "CreatedBy",
                principalTable: "identity_users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_institutional_import_rows_identity_users_StudentId",
                table: "institutional_import_rows",
                column: "StudentId",
                principalTable: "identity_users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_registrar_eligibility_decisions_identity_users_StudentId",
                table: "registrar_eligibility_decisions",
                column: "StudentId",
                principalTable: "identity_users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_registrar_exceptions_identity_users_RequestedBy",
                table: "registrar_exceptions",
                column: "RequestedBy",
                principalTable: "identity_users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_registrar_exceptions_identity_users_ReviewedBy",
                table: "registrar_exceptions",
                column: "ReviewedBy",
                principalTable: "identity_users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_registrar_exceptions_identity_users_StudentId",
                table: "registrar_exceptions",
                column: "StudentId",
                principalTable: "identity_users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_registrar_policy_revisions_identity_users_CreatedBy",
                table: "registrar_policy_revisions",
                column: "CreatedBy",
                principalTable: "identity_users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_registrar_policy_revisions_identity_users_PublishedBy",
                table: "registrar_policy_revisions",
                column: "PublishedBy",
                principalTable: "identity_users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_registrar_student_holds_identity_users_CreatedBy",
                table: "registrar_student_holds",
                column: "CreatedBy",
                principalTable: "identity_users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_registrar_student_holds_identity_users_ReleasedBy",
                table: "registrar_student_holds",
                column: "ReleasedBy",
                principalTable: "identity_users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_registrar_student_holds_identity_users_StudentId",
                table: "registrar_student_holds",
                column: "StudentId",
                principalTable: "identity_users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
            migrationBuilder.Sql("""

CREATE FUNCTION p19_guard_student_tenant() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    IF NEW."StudentId" IS NOT NULL AND NOT EXISTS (
        SELECT 1 FROM identity_users WHERE "Id" = NEW."StudentId" AND "InstitutionId" = NEW."InstitutionId") THEN
        RAISE EXCEPTION 'Student must belong to the resource institution' USING ERRCODE = '23514';
    END IF;
    RETURN NEW;
END $$;
CREATE TRIGGER p19_hold_tenant BEFORE INSERT OR UPDATE ON registrar_student_holds FOR EACH ROW EXECUTE FUNCTION p19_guard_student_tenant();
CREATE TRIGGER p19_exception_tenant BEFORE INSERT OR UPDATE ON registrar_exceptions FOR EACH ROW EXECUTE FUNCTION p19_guard_student_tenant();
CREATE TRIGGER p19_decision_tenant BEFORE INSERT OR UPDATE ON registrar_eligibility_decisions FOR EACH ROW EXECUTE FUNCTION p19_guard_student_tenant();
CREATE TRIGGER p19_import_row_tenant BEFORE INSERT OR UPDATE ON institutional_import_rows FOR EACH ROW EXECUTE FUNCTION p19_guard_student_tenant();
CREATE TRIGGER p19_external_identity_tenant BEFORE INSERT OR UPDATE ON institutional_external_identities FOR EACH ROW EXECUTE FUNCTION p19_guard_student_tenant();

CREATE FUNCTION p19_guard_policy() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    IF TG_OP <> 'INSERT' AND OLD."Status" = 'Published' THEN
        RAISE EXCEPTION 'Published policy revisions are immutable' USING ERRCODE = '23514';
    END IF;
    IF TG_OP = 'DELETE' THEN RETURN OLD; END IF;
    IF NEW."Status" = 'Published' THEN
        PERFORM "Id" FROM institutions WHERE "Id" = NEW."InstitutionId" FOR UPDATE;
        IF EXISTS (SELECT 1 FROM registrar_policy_revisions p WHERE p."InstitutionId" = NEW."InstitutionId"
            AND p."ProgramId" IS NOT DISTINCT FROM NEW."ProgramId" AND p."Id" <> NEW."Id" AND p."Status" = 'Published'
            AND p."EffectiveFromUtc" < NEW."EffectiveUntilUtc" AND NEW."EffectiveFromUtc" < p."EffectiveUntilUtc") THEN
            RAISE EXCEPTION 'Overlapping policy publication' USING ERRCODE = '23514';
        END IF;
    END IF;
    RETURN NEW;
END $$;
CREATE TRIGGER p19_policy_immutable BEFORE INSERT OR UPDATE OR DELETE ON registrar_policy_revisions FOR EACH ROW EXECUTE FUNCTION p19_guard_policy();

CREATE FUNCTION p19_immutable_decision() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    RAISE EXCEPTION 'Eligibility decisions are immutable' USING ERRCODE = '23514';
END $$;
CREATE TRIGGER p19_decision_immutable BEFORE UPDATE OR DELETE ON registrar_eligibility_decisions FOR EACH ROW EXECUTE FUNCTION p19_immutable_decision();

CREATE FUNCTION p19_import_payload_immutable() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    IF NEW."PayloadJson" IS DISTINCT FROM OLD."PayloadJson" OR NEW."JobId" <> OLD."JobId" OR NEW."InstitutionId" <> OLD."InstitutionId" OR NEW."Number" <> OLD."Number" THEN
        RAISE EXCEPTION 'Import payloads are immutable' USING ERRCODE = '23514';
    END IF;
    RETURN NEW;
END $$;
CREATE TRIGGER p19_import_payload BEFORE UPDATE ON institutional_import_rows FOR EACH ROW EXECUTE FUNCTION p19_import_payload_immutable();
""");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""

DROP TRIGGER p19_import_payload ON institutional_import_rows;
DROP FUNCTION p19_import_payload_immutable();
DROP TRIGGER p19_decision_immutable ON registrar_eligibility_decisions;
DROP FUNCTION p19_immutable_decision();
DROP TRIGGER p19_policy_immutable ON registrar_policy_revisions;
DROP FUNCTION p19_guard_policy();
DROP TRIGGER p19_hold_tenant ON registrar_student_holds;
DROP TRIGGER p19_exception_tenant ON registrar_exceptions;
DROP TRIGGER p19_decision_tenant ON registrar_eligibility_decisions;
DROP TRIGGER p19_import_row_tenant ON institutional_import_rows;
DROP TRIGGER p19_external_identity_tenant ON institutional_external_identities;
DROP FUNCTION p19_guard_student_tenant();
""");

            migrationBuilder.DropForeignKey(
                name: "FK_institutional_external_identities_identity_users_StudentId",
                table: "institutional_external_identities");

            migrationBuilder.DropForeignKey(
                name: "FK_institutional_import_jobs_identity_users_ApprovedBy",
                table: "institutional_import_jobs");

            migrationBuilder.DropForeignKey(
                name: "FK_institutional_import_jobs_identity_users_CreatedBy",
                table: "institutional_import_jobs");

            migrationBuilder.DropForeignKey(
                name: "FK_institutional_import_rows_identity_users_StudentId",
                table: "institutional_import_rows");

            migrationBuilder.DropForeignKey(
                name: "FK_registrar_eligibility_decisions_identity_users_StudentId",
                table: "registrar_eligibility_decisions");

            migrationBuilder.DropForeignKey(
                name: "FK_registrar_exceptions_identity_users_RequestedBy",
                table: "registrar_exceptions");

            migrationBuilder.DropForeignKey(
                name: "FK_registrar_exceptions_identity_users_ReviewedBy",
                table: "registrar_exceptions");

            migrationBuilder.DropForeignKey(
                name: "FK_registrar_exceptions_identity_users_StudentId",
                table: "registrar_exceptions");

            migrationBuilder.DropForeignKey(
                name: "FK_registrar_policy_revisions_identity_users_CreatedBy",
                table: "registrar_policy_revisions");

            migrationBuilder.DropForeignKey(
                name: "FK_registrar_policy_revisions_identity_users_PublishedBy",
                table: "registrar_policy_revisions");

            migrationBuilder.DropForeignKey(
                name: "FK_registrar_student_holds_identity_users_CreatedBy",
                table: "registrar_student_holds");

            migrationBuilder.DropForeignKey(
                name: "FK_registrar_student_holds_identity_users_ReleasedBy",
                table: "registrar_student_holds");

            migrationBuilder.DropForeignKey(
                name: "FK_registrar_student_holds_identity_users_StudentId",
                table: "registrar_student_holds");

            migrationBuilder.DropIndex(
                name: "IX_registrar_student_holds_CreatedBy",
                table: "registrar_student_holds");

            migrationBuilder.DropIndex(
                name: "IX_registrar_student_holds_ReleasedBy",
                table: "registrar_student_holds");

            migrationBuilder.DropIndex(
                name: "IX_registrar_student_holds_StudentId",
                table: "registrar_student_holds");

            migrationBuilder.DropIndex(
                name: "IX_registrar_policy_revisions_CreatedBy",
                table: "registrar_policy_revisions");

            migrationBuilder.DropIndex(
                name: "IX_registrar_policy_revisions_PublishedBy",
                table: "registrar_policy_revisions");

            migrationBuilder.DropIndex(
                name: "IX_registrar_exceptions_RequestedBy",
                table: "registrar_exceptions");

            migrationBuilder.DropIndex(
                name: "IX_registrar_exceptions_ReviewedBy",
                table: "registrar_exceptions");

            migrationBuilder.DropIndex(
                name: "IX_registrar_exceptions_StudentId",
                table: "registrar_exceptions");

            migrationBuilder.DropIndex(
                name: "IX_registrar_eligibility_decisions_StudentId",
                table: "registrar_eligibility_decisions");

            migrationBuilder.DropIndex(
                name: "IX_institutional_import_rows_StudentId",
                table: "institutional_import_rows");

            migrationBuilder.DropIndex(
                name: "IX_institutional_import_jobs_ApprovedBy",
                table: "institutional_import_jobs");

            migrationBuilder.DropIndex(
                name: "IX_institutional_import_jobs_CreatedBy",
                table: "institutional_import_jobs");

            migrationBuilder.DropIndex(
                name: "IX_institutional_external_identities_StudentId",
                table: "institutional_external_identities");
        }
    }
}
