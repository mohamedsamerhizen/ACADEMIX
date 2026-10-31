using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Academix.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase19ImportIdentityActivation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_import_row_state",
                table: "institutional_import_rows");

            migrationBuilder.AddCheckConstraint(
                name: "CK_import_row_state",
                table: "institutional_import_rows",
                sql: "\"Status\" IN ('Pending','Valid','Invalid','Applied','Failed','AwaitingActivation') AND \"Number\" BETWEEN 1 AND 1000");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_import_row_state",
                table: "institutional_import_rows");

            migrationBuilder.AddCheckConstraint(
                name: "CK_import_row_state",
                table: "institutional_import_rows",
                sql: "\"Status\" IN ('Pending','Valid','Invalid','Applied','Failed') AND \"Number\" BETWEEN 1 AND 1000");
        }
    }
}
