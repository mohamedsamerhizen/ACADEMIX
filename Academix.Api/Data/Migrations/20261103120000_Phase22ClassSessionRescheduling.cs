using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Academix.Api.Data.Migrations;

public partial class Phase22ClassSessionRescheduling : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "academic_class_session_revisions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
                SectionId = table.Column<Guid>(type: "uuid", nullable: false),
                SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                BeforeJson = table.Column<string>(type: "text", nullable: false),
                AfterJson = table.Column<string>(type: "text", nullable: false),
                AtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_academic_class_session_revisions", x => x.Id);
                table.ForeignKey("FK_p22_session_revision_session", x => new { x.SessionId, x.SectionId, x.InstitutionId },
                    "academic_class_sessions", new[] { "Id", "SectionId", "InstitutionId" }, onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_p22_session_revision_actor", x => x.ActorId,
                    "identity_users", "Id", onDelete: ReferentialAction.Restrict);
            });
        migrationBuilder.CreateIndex("IX_academic_class_session_revisions_ActorId", "academic_class_session_revisions", "ActorId");
        migrationBuilder.CreateIndex("IX_academic_class_session_revisions_SessionId_SectionId_InstitutionId",
            "academic_class_session_revisions", new[] { "SessionId", "SectionId", "InstitutionId" });
        migrationBuilder.CreateIndex("IX_p22_session_revision_scope", "academic_class_session_revisions",
            new[] { "InstitutionId", "SectionId", "SessionId", "AtUtc" });
        migrationBuilder.Sql("""
            CREATE FUNCTION p22_deny_class_session_revision_mutation() RETURNS trigger AS $$
            BEGIN
                RAISE EXCEPTION 'Class session revision history is immutable';
            END;
            $$ LANGUAGE plpgsql;
            CREATE TRIGGER p22_class_session_revision_immutable
            BEFORE UPDATE OR DELETE ON academic_class_session_revisions
            FOR EACH ROW EXECUTE FUNCTION p22_deny_class_session_revision_mutation();
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable("academic_class_session_revisions");
        migrationBuilder.Sql("DROP FUNCTION IF EXISTS p22_deny_class_session_revision_mutation();");
    }
}
