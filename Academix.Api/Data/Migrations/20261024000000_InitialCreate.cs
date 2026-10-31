using System;
using Academix.Api.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Academix.Api.Data.Migrations;

[DbContext(typeof(AcademixDbContext))]
[Migration("20261024000000_InitialCreate")]
public partial class InitialCreate : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "courses",
            columns: table => new
            {
                id = table.Column<Guid>(type: "uuid", nullable: false),
                code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                is_active = table.Column<bool>(type: "boolean", nullable: false)
            },
            constraints: table => table.PrimaryKey("pk_courses", x => x.id));

        migrationBuilder.CreateIndex(
            name: "ux_courses_code",
            table: "courses",
            column: "code",
            unique: true);

        migrationBuilder.InsertData(
            table: "courses",
            columns: new[] { "id", "code", "title", "is_active" },
            values: new object[,]
            {
                { new Guid("8b217247-2c98-4b79-ac2e-bbe086a2ad11"), "CS101", "Introduction to Computer Science", true },
                { new Guid("fe9ed719-2e94-498a-988c-4c8e2450c2d2"), "MATH101", "Calculus I", true }
            });
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "courses");
    }
}
