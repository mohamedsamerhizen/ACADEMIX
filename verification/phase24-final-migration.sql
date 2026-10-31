START TRANSACTION;
ALTER TABLE quality_program_reviews ADD "DirectSnapshotJson" text NOT NULL DEFAULT '[]';

ALTER TABLE quality_improvement_actions ADD CONSTRAINT "AK_quality_improvement_actions_Id_InstitutionId" UNIQUE ("Id", "InstitutionId");

CREATE TABLE quality_rubric_alignments (
    "Id" uuid NOT NULL,
    "InstitutionId" uuid NOT NULL,
    "ProgramId" uuid NOT NULL,
    "OutcomeId" uuid NOT NULL,
    "SectionId" uuid NOT NULL,
    "CriterionId" uuid NOT NULL,
    "ProficiencyThresholdPercent" numeric(5,2) NOT NULL,
    "IsActive" boolean NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_quality_rubric_alignments" PRIMARY KEY ("Id"),
    CONSTRAINT "CK_p24_proficiency" CHECK ("ProficiencyThresholdPercent" BETWEEN 0 AND 100),
    CONSTRAINT "FK_p24_rubric_outcome" FOREIGN KEY ("OutcomeId", "ProgramId", "InstitutionId") REFERENCES quality_program_outcomes ("Id", "ProgramId", "InstitutionId") ON DELETE RESTRICT,
    CONSTRAINT "FK_p24_rubric_criterion" FOREIGN KEY ("CriterionId") REFERENCES lms_rubric_criteria ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_p24_rubric_section" FOREIGN KEY ("SectionId", "InstitutionId") REFERENCES course_sections ("Id", "InstitutionId") ON DELETE RESTRICT
);

CREATE INDEX "IX_quality_rubric_alignments_OutcomeId_ProgramId_InstitutionId" ON quality_rubric_alignments ("OutcomeId", "ProgramId", "InstitutionId");

CREATE INDEX "IX_quality_rubric_alignments_CriterionId" ON quality_rubric_alignments ("CriterionId");

CREATE INDEX "IX_quality_rubric_alignments_SectionId_InstitutionId" ON quality_rubric_alignments ("SectionId", "InstitutionId");

CREATE UNIQUE INDEX "IX_quality_rubric_alignments_InstitutionId_OutcomeId_CriterionId" ON quality_rubric_alignments ("InstitutionId", "OutcomeId", "CriterionId");

CREATE TABLE quality_action_events (
    "Id" uuid NOT NULL,
    "InstitutionId" uuid NOT NULL,
    "ActionId" uuid NOT NULL,
    "ActorId" uuid NOT NULL,
    "FromStatus" character varying(20) NOT NULL,
    "ToStatus" character varying(20) NOT NULL,
    "Note" character varying(1500) NOT NULL,
    "AtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_quality_action_events" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_p24_event_action" FOREIGN KEY ("ActionId", "InstitutionId") REFERENCES quality_improvement_actions ("Id", "InstitutionId") ON DELETE RESTRICT,
    CONSTRAINT "FK_p24_event_actor" FOREIGN KEY ("ActorId") REFERENCES identity_users ("Id") ON DELETE RESTRICT
);

CREATE INDEX "IX_quality_action_events_ActionId_InstitutionId" ON quality_action_events ("ActionId", "InstitutionId");

CREATE INDEX "IX_quality_action_events_ActorId" ON quality_action_events ("ActorId");

CREATE INDEX "IX_quality_action_events_InstitutionId_ActionId_AtUtc" ON quality_action_events ("InstitutionId", "ActionId", "AtUtc");

CREATE TABLE quality_accreditation_packs (
    "Id" uuid NOT NULL,
    "InstitutionId" uuid NOT NULL,
    "ReviewId" uuid NOT NULL,
    "Title" character varying(200) NOT NULL,
    "Status" character varying(20) NOT NULL,
    "ManifestJson" text NOT NULL,
    "ManifestSha256" character varying(64) NOT NULL,
    "CreatedBy" uuid NOT NULL,
    "SealedBy" uuid,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    "SealedAtUtc" timestamp with time zone,
    "Version" bigint NOT NULL,
    CONSTRAINT "PK_quality_accreditation_packs" PRIMARY KEY ("Id"),
    CONSTRAINT "AK_quality_accreditation_packs_Id_InstitutionId" UNIQUE ("Id", "InstitutionId"),
    CONSTRAINT "CK_p24_pack_status" CHECK ("Status" IN ('Draft','Sealed')),
    CONSTRAINT "FK_p24_pack_review" FOREIGN KEY ("ReviewId", "InstitutionId") REFERENCES quality_program_reviews ("Id", "InstitutionId") ON DELETE RESTRICT,
    CONSTRAINT "FK_p24_pack_creator" FOREIGN KEY ("CreatedBy") REFERENCES identity_users ("Id") ON DELETE RESTRICT,
    CONSTRAINT "FK_p24_pack_sealer" FOREIGN KEY ("SealedBy") REFERENCES identity_users ("Id") ON DELETE RESTRICT
);

CREATE INDEX "IX_quality_accreditation_packs_ReviewId_InstitutionId" ON quality_accreditation_packs ("ReviewId", "InstitutionId");

CREATE INDEX "IX_quality_accreditation_packs_CreatedBy" ON quality_accreditation_packs ("CreatedBy");

CREATE INDEX "IX_quality_accreditation_packs_SealedBy" ON quality_accreditation_packs ("SealedBy");

