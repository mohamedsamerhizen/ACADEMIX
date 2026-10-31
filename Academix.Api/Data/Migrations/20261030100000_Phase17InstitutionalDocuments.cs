using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace Academix.Api.Data.Migrations;

public partial class Phase17InstitutionalDocuments : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.CreateTable("institutional_document_policies", columns: t => new
        {
            InstitutionId = t.Column<Guid>(type: "uuid", nullable: false),
            AllowEnrollment = t.Column<bool>(type: "boolean", nullable: false),
            AllowTranscript = t.Column<bool>(type: "boolean", nullable: false),
            AllowStanding = t.Column<bool>(type: "boolean", nullable: false),
            ValidityDays = t.Column<int>(type: "integer", nullable: false),
            Version = t.Column<long>(type: "bigint", nullable: false)
        }, constraints: t =>
        {
            t.PrimaryKey("PK_institutional_document_policies", x => x.InstitutionId);
            t.ForeignKey("FK_p17_policy_institution", x => x.InstitutionId,
                "institutions", "Id", onDelete: ReferentialAction.Restrict);
        });
        m.CreateTable("institutional_document_requests", columns: t => new
        {
            Id = t.Column<Guid>(type: "uuid", nullable: false),
            InstitutionId = t.Column<Guid>(type: "uuid", nullable: false),
            StudentId = t.Column<Guid>(type: "uuid", nullable: false),
            ClientRequestId = t.Column<Guid>(type: "uuid", nullable: false),
            Kind = t.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
            Purpose = t.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
            Status = t.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
            Version = t.Column<long>(type: "bigint", nullable: false),
            SubmittedAtUtc = t.Column<DateTime>(type: "timestamp with time zone", nullable: false),
            ReviewedById = t.Column<Guid>(type: "uuid", nullable: true),
            ReviewedAtUtc = t.Column<DateTime>(type: "timestamp with time zone", nullable: true),
            DecisionReason = t.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
        }, constraints: t =>
        {
            t.PrimaryKey("PK_institutional_document_requests", x => x.Id);
            t.UniqueConstraint("AK_institutional_document_requests_Id_InstitutionId", x => new { x.Id, x.InstitutionId });
            t.CheckConstraint("CK_p17_request_state", "\"Status\" IN ('Submitted','Approved','Rejected','Cancelled','Issued')");
            t.ForeignKey("FK_p17_request_student", x => x.StudentId,
                "identity_users", "Id", onDelete: ReferentialAction.Restrict);
            t.ForeignKey("FK_p17_request_reviewer", x => x.ReviewedById,
                "identity_users", "Id", onDelete: ReferentialAction.Restrict);
        });
        m.CreateIndex("IX_p17_request_idempotency", "institutional_document_requests", new[] { "InstitutionId", "StudentId", "ClientRequestId" }, unique: true);
        m.CreateIndex("IX_p17_request_inbox", "institutional_document_requests", new[] { "InstitutionId", "Status", "SubmittedAtUtc" });
        m.CreateIndex("IX_institutional_document_requests_StudentId", "institutional_document_requests", "StudentId");
        m.CreateIndex("IX_institutional_document_requests_ReviewedById", "institutional_document_requests", "ReviewedById");
        m.CreateTable("issued_academic_documents", columns: t => new
        {
            Id = t.Column<Guid>(type: "uuid", nullable: false),
            InstitutionId = t.Column<Guid>(type: "uuid", nullable: false),
            StudentId = t.Column<Guid>(type: "uuid", nullable: false),
            RequestId = t.Column<Guid>(type: "uuid", nullable: false),
            Kind = t.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
            ReferenceNumber = t.Column<string>(type: "character varying(48)", maxLength: 48, nullable: false),
            SnapshotJson = t.Column<string>(type: "text", nullable: false),
            IssuedById = t.Column<Guid>(type: "uuid", nullable: false),
            IssuedAtUtc = t.Column<DateTime>(type: "timestamp with time zone", nullable: false),
            ExpiresAtUtc = t.Column<DateTime>(type: "timestamp with time zone", nullable: false),
            RevokedById = t.Column<Guid>(type: "uuid", nullable: true),
            RevokedAtUtc = t.Column<DateTime>(type: "timestamp with time zone", nullable: true),
            RevocationReason = t.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
            Version = t.Column<long>(type: "bigint", nullable: false)
        }, constraints: t =>
        {
            t.PrimaryKey("PK_issued_academic_documents", x => x.Id);
            t.ForeignKey("FK_p17_document_request", x => new { x.RequestId, x.InstitutionId },
                "institutional_document_requests", new[] { "Id", "InstitutionId" }, onDelete: ReferentialAction.Restrict);
            t.ForeignKey("FK_p17_document_student", x => x.StudentId,
                "identity_users", "Id", onDelete: ReferentialAction.Restrict);
            t.ForeignKey("FK_p17_document_issuer", x => x.IssuedById,
                "identity_users", "Id", onDelete: ReferentialAction.Restrict);
            t.ForeignKey("FK_p17_document_revoker", x => x.RevokedById,
                "identity_users", "Id", onDelete: ReferentialAction.Restrict);
        });
        m.CreateIndex("IX_p17_document_reference", "issued_academic_documents", "ReferenceNumber", unique: true);
        m.CreateIndex("IX_p17_document_student", "issued_academic_documents", new[] { "InstitutionId", "StudentId", "IssuedAtUtc" });
        m.CreateIndex("IX_p17_document_one_per_request", "issued_academic_documents", new[] { "RequestId", "InstitutionId" }, unique: true);
        m.CreateIndex("IX_issued_academic_documents_StudentId", "issued_academic_documents", "StudentId");
        m.CreateIndex("IX_issued_academic_documents_IssuedById", "issued_academic_documents", "IssuedById");
        m.CreateIndex("IX_issued_academic_documents_RevokedById", "issued_academic_documents", "RevokedById");
        m.CreateTable("institutional_document_events", columns: t => new
        {
            Id = t.Column<Guid>(type: "uuid", nullable: false),
            InstitutionId = t.Column<Guid>(type: "uuid", nullable: false),
            StudentId = t.Column<Guid>(type: "uuid", nullable: false),
            RequestId = t.Column<Guid>(type: "uuid", nullable: false),
            DocumentId = t.Column<Guid>(type: "uuid", nullable: true),
            ActorId = t.Column<Guid>(type: "uuid", nullable: false),
            Action = t.Column<string>(type: "character varying(25)", maxLength: 25, nullable: false),
            Detail = t.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
            AtUtc = t.Column<DateTime>(type: "timestamp with time zone", nullable: false)
        }, constraints: t =>
        {
            t.PrimaryKey("PK_institutional_document_events", x => x.Id);
            t.ForeignKey("FK_p17_event_request", x => new { x.RequestId, x.InstitutionId },
                "institutional_document_requests", new[] { "Id", "InstitutionId" }, onDelete: ReferentialAction.Restrict);
            t.ForeignKey("FK_p17_event_student", x => x.StudentId,
                "identity_users", "Id", onDelete: ReferentialAction.Restrict);
            t.ForeignKey("FK_p17_event_actor", x => x.ActorId,
                "identity_users", "Id", onDelete: ReferentialAction.Restrict);
        });
        m.CreateIndex("IX_p17_event_institution", "institutional_document_events", new[] { "InstitutionId", "AtUtc" });
        m.CreateIndex("IX_institutional_document_events_RequestId_InstitutionId", "institutional_document_events", new[] { "RequestId", "InstitutionId" });
        m.CreateIndex("IX_institutional_document_events_StudentId", "institutional_document_events", "StudentId");
        m.CreateIndex("IX_institutional_document_events_ActorId", "institutional_document_events", "ActorId");
    }
    protected override void Down(MigrationBuilder m)
    {
        m.DropTable("institutional_document_events");
        m.DropTable("issued_academic_documents");
        m.DropTable("institutional_document_requests");
        m.DropTable("institutional_document_policies");
    }
}
