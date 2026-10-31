using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Academix.Api.Data.Migrations;

public partial class Phase24QualityEvidenceCompletion : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.AddColumn<string>("DirectSnapshotJson", "quality_program_reviews", "text", nullable: false, defaultValue: "[]");
        m.AddUniqueConstraint("AK_quality_improvement_actions_Id_InstitutionId", "quality_improvement_actions", new[] { "Id", "InstitutionId" });

        m.CreateTable("quality_rubric_alignments", columns: t => new
        {
            Id = t.Column<Guid>("uuid", nullable: false),
            InstitutionId = t.Column<Guid>("uuid", nullable: false),
            ProgramId = t.Column<Guid>("uuid", nullable: false),
            OutcomeId = t.Column<Guid>("uuid", nullable: false),
            SectionId = t.Column<Guid>("uuid", nullable: false),
            CriterionId = t.Column<Guid>("uuid", nullable: false),
            ProficiencyThresholdPercent = t.Column<decimal>("numeric(5,2)", nullable: false),
            IsActive = t.Column<bool>("boolean", nullable: false),
            CreatedAtUtc = t.Column<DateTime>("timestamp with time zone", nullable: false)
        }, constraints: t =>
        {
            t.PrimaryKey("PK_quality_rubric_alignments", x => x.Id);
            t.CheckConstraint("CK_p24_proficiency", "\"ProficiencyThresholdPercent\" BETWEEN 0 AND 100");
            t.ForeignKey("FK_p24_rubric_outcome", x => new { x.OutcomeId, x.ProgramId, x.InstitutionId },
                "quality_program_outcomes", new[] { "Id", "ProgramId", "InstitutionId" }, onDelete: ReferentialAction.Restrict);
            t.ForeignKey("FK_p24_rubric_criterion", x => x.CriterionId,
                "lms_rubric_criteria", "Id", onDelete: ReferentialAction.Restrict);
            t.ForeignKey("FK_p24_rubric_section", x => new { x.SectionId, x.InstitutionId },
                "course_sections", new[] { "Id", "InstitutionId" }, onDelete: ReferentialAction.Restrict);
        });
        m.CreateIndex("IX_quality_rubric_alignments_OutcomeId_ProgramId_InstitutionId", "quality_rubric_alignments", new[] { "OutcomeId", "ProgramId", "InstitutionId" });
        m.CreateIndex("IX_quality_rubric_alignments_CriterionId", "quality_rubric_alignments", "CriterionId");
        m.CreateIndex("IX_quality_rubric_alignments_SectionId_InstitutionId", "quality_rubric_alignments", new[] { "SectionId", "InstitutionId" });
        m.CreateIndex("IX_quality_rubric_alignments_InstitutionId_OutcomeId_CriterionId", "quality_rubric_alignments", new[] { "InstitutionId", "OutcomeId", "CriterionId" }, unique: true);

        m.CreateTable("quality_action_events", columns: t => new
        {
            Id = t.Column<Guid>("uuid", nullable: false),
            InstitutionId = t.Column<Guid>("uuid", nullable: false),
            ActionId = t.Column<Guid>("uuid", nullable: false),
            ActorId = t.Column<Guid>("uuid", nullable: false),
            FromStatus = t.Column<string>("character varying(20)", maxLength: 20, nullable: false),
            ToStatus = t.Column<string>("character varying(20)", maxLength: 20, nullable: false),
            Note = t.Column<string>("character varying(1500)", maxLength: 1500, nullable: false),
            AtUtc = t.Column<DateTime>("timestamp with time zone", nullable: false)
        }, constraints: t =>
        {
            t.PrimaryKey("PK_quality_action_events", x => x.Id);
            t.ForeignKey("FK_p24_event_action", x => new { x.ActionId, x.InstitutionId },
                "quality_improvement_actions", new[] { "Id", "InstitutionId" }, onDelete: ReferentialAction.Restrict);
            t.ForeignKey("FK_p24_event_actor", x => x.ActorId, "identity_users", "Id", onDelete: ReferentialAction.Restrict);
        });
        m.CreateIndex("IX_quality_action_events_ActionId_InstitutionId", "quality_action_events", new[] { "ActionId", "InstitutionId" });
        m.CreateIndex("IX_quality_action_events_ActorId", "quality_action_events", "ActorId");
        m.CreateIndex("IX_quality_action_events_InstitutionId_ActionId_AtUtc", "quality_action_events", new[] { "InstitutionId", "ActionId", "AtUtc" });

        m.CreateTable("quality_accreditation_packs", columns: t => new
        {
            Id = t.Column<Guid>("uuid", nullable: false),
            InstitutionId = t.Column<Guid>("uuid", nullable: false),
            ReviewId = t.Column<Guid>("uuid", nullable: false),
            Title = t.Column<string>("character varying(200)", maxLength: 200, nullable: false),
            Status = t.Column<string>("character varying(20)", maxLength: 20, nullable: false),
            ManifestJson = t.Column<string>("text", nullable: false),
            ManifestSha256 = t.Column<string>("character varying(64)", maxLength: 64, nullable: false),
            CreatedBy = t.Column<Guid>("uuid", nullable: false),
            SealedBy = t.Column<Guid>("uuid", nullable: true),
            CreatedAtUtc = t.Column<DateTime>("timestamp with time zone", nullable: false),
            SealedAtUtc = t.Column<DateTime>("timestamp with time zone", nullable: true),
            Version = t.Column<long>("bigint", nullable: false)
        }, constraints: t =>
        {
            t.PrimaryKey("PK_quality_accreditation_packs", x => x.Id);
            t.UniqueConstraint("AK_quality_accreditation_packs_Id_InstitutionId", x => new { x.Id, x.InstitutionId });
            t.CheckConstraint("CK_p24_pack_status", "\"Status\" IN ('Draft','Sealed')");
            t.ForeignKey("FK_p24_pack_review", x => new { x.ReviewId, x.InstitutionId },
                "quality_program_reviews", new[] { "Id", "InstitutionId" }, onDelete: ReferentialAction.Restrict);
            t.ForeignKey("FK_p24_pack_creator", x => x.CreatedBy, "identity_users", "Id", onDelete: ReferentialAction.Restrict);
            t.ForeignKey("FK_p24_pack_sealer", x => x.SealedBy, "identity_users", "Id", onDelete: ReferentialAction.Restrict);
        });
        m.CreateIndex("IX_quality_accreditation_packs_ReviewId_InstitutionId", "quality_accreditation_packs", new[] { "ReviewId", "InstitutionId" });
        m.CreateIndex("IX_quality_accreditation_packs_CreatedBy", "quality_accreditation_packs", "CreatedBy");
        m.CreateIndex("IX_quality_accreditation_packs_SealedBy", "quality_accreditation_packs", "SealedBy");
        m.CreateIndex("IX_quality_accreditation_packs_InstitutionId_ReviewId_CreatedAtUtc", "quality_accreditation_packs", new[] { "InstitutionId", "ReviewId", "CreatedAtUtc" });

        m.CreateTable("quality_pack_items", columns: t => new
        {
            Id = t.Column<Guid>("uuid", nullable: false),
            InstitutionId = t.Column<Guid>("uuid", nullable: false),
            PackId = t.Column<Guid>("uuid", nullable: false),
            EvidenceKind = t.Column<string>("character varying(40)", maxLength: 40, nullable: false),
            SourceId = t.Column<Guid>("uuid", nullable: false),
            Label = t.Column<string>("character varying(200)", maxLength: 200, nullable: false),
            EvidenceJson = t.Column<string>("text", nullable: false),
            EvidenceSha256 = t.Column<string>("character varying(64)", maxLength: 64, nullable: false),
            AddedBy = t.Column<Guid>("uuid", nullable: false),
            CreatedAtUtc = t.Column<DateTime>("timestamp with time zone", nullable: false)
        }, constraints: t =>
        {
            t.PrimaryKey("PK_quality_pack_items", x => x.Id);
            t.ForeignKey("FK_p24_item_pack", x => new { x.PackId, x.InstitutionId },
                "quality_accreditation_packs", new[] { "Id", "InstitutionId" }, onDelete: ReferentialAction.Restrict);
            t.ForeignKey("FK_p24_item_actor", x => x.AddedBy, "identity_users", "Id", onDelete: ReferentialAction.Restrict);
        });
        m.CreateIndex("IX_quality_pack_items_PackId_InstitutionId", "quality_pack_items", new[] { "PackId", "InstitutionId" });
        m.CreateIndex("IX_quality_pack_items_AddedBy", "quality_pack_items", "AddedBy");
        m.CreateIndex("IX_quality_pack_items_InstitutionId_PackId_EvidenceKind_SourceId", "quality_pack_items", new[] { "InstitutionId", "PackId", "EvidenceKind", "SourceId" }, unique: true);

        // Database-level last line of defense: event history cannot be rewritten, and sealed packs cannot be changed.
        m.Sql("""
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
            """);
    }

    protected override void Down(MigrationBuilder m)
    {
        m.Sql("""
            DROP TRIGGER IF EXISTS trg_p24_review_guard ON quality_program_reviews;
            DROP FUNCTION IF EXISTS quality_p24_review_guard();
            DROP TRIGGER IF EXISTS trg_p24_rubric_guard ON quality_rubric_alignments;
            DROP FUNCTION IF EXISTS quality_p24_rubric_guard();
            DROP TRIGGER IF EXISTS trg_p24_item_guard ON quality_pack_items;
            DROP FUNCTION IF EXISTS quality_p24_item_guard();
            DROP TRIGGER IF EXISTS trg_p24_pack_guard ON quality_accreditation_packs;
            DROP FUNCTION IF EXISTS quality_p24_pack_guard();
            DROP TRIGGER IF EXISTS trg_p24_event_guard ON quality_action_events;
            DROP FUNCTION IF EXISTS quality_p24_immutable_event();
            """);
        m.DropTable("quality_pack_items");
        m.DropTable("quality_action_events");
        m.DropTable("quality_rubric_alignments");
        m.DropTable("quality_accreditation_packs");
        m.DropUniqueConstraint("AK_quality_improvement_actions_Id_InstitutionId", "quality_improvement_actions");
        m.DropColumn("DirectSnapshotJson", "quality_program_reviews");
    }
}
