using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Academix.Api.Data.Migrations;

public partial class Phase22TimetableRevisionHistory : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "academic_timetable_slot_revisions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
                SectionId = table.Column<Guid>(type: "uuid", nullable: false),
                SlotId = table.Column<Guid>(type: "uuid", nullable: false),
                ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                Action = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                BeforeJson = table.Column<string>(type: "text", nullable: true),
                AfterJson = table.Column<string>(type: "text", nullable: false),
                AtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_academic_timetable_slot_revisions", x => x.Id);
                table.CheckConstraint("CK_p22_revision_action", "\"Action\" IN ('Created','Updated','Deactivated')");
                table.ForeignKey("FK_p22_revision_slot",
                    x => new { x.SlotId, x.SectionId, x.InstitutionId },
                    "academic_timetable_slots", new[] { "Id", "SectionId", "InstitutionId" },
                    onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_p22_revision_actor",
                    x => x.ActorId, "identity_users", "Id", onDelete: ReferentialAction.Restrict);
            });
        migrationBuilder.CreateIndex("IX_p22_revision_actor",
            "academic_timetable_slot_revisions", "ActorId");
        migrationBuilder.CreateIndex("IX_p22_revision_slot",
            "academic_timetable_slot_revisions", new[] { "SlotId", "SectionId", "InstitutionId" });
        migrationBuilder.CreateIndex("IX_p22_revision_scope_time",
            "academic_timetable_slot_revisions", new[] { "InstitutionId", "SectionId", "SlotId", "AtUtc" });
        migrationBuilder.Sql("""
            CREATE FUNCTION p22_deny_timetable_revision_mutation() RETURNS trigger AS $$
            BEGIN
                RAISE EXCEPTION 'Timetable revision history is immutable';
            END;
            $$ LANGUAGE plpgsql;
            CREATE TRIGGER p22_timetable_revision_immutable
            BEFORE UPDATE OR DELETE ON academic_timetable_slot_revisions
            FOR EACH ROW EXECUTE FUNCTION p22_deny_timetable_revision_mutation();
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("academic_timetable_slot_revisions");
        migrationBuilder.Sql("DROP FUNCTION IF EXISTS p22_deny_timetable_revision_mutation();");
    }
}
