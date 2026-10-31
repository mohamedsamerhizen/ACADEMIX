using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Academix.Api.Data.Migrations;

public partial class Phase22PhysicalVenueAllocations : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "campus_room_reservations",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
                RoomId = table.Column<Guid>(type: "uuid", nullable: false),
                Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                SectionId = table.Column<Guid>(type: "uuid", nullable: true),
                ExamId = table.Column<Guid>(type: "uuid", nullable: true),
                InvigilatorId = table.Column<Guid>(type: "uuid", nullable: true),
                SeatCount = table.Column<int>(type: "integer", nullable: true),
                StartsAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                EndsAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                ClientRequestId = table.Column<Guid>(type: "uuid", nullable: false),
                Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
                CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                CancelledBy = table.Column<Guid>(type: "uuid", nullable: true),
                CancelledAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                CancellationReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                Version = table.Column<long>(type: "bigint", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_campus_room_reservations", x => x.Id);
                table.CheckConstraint("CK_p22_reservation_times", "\"EndsAtUtc\" > \"StartsAtUtc\"");
                table.CheckConstraint("CK_p22_reservation_status", "\"Status\" IN ('Active','Cancelled')");
                table.CheckConstraint("CK_p22_reservation_kind", "\"Kind\" IN ('Maintenance','Exam')");
                table.CheckConstraint("CK_p22_reservation_shape", "(\"Kind\" = 'Maintenance' AND \"SectionId\" IS NULL AND \"ExamId\" IS NULL AND \"InvigilatorId\" IS NULL AND \"SeatCount\" IS NULL) OR (\"Kind\" = 'Exam' AND \"SectionId\" IS NOT NULL AND \"ExamId\" IS NOT NULL AND \"InvigilatorId\" IS NOT NULL AND \"SeatCount\" BETWEEN 1 AND 10000)");
                table.ForeignKey("FK_reservation_room", x => new { x.RoomId, x.InstitutionId },
                    "campus_rooms", new[] { "Id", "InstitutionId" }, onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_reservation_section", x => new { x.SectionId, x.InstitutionId },
                    "course_sections", new[] { "Id", "InstitutionId" }, onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_reservation_exam", x => x.ExamId,
                    "exam_definitions", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_reservation_invigilator", x => x.InvigilatorId,
                    "identity_users", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_reservation_created_by", x => x.CreatedBy,
                    "identity_users", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_reservation_cancelled_by", x => x.CancelledBy,
                    "identity_users", "Id", onDelete: ReferentialAction.Restrict);
            });
        migrationBuilder.CreateIndex(name: "IX_reservation_cancelled_by", table: "campus_room_reservations", column: "CancelledBy");
        migrationBuilder.CreateIndex(name: "IX_reservation_created_by", table: "campus_room_reservations", column: "CreatedBy");
        migrationBuilder.CreateIndex(name: "IX_reservation_invigilator", table: "campus_room_reservations", column: "InvigilatorId");
        migrationBuilder.CreateIndex(name: "IX_reservation_room_fk", table: "campus_room_reservations", columns: new[] { "RoomId", "InstitutionId" });
        migrationBuilder.CreateIndex(name: "IX_reservation_section_fk", table: "campus_room_reservations", columns: new[] { "SectionId", "InstitutionId" });
        migrationBuilder.CreateIndex(name: "IX_reservation_exam_fk", table: "campus_room_reservations", column: "ExamId");
        migrationBuilder.CreateIndex(name: "IX_reservation_request", table: "campus_room_reservations", columns: new[] { "InstitutionId", "ClientRequestId" }, unique: true);
        migrationBuilder.CreateIndex(name: "IX_reservation_active_exam", table: "campus_room_reservations",
            columns: new[] { "InstitutionId", "ExamId" }, unique: true,
            filter: "\"Status\" = 'Active' AND \"Kind\" = 'Exam'");
        migrationBuilder.CreateIndex(name: "IX_reservation_room_interval", table: "campus_room_reservations",
            columns: new[] { "InstitutionId", "RoomId", "StartsAtUtc" });
        migrationBuilder.CreateIndex(name: "IX_reservation_section_interval", table: "campus_room_reservations",
            columns: new[] { "InstitutionId", "SectionId", "StartsAtUtc" });
        migrationBuilder.CreateIndex(name: "IX_reservation_invigilator_interval", table: "campus_room_reservations",
            columns: new[] { "InstitutionId", "InvigilatorId", "StartsAtUtc" });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
        => migrationBuilder.DropTable(name: "campus_room_reservations");
}
