using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Academix.Api.Data.Migrations;

public partial class Phase22StandaloneSessionRooms : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<Guid>(
            name: "RoomId", table: "academic_class_sessions", type: "uuid", nullable: true);

        // Preserve existing managed room allocations from timetable-linked sessions.
        migrationBuilder.Sql("""
            UPDATE academic_class_sessions AS cs
            SET "RoomId" = slot."RoomId"
            FROM academic_timetable_slots AS slot
            WHERE cs."TimetableSlotId" = slot."Id"
              AND cs."InstitutionId" = slot."InstitutionId"
              AND cs."SectionId" = slot."SectionId"
              AND slot."RoomId" IS NOT NULL;
            """);

        migrationBuilder.CreateIndex(
            name: "IX_p22_session_room_utc", table: "academic_class_sessions",
            columns: new[] { "RoomId", "InstitutionId", "StartsAtUtc" });

        migrationBuilder.AddForeignKey(
            name: "FK_p22_session_room", table: "academic_class_sessions",
            columns: new[] { "RoomId", "InstitutionId" },
            principalTable: "campus_rooms", principalColumns: new[] { "Id", "InstitutionId" },
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey("FK_p22_session_room", "academic_class_sessions");
        migrationBuilder.DropIndex("IX_p22_session_room_utc", "academic_class_sessions");
        migrationBuilder.DropColumn("RoomId", "academic_class_sessions");
    }
}
