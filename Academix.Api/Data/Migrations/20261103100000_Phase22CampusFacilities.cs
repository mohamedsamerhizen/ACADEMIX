using System;
using Academix.Api.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Academix.Api.Data.Migrations;

public partial class Phase22CampusFacilities : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "campus_buildings",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
                Code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false),
                Version = table.Column<long>(type: "bigint", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_campus_buildings", x => x.Id);
                table.UniqueConstraint("AK_campus_buildings_Id_InstitutionId", x => new { x.Id, x.InstitutionId });
                table.ForeignKey("FK_campus_buildings_institutions_InstitutionId", x => x.InstitutionId,
                    "institutions", "Id", onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.CreateTable(
            name: "campus_rooms",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
                BuildingId = table.Column<Guid>(type: "uuid", nullable: false),
                Code = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                Name = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                Kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                Capacity = table.Column<int>(type: "integer", nullable: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false),
                Version = table.Column<long>(type: "bigint", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_campus_rooms", x => x.Id);
                table.UniqueConstraint("AK_campus_rooms_Id_InstitutionId", x => new { x.Id, x.InstitutionId });
                table.CheckConstraint("CK_p22_room_capacity", "\"Capacity\" BETWEEN 1 AND 10000");
                table.ForeignKey("FK_campus_rooms_campus_buildings_BuildingId_InstitutionId",
                    x => new { x.BuildingId, x.InstitutionId }, "campus_buildings",
                    new[] { "Id", "InstitutionId" }, onDelete: ReferentialAction.Restrict);
            });

        migrationBuilder.AddColumn<Guid>(name: "RoomId", table: "academic_timetable_slots",
            type: "uuid", nullable: true);
        migrationBuilder.CreateIndex(name: "IX_campus_buildings_InstitutionId_Code", table: "campus_buildings",
            columns: new[] { "InstitutionId", "Code" }, unique: true);
        migrationBuilder.CreateIndex(name: "IX_campus_rooms_BuildingId_InstitutionId", table: "campus_rooms",
            columns: new[] { "BuildingId", "InstitutionId" });
        migrationBuilder.CreateIndex(name: "IX_campus_rooms_InstitutionId_BuildingId_Code", table: "campus_rooms",
            columns: new[] { "InstitutionId", "BuildingId", "Code" }, unique: true);
        migrationBuilder.CreateIndex(name: "IX_academic_timetable_slots_RoomId_InstitutionId",
            table: "academic_timetable_slots", columns: new[] { "RoomId", "InstitutionId" });
        migrationBuilder.AddForeignKey(name: "FK_academic_timetable_slots_campus_rooms_RoomId_InstitutionId",
            table: "academic_timetable_slots", columns: new[] { "RoomId", "InstitutionId" },
            principalTable: "campus_rooms", principalColumns: new[] { "Id", "InstitutionId" },
            onDelete: ReferentialAction.Restrict);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropForeignKey(name: "FK_academic_timetable_slots_campus_rooms_RoomId_InstitutionId",
            table: "academic_timetable_slots");
        migrationBuilder.DropIndex(name: "IX_academic_timetable_slots_RoomId_InstitutionId",
            table: "academic_timetable_slots");
        migrationBuilder.DropColumn(name: "RoomId", table: "academic_timetable_slots");
        migrationBuilder.DropTable(name: "campus_rooms");
        migrationBuilder.DropTable(name: "campus_buildings");
    }
}
