using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Academix.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Phase19DocumentReissuance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SupersedesDocumentId",
                table: "institutional_document_requests",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "AllowGraduation",
                table: "institutional_document_policies",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_institutional_document_requests_SupersedesDocumentId_Instit~",
                table: "institutional_document_requests",
                columns: new[] { "SupersedesDocumentId", "InstitutionId" });

            migrationBuilder.AddForeignKey(
                name: "FK_institutional_document_requests_issued_academic_documents_S~",
                table: "institutional_document_requests",
                columns: new[] { "SupersedesDocumentId", "InstitutionId" },
                principalTable: "issued_academic_documents",
                principalColumns: new[] { "Id", "InstitutionId" },
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_institutional_document_requests_issued_academic_documents_S~",
                table: "institutional_document_requests");

            migrationBuilder.DropIndex(
                name: "IX_institutional_document_requests_SupersedesDocumentId_Instit~",
                table: "institutional_document_requests");

            migrationBuilder.DropColumn(
                name: "SupersedesDocumentId",
                table: "institutional_document_requests");

            migrationBuilder.DropColumn(
                name: "AllowGraduation",
                table: "institutional_document_policies");
        }
    }
}
