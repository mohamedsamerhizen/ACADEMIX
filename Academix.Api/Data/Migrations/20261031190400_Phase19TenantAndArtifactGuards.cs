using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Academix.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase19TenantAndArtifactGuards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_institutional_event_receipts_institutional_outbox_events_Ev~",
                table: "institutional_event_receipts");

            migrationBuilder.AddUniqueConstraint(
                name: "AK_institutional_outbox_events_Id_InstitutionId",
                table: "institutional_outbox_events",
                columns: new[] { "Id", "InstitutionId" });

            migrationBuilder.CreateIndex(
                name: "IX_student_academic_requests_FulfilledBy",
                table: "student_academic_requests",
                column: "FulfilledBy");

            migrationBuilder.CreateIndex(
                name: "IX_registrar_recognized_credits_StudentId",
                table: "registrar_recognized_credits",
                column: "StudentId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_recognized_positive_credit",
                table: "registrar_recognized_credits",
                sql: "\"CreditHours\" > 0");

            migrationBuilder.CreateIndex(
                name: "IX_registrar_curriculum_transitions_ApprovedBy",
                table: "registrar_curriculum_transitions",
                column: "ApprovedBy");

            migrationBuilder.CreateIndex(
                name: "IX_registrar_curriculum_transitions_RequestedBy",
                table: "registrar_curriculum_transitions",
                column: "RequestedBy");

            migrationBuilder.CreateIndex(
                name: "IX_registrar_applicant_history_ActorId",
                table: "registrar_applicant_history",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_institutional_event_receipts_EventId_InstitutionId",
                table: "institutional_event_receipts",
                columns: new[] { "EventId", "InstitutionId" });

            migrationBuilder.CreateIndex(
                name: "IX_institutional_document_templates_CreatedBy",
                table: "institutional_document_templates",
                column: "CreatedBy");

            migrationBuilder.AddForeignKey(
                name: "FK_institutional_document_templates_identity_users_CreatedBy",
                table: "institutional_document_templates",
                column: "CreatedBy",
                principalTable: "identity_users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_institutional_event_receipts_institutional_outbox_events_Ev~",
                table: "institutional_event_receipts",
                columns: new[] { "EventId", "InstitutionId" },
                principalTable: "institutional_outbox_events",
                principalColumns: new[] { "Id", "InstitutionId" },
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_registrar_applicant_history_identity_users_ActorId",
                table: "registrar_applicant_history",
                column: "ActorId",
                principalTable: "identity_users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_registrar_curriculum_transitions_identity_users_ApprovedBy",
                table: "registrar_curriculum_transitions",
                column: "ApprovedBy",
                principalTable: "identity_users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_registrar_curriculum_transitions_identity_users_RequestedBy",
                table: "registrar_curriculum_transitions",
                column: "RequestedBy",
                principalTable: "identity_users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_registrar_recognized_credits_identity_users_StudentId",
                table: "registrar_recognized_credits",
                column: "StudentId",
                principalTable: "identity_users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_student_academic_requests_identity_users_FulfilledBy",
                table: "student_academic_requests",
                column: "FulfilledBy",
                principalTable: "identity_users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
            migrationBuilder.Sql("""

CREATE FUNCTION p19_artifact_immutable() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
 IF ROW(NEW."Id",NEW."InstitutionId",NEW."StudentId",NEW."RequestId",NEW."Kind",NEW."ReferenceNumber",NEW."SnapshotJson",NEW."IssuedById",NEW."IssuedAtUtc",NEW."ExpiresAtUtc",NEW."TemplateId",NEW."VerificationKeyId",NEW."ArtifactKey",NEW."ArtifactDigest")
 IS DISTINCT FROM ROW(OLD."Id",OLD."InstitutionId",OLD."StudentId",OLD."RequestId",OLD."Kind",OLD."ReferenceNumber",OLD."SnapshotJson",OLD."IssuedById",OLD."IssuedAtUtc",OLD."ExpiresAtUtc",OLD."TemplateId",OLD."VerificationKeyId",OLD."ArtifactKey",OLD."ArtifactDigest")
 OR (OLD."LegacyProof" IS NOT NULL AND NEW."LegacyProof" IS DISTINCT FROM OLD."LegacyProof") THEN
 RAISE EXCEPTION 'Issued document content is immutable' USING ERRCODE='23514'; END IF;
 RETURN NEW;
END $$;
CREATE TRIGGER p19_artifact_immutable BEFORE UPDATE ON issued_academic_documents FOR EACH ROW EXECUTE FUNCTION p19_artifact_immutable();
CREATE FUNCTION p19_template_immutable() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
 IF OLD."PublishedAtUtc" IS NOT NULL THEN RAISE EXCEPTION 'Published template is immutable' USING ERRCODE='23514'; END IF;
 IF TG_OP='DELETE' THEN RETURN OLD; END IF; RETURN NEW;
END $$;
CREATE TRIGGER p19_template_immutable BEFORE UPDATE OR DELETE ON institutional_document_templates FOR EACH ROW EXECUTE FUNCTION p19_template_immutable();
CREATE FUNCTION p19_key_material_immutable() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
 IF ROW(NEW."Id",NEW."InstitutionId",NEW."ProtectedSecret",NEW."CreatedAtUtc") IS DISTINCT FROM ROW(OLD."Id",OLD."InstitutionId",OLD."ProtectedSecret",OLD."CreatedAtUtc")
 THEN RAISE EXCEPTION 'Verification key material is immutable' USING ERRCODE='23514'; END IF; RETURN NEW;
END $$;
CREATE TRIGGER p19_key_immutable BEFORE UPDATE ON institutional_verification_keys FOR EACH ROW EXECUTE FUNCTION p19_key_material_immutable();
CREATE TRIGGER p19_profile_tenant BEFORE INSERT OR UPDATE ON registrar_student_profiles FOR EACH ROW EXECUTE FUNCTION p19_guard_student_tenant();
CREATE TRIGGER p19_applicant_tenant BEFORE INSERT OR UPDATE ON registrar_applicants FOR EACH ROW EXECUTE FUNCTION p19_guard_student_tenant();
CREATE TRIGGER p19_transition_tenant BEFORE INSERT OR UPDATE ON registrar_curriculum_transitions FOR EACH ROW EXECUTE FUNCTION p19_guard_student_tenant();
CREATE TRIGGER p19_credit_tenant BEFORE INSERT OR UPDATE ON registrar_recognized_credits FOR EACH ROW EXECUTE FUNCTION p19_guard_student_tenant();
CREATE FUNCTION p19_credit_immutable() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN RAISE EXCEPTION 'Historical recognition cannot be mutated' USING ERRCODE='23514'; END $$;
CREATE TRIGGER p19_credit_immutable BEFORE UPDATE OR DELETE ON registrar_recognized_credits FOR EACH ROW EXECUTE FUNCTION p19_credit_immutable();
CREATE FUNCTION p19_transition_immutable() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
 IF OLD."Status"='Applied' OR (OLD."Status"<>'Pending' AND ROW(NEW."StudentId",NEW."InstitutionId",NEW."FromPlanId",NEW."ToPlanId",NEW."MappingJson",NEW."EffectiveAtUtc",NEW."RequestedBy",NEW."Reason") IS DISTINCT FROM ROW(OLD."StudentId",OLD."InstitutionId",OLD."FromPlanId",OLD."ToPlanId",OLD."MappingJson",OLD."EffectiveAtUtc",OLD."RequestedBy",OLD."Reason"))
 THEN RAISE EXCEPTION 'Reviewed transition data is immutable' USING ERRCODE='23514'; END IF; RETURN NEW;
END $$;
CREATE TRIGGER p19_transition_immutable BEFORE UPDATE ON registrar_curriculum_transitions FOR EACH ROW EXECUTE FUNCTION p19_transition_immutable();
CREATE FUNCTION p19_outbox_payload_immutable() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
 IF ROW(NEW."Id",NEW."InstitutionId",NEW."ImportRowId",NEW."Kind",NEW."PayloadJson",NEW."CreatedAtUtc",NEW."DeduplicationKey") IS DISTINCT FROM ROW(OLD."Id",OLD."InstitutionId",OLD."ImportRowId",OLD."Kind",OLD."PayloadJson",OLD."CreatedAtUtc",OLD."DeduplicationKey")
 THEN RAISE EXCEPTION 'Outbox ownership and payload are immutable' USING ERRCODE='23514'; END IF; RETURN NEW;
END $$;
CREATE TRIGGER p19_outbox_immutable BEFORE UPDATE ON institutional_outbox_events FOR EACH ROW EXECUTE FUNCTION p19_outbox_payload_immutable();
""");

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
DROP TRIGGER p19_outbox_immutable ON institutional_outbox_events; DROP FUNCTION p19_outbox_payload_immutable();
DROP TRIGGER p19_transition_immutable ON registrar_curriculum_transitions; DROP FUNCTION p19_transition_immutable();
DROP TRIGGER p19_credit_immutable ON registrar_recognized_credits; DROP FUNCTION p19_credit_immutable();
DROP TRIGGER p19_credit_tenant ON registrar_recognized_credits; DROP TRIGGER p19_transition_tenant ON registrar_curriculum_transitions;
DROP TRIGGER p19_applicant_tenant ON registrar_applicants; DROP TRIGGER p19_profile_tenant ON registrar_student_profiles;
DROP TRIGGER p19_key_immutable ON institutional_verification_keys; DROP FUNCTION p19_key_material_immutable();
DROP TRIGGER p19_template_immutable ON institutional_document_templates; DROP FUNCTION p19_template_immutable();
DROP TRIGGER p19_artifact_immutable ON issued_academic_documents; DROP FUNCTION p19_artifact_immutable();
""");

            migrationBuilder.DropForeignKey(
                name: "FK_institutional_document_templates_identity_users_CreatedBy",
                table: "institutional_document_templates");

            migrationBuilder.DropForeignKey(
                name: "FK_institutional_event_receipts_institutional_outbox_events_Ev~",
                table: "institutional_event_receipts");

            migrationBuilder.DropForeignKey(
                name: "FK_registrar_applicant_history_identity_users_ActorId",
                table: "registrar_applicant_history");

            migrationBuilder.DropForeignKey(
                name: "FK_registrar_curriculum_transitions_identity_users_ApprovedBy",
                table: "registrar_curriculum_transitions");

            migrationBuilder.DropForeignKey(
                name: "FK_registrar_curriculum_transitions_identity_users_RequestedBy",
                table: "registrar_curriculum_transitions");

            migrationBuilder.DropForeignKey(
                name: "FK_registrar_recognized_credits_identity_users_StudentId",
                table: "registrar_recognized_credits");

            migrationBuilder.DropForeignKey(
                name: "FK_student_academic_requests_identity_users_FulfilledBy",
                table: "student_academic_requests");

            migrationBuilder.DropIndex(
                name: "IX_student_academic_requests_FulfilledBy",
                table: "student_academic_requests");

            migrationBuilder.DropIndex(
                name: "IX_registrar_recognized_credits_StudentId",
                table: "registrar_recognized_credits");

            migrationBuilder.DropCheckConstraint(
                name: "CK_recognized_positive_credit",
                table: "registrar_recognized_credits");

            migrationBuilder.DropIndex(
                name: "IX_registrar_curriculum_transitions_ApprovedBy",
                table: "registrar_curriculum_transitions");

            migrationBuilder.DropIndex(
                name: "IX_registrar_curriculum_transitions_RequestedBy",
                table: "registrar_curriculum_transitions");

            migrationBuilder.DropIndex(
                name: "IX_registrar_applicant_history_ActorId",
                table: "registrar_applicant_history");

            migrationBuilder.DropUniqueConstraint(
                name: "AK_institutional_outbox_events_Id_InstitutionId",
                table: "institutional_outbox_events");

            migrationBuilder.DropIndex(
                name: "IX_institutional_event_receipts_EventId_InstitutionId",
                table: "institutional_event_receipts");

            migrationBuilder.DropIndex(
                name: "IX_institutional_document_templates_CreatedBy",
                table: "institutional_document_templates");

            migrationBuilder.AddForeignKey(
                name: "FK_institutional_event_receipts_institutional_outbox_events_Ev~",
                table: "institutional_event_receipts",
                column: "EventId",
                principalTable: "institutional_outbox_events",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