CREATE INDEX "IX_quality_accreditation_packs_InstitutionId_ReviewId_CreatedAtUtc" ON quality_accreditation_packs ("InstitutionId", "ReviewId", "CreatedAtUtc");

CREATE TABLE quality_pack_items (
    "Id" uuid NOT NULL,
    "InstitutionId" uuid NOT NULL,
    "PackId" uuid NOT NULL,
    "EvidenceKind" character varying(40) NOT NULL,
    "SourceId" uuid NOT NULL,
    "Label" character varying(200) NOT NULL,
    "EvidenceJson" text NOT NULL,
    "EvidenceSha256" character varying(64) NOT NULL,
    "AddedBy" uuid NOT NULL,
    "CreatedAtUtc" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_quality_pack_items" PRIMARY KEY ("Id"),
    CONSTRAINT "FK_p24_item_pack" FOREIGN KEY ("PackId", "InstitutionId") REFERENCES quality_accreditation_packs ("Id", "InstitutionId") ON DELETE RESTRICT,
    CONSTRAINT "FK_p24_item_actor" FOREIGN KEY ("AddedBy") REFERENCES identity_users ("Id") ON DELETE RESTRICT
);

CREATE INDEX "IX_quality_pack_items_PackId_InstitutionId" ON quality_pack_items ("PackId", "InstitutionId");

CREATE INDEX "IX_quality_pack_items_AddedBy" ON quality_pack_items ("AddedBy");

CREATE UNIQUE INDEX "IX_quality_pack_items_InstitutionId_PackId_EvidenceKind_SourceId" ON quality_pack_items ("InstitutionId", "PackId", "EvidenceKind", "SourceId");

CREATE FUNCTION quality_p24_immutable_event() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN RAISE EXCEPTION 'Quality action events are append-only'; END; $$;
CREATE TRIGGER trg_p24_event_guard BEFORE UPDATE OR DELETE ON quality_action_events
  FOR EACH ROW EXECUTE FUNCTION quality_p24_immutable_event();
CREATE FUNCTION quality_p24_pack_guard() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
  IF TG_OP = 'DELETE' OR OLD."Status" = 'Sealed' THEN
    RAISE EXCEPTION 'Sealed evidence packs cannot be changed or deleted';
  END IF;
  RETURN NEW;
END; $$;
CREATE TRIGGER trg_p24_pack_guard BEFORE UPDATE OR DELETE ON quality_accreditation_packs
  FOR EACH ROW EXECUTE FUNCTION quality_p24_pack_guard();
CREATE FUNCTION quality_p24_item_guard() RETURNS trigger LANGUAGE plpgsql AS $$
DECLARE parent_status text;
BEGIN
  IF TG_OP <> 'INSERT' THEN RAISE EXCEPTION 'Evidence items are append-only'; END IF;
  SELECT p."Status" INTO parent_status FROM quality_accreditation_packs p
      WHERE p."Id" = NEW."PackId" AND p."InstitutionId" = NEW."InstitutionId" FOR UPDATE;
  IF parent_status IS NULL OR parent_status <> 'Draft' THEN
    RAISE EXCEPTION 'Cannot attach evidence to missing or sealed pack';
  END IF;
  RETURN NEW;
END; $$;
CREATE TRIGGER trg_p24_item_guard BEFORE INSERT OR UPDATE OR DELETE ON quality_pack_items
  FOR EACH ROW EXECUTE FUNCTION quality_p24_item_guard();
CREATE FUNCTION quality_p24_rubric_guard() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
  IF NOT EXISTS (SELECT 1 FROM lms_rubric_criteria c WHERE c."Id" = NEW."CriterionId"
      AND c."SectionId" = NEW."SectionId" AND c."InstitutionId" = NEW."InstitutionId") THEN
    RAISE EXCEPTION 'Rubric criterion must belong to mapped section and institution';
  END IF;
  IF NOT EXISTS (SELECT 1 FROM course_sections s JOIN program_courses pc
        ON pc."CourseId" = s."CourseId" AND pc."InstitutionId" = s."InstitutionId"
      WHERE s."Id" = NEW."SectionId" AND s."InstitutionId" = NEW."InstitutionId"
        AND pc."ProgramId" = NEW."ProgramId") THEN
    RAISE EXCEPTION 'Mapped section course must belong to program';
  END IF;
  RETURN NEW;
END; $$;
CREATE TRIGGER trg_p24_rubric_guard BEFORE INSERT OR UPDATE ON quality_rubric_alignments
  FOR EACH ROW EXECUTE FUNCTION quality_p24_rubric_guard();
CREATE FUNCTION quality_p24_review_guard() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
  IF OLD."Status" = 'Published' AND
     (NEW."SnapshotJson" IS DISTINCT FROM OLD."SnapshotJson" OR
      NEW."DirectSnapshotJson" IS DISTINCT FROM OLD."DirectSnapshotJson" OR
      NEW."Status" IS DISTINCT FROM OLD."Status" OR
      NEW."PublishedAtUtc" IS DISTINCT FROM OLD."PublishedAtUtc") THEN
    RAISE EXCEPTION 'Published quality evidence is immutable';
  END IF;
  RETURN NEW;
END; $$;
CREATE TRIGGER trg_p24_review_guard BEFORE UPDATE ON quality_program_reviews
  FOR EACH ROW EXECUTE FUNCTION quality_p24_review_guard();

INSERT INTO "__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261105110000_Phase24QualityEvidenceCompletion', '10.0.3');

COMMIT;

