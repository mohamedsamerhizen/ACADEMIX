START TRANSACTION;
ALTER TABLE gradebook_results ADD "IsManualOverride" boolean NOT NULL DEFAULT FALSE;

ALTER TABLE gradebook_results ADD "RequiresReconciliation" boolean NOT NULL DEFAULT FALSE;

ALTER TABLE gradebook_results ADD "SourceEvidenceJson" text NOT NULL DEFAULT '[]';

ALTER TABLE gradebook_audit ADD "CommandFingerprint" character varying(64);

ALTER TABLE gradebook_audit ADD "CommandId" uuid;

ALTER TABLE gradebook_audit ADD "EvidenceJson" text NOT NULL DEFAULT '{}';

ALTER TABLE gradebook_audit ADD "ResultJson" text;

CREATE UNIQUE INDEX "IX_gradebook_audit_InstitutionId_CommandId" ON gradebook_audit ("InstitutionId", "CommandId") WHERE "CommandId" IS NOT NULL;

ALTER TABLE exam_grade_reports ADD "ScoreVersion" bigint NOT NULL DEFAULT 1;

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
BEGIN RAISE EXCEPTION 'Gradebook audit history is append-only' USING ERRCODE='23514'; END; $;
CREATE TRIGGER remediation_grade_audit_immutable BEFORE UPDATE OR DELETE ON gradebook_audit
  FOR EACH ROW EXECUTE FUNCTION remediation_grade_audit_immutable();

ALTER TABLE exam_grade_reports ADD CONSTRAINT "CK_remediation_score_version" CHECK ("ScoreVersion" > 0);

ALTER TABLE gradebook_results ADD CONSTRAINT "CK_remediation_grade_evidence" CHECK (jsonb_typeof("SourceEvidenceJson"::jsonb) IN ('object','array'));

ALTER TABLE gradebook_audit ADD CONSTRAINT "CK_remediation_command" CHECK ("CommandId" IS NULL OR ("CommandFingerprint" IS NOT NULL AND length("CommandFingerprint")=64 AND "ResultJson" IS NOT NULL));

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261106100000_FinalEngineeringRemediation', '10.0.3');

COMMIT;

