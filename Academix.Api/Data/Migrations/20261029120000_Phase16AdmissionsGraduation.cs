using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace Academix.Api.Data.Migrations;

public partial class Phase16AdmissionsGraduation : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.CreateTable("admission_applications", columns: t => new
        {
            Id = t.Column<Guid>(type: "uuid", nullable: false),
            InstitutionId = t.Column<Guid>(type: "uuid", nullable: false),
            StudentId = t.Column<Guid>(type: "uuid", nullable: false),
            ProgramId = t.Column<Guid>(type: "uuid", nullable: false),
            TermId = t.Column<Guid>(type: "uuid", nullable: false),
            Statement = t.Column<string>(type: "character varying(1500)", maxLength: 1500, nullable: false),
            Status = t.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
            Version = t.Column<long>(type: "bigint", nullable: false),
            ReviewedById = t.Column<Guid>(type: "uuid", nullable: true),
            DecisionReason = t.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
            CreatedAtUtc = t.Column<DateTime>(type: "timestamp with time zone", nullable: false),
            ReviewedAtUtc = t.Column<DateTime>(type: "timestamp with time zone", nullable: true)
        }, constraints: t =>
        {
            t.PrimaryKey("PK_admission_applications", x => x.Id);
            t.CheckConstraint("CK_p16_admission_status", "\"Status\" IN ('Submitted','Approved','Rejected')");
            t.ForeignKey("FK_p16_admission_program", x => new { x.ProgramId, x.InstitutionId },
                "academic_programs", new[] { "Id", "InstitutionId" }, onDelete: ReferentialAction.Restrict);
            t.ForeignKey("FK_p16_admission_term", x => new { x.TermId, x.InstitutionId },
                "academic_terms", new[] { "Id", "InstitutionId" }, onDelete: ReferentialAction.Restrict);
            t.ForeignKey("FK_p16_admission_student", x => x.StudentId,
                "identity_users", "Id", onDelete: ReferentialAction.Restrict);
            t.ForeignKey("FK_p16_admission_reviewer", x => x.ReviewedById,
                "identity_users", "Id", onDelete: ReferentialAction.Restrict);
        });
        m.CreateIndex("IX_p16_admission_student", "admission_applications", new[] { "InstitutionId", "StudentId", "Status" });
        m.CreateIndex("IX_admission_applications_InstitutionId_CreatedAtUtc", "admission_applications", new[] { "InstitutionId", "CreatedAtUtc" });
        m.CreateIndex("IX_p16_admission_one_pending", "admission_applications", new[] { "InstitutionId", "StudentId" },
            unique: true, filter: "\"Status\" = 'Submitted'");
        m.CreateIndex("IX_admission_applications_ProgramId_InstitutionId", "admission_applications", new[] { "ProgramId", "InstitutionId" });
        m.CreateIndex("IX_admission_applications_TermId_InstitutionId", "admission_applications", new[] { "TermId", "InstitutionId" });
        m.CreateIndex("IX_admission_applications_StudentId", "admission_applications", "StudentId");
        m.CreateIndex("IX_admission_applications_ReviewedById", "admission_applications", "ReviewedById");

        m.CreateTable("transfer_credit_petitions", columns: t => new
        {
            Id = t.Column<Guid>(type: "uuid", nullable: false),
            InstitutionId = t.Column<Guid>(type: "uuid", nullable: false),
            StudentId = t.Column<Guid>(type: "uuid", nullable: false),
            ProgramId = t.Column<Guid>(type: "uuid", nullable: false),
            OriginInstitution = t.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
            Status = t.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
            Version = t.Column<long>(type: "bigint", nullable: false),
            ReviewedById = t.Column<Guid>(type: "uuid", nullable: true),
            DecisionReason = t.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
            CreatedAtUtc = t.Column<DateTime>(type: "timestamp with time zone", nullable: false),
            ReviewedAtUtc = t.Column<DateTime>(type: "timestamp with time zone", nullable: true)
        }, constraints: t =>
        {
            t.PrimaryKey("PK_transfer_credit_petitions", x => x.Id);
            t.UniqueConstraint("AK_transfer_credit_petitions_Id_InstitutionId", x => new { x.Id, x.InstitutionId });
            t.CheckConstraint("CK_p16_transfer_status", "\"Status\" IN ('Submitted','Approved','Rejected')");
            t.ForeignKey("FK_p16_transfer_program", x => new { x.ProgramId, x.InstitutionId },
                "academic_programs", new[] { "Id", "InstitutionId" }, onDelete: ReferentialAction.Restrict);
            t.ForeignKey("FK_p16_transfer_student", x => x.StudentId,
                "identity_users", "Id", onDelete: ReferentialAction.Restrict);
            t.ForeignKey("FK_p16_transfer_reviewer", x => x.ReviewedById,
                "identity_users", "Id", onDelete: ReferentialAction.Restrict);
        });
        m.CreateIndex("IX_p16_transfer_student", "transfer_credit_petitions", new[] { "InstitutionId", "StudentId", "CreatedAtUtc" });
        m.CreateIndex("IX_transfer_credit_petitions_ProgramId_InstitutionId", "transfer_credit_petitions", new[] { "ProgramId", "InstitutionId" });
        m.CreateIndex("IX_transfer_credit_petitions_StudentId", "transfer_credit_petitions", "StudentId");
        m.CreateIndex("IX_transfer_credit_petitions_ReviewedById", "transfer_credit_petitions", "ReviewedById");

        m.CreateTable("graduation_applications", columns: t => new
        {
            Id = t.Column<Guid>(type: "uuid", nullable: false),
            InstitutionId = t.Column<Guid>(type: "uuid", nullable: false),
            StudentId = t.Column<Guid>(type: "uuid", nullable: false),
            ProgramId = t.Column<Guid>(type: "uuid", nullable: false),
            PlanId = t.Column<Guid>(type: "uuid", nullable: false),
            Status = t.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
            Version = t.Column<long>(type: "bigint", nullable: false),
            ReviewedById = t.Column<Guid>(type: "uuid", nullable: true),
            DecisionReason = t.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
            CreatedAtUtc = t.Column<DateTime>(type: "timestamp with time zone", nullable: false),
            ReviewedAtUtc = t.Column<DateTime>(type: "timestamp with time zone", nullable: true),
            IssuedById = t.Column<Guid>(type: "uuid", nullable: true),
            CredentialNumber = t.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
            IssuedAtUtc = t.Column<DateTime>(type: "timestamp with time zone", nullable: true)
        }, constraints: t =>
        {
            t.PrimaryKey("PK_graduation_applications", x => x.Id);
            t.CheckConstraint("CK_p16_graduation_status", "\"Status\" IN ('Submitted','Approved','Rejected','Issued')");
            t.ForeignKey("FK_p16_graduation_program", x => new { x.ProgramId, x.InstitutionId },
                "academic_programs", new[] { "Id", "InstitutionId" }, onDelete: ReferentialAction.Restrict);
            t.ForeignKey("FK_p16_graduation_plan", x => new { x.PlanId, x.InstitutionId },
                "curriculum_plans", new[] { "Id", "InstitutionId" }, onDelete: ReferentialAction.Restrict);
            t.ForeignKey("FK_p16_graduation_student", x => x.StudentId,
                "identity_users", "Id", onDelete: ReferentialAction.Restrict);
            t.ForeignKey("FK_p16_graduation_reviewer", x => x.ReviewedById,
                "identity_users", "Id", onDelete: ReferentialAction.Restrict);
            t.ForeignKey("FK_p16_graduation_issuer", x => x.IssuedById,
                "identity_users", "Id", onDelete: ReferentialAction.Restrict);
        });
        m.CreateIndex("IX_p16_graduation_student", "graduation_applications", new[] { "InstitutionId", "StudentId", "CreatedAtUtc" });
        m.CreateIndex("IX_p16_graduation_one_active", "graduation_applications", new[] { "InstitutionId", "StudentId" },
            unique: true, filter: "\"Status\" <> 'Rejected'");
        m.CreateIndex("IX_p16_credential_unique", "graduation_applications", "CredentialNumber", unique: true);
        m.CreateIndex("IX_graduation_applications_ProgramId_InstitutionId", "graduation_applications", new[] { "ProgramId", "InstitutionId" });
        m.CreateIndex("IX_graduation_applications_PlanId_InstitutionId", "graduation_applications", new[] { "PlanId", "InstitutionId" });
        m.CreateIndex("IX_graduation_applications_StudentId", "graduation_applications", "StudentId");
        m.CreateIndex("IX_graduation_applications_ReviewedById", "graduation_applications", "ReviewedById");
        m.CreateIndex("IX_graduation_applications_IssuedById", "graduation_applications", "IssuedById");

        m.CreateTable("lifecycle_decision_events", columns: t => new
        {
            Id = t.Column<Guid>(type: "uuid", nullable: false),
            InstitutionId = t.Column<Guid>(type: "uuid", nullable: false),
            StudentId = t.Column<Guid>(type: "uuid", nullable: false),
            ActorId = t.Column<Guid>(type: "uuid", nullable: false),
            EntityKind = t.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
            EntityId = t.Column<Guid>(type: "uuid", nullable: false),
            Action = t.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
            Reason = t.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
            AtUtc = t.Column<DateTime>(type: "timestamp with time zone", nullable: false)
        }, constraints: t =>
        {
            t.PrimaryKey("PK_lifecycle_decision_events", x => x.Id);
            t.ForeignKey("FK_p16_event_student", x => x.StudentId,
                "identity_users", "Id", onDelete: ReferentialAction.Restrict);
            t.ForeignKey("FK_p16_event_actor", x => x.ActorId,
                "identity_users", "Id", onDelete: ReferentialAction.Restrict);
        });
        m.CreateIndex("IX_p16_event_institution", "lifecycle_decision_events", new[] { "InstitutionId", "AtUtc" });
        m.CreateIndex("IX_lifecycle_decision_events_StudentId", "lifecycle_decision_events", "StudentId");
        m.CreateIndex("IX_lifecycle_decision_events_ActorId", "lifecycle_decision_events", "ActorId");

        m.CreateTable("transfer_credit_lines", columns: t => new
        {
            Id = t.Column<Guid>(type: "uuid", nullable: false),
            InstitutionId = t.Column<Guid>(type: "uuid", nullable: false),
            PetitionId = t.Column<Guid>(type: "uuid", nullable: false),
            TargetCourseId = t.Column<Guid>(type: "uuid", nullable: false),
            OriginCourseCode = t.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
            OriginCourseTitle = t.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
            ProposedCreditHours = t.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false)
        }, constraints: t =>
        {
            t.PrimaryKey("PK_transfer_credit_lines", x => x.Id);
            t.ForeignKey("FK_p16_line_transfer", x => new { x.PetitionId, x.InstitutionId },
                "transfer_credit_petitions", new[] { "Id", "InstitutionId" }, onDelete: ReferentialAction.Restrict);
            t.ForeignKey("FK_p16_line_course", x => new { x.TargetCourseId, x.InstitutionId },
                "courses", new[] { "id", "institution_id" }, onDelete: ReferentialAction.Restrict);
        });
        m.CreateIndex("IX_p16_transfer_line_unique", "transfer_credit_lines", new[] { "InstitutionId", "PetitionId", "TargetCourseId" }, unique: true);
        m.CreateIndex("IX_transfer_credit_lines_PetitionId_InstitutionId", "transfer_credit_lines", new[] { "PetitionId", "InstitutionId" });
        m.CreateIndex("IX_transfer_credit_lines_TargetCourseId_InstitutionId", "transfer_credit_lines", new[] { "TargetCourseId", "InstitutionId" });
    }
    protected override void Down(MigrationBuilder m)
    {
        m.DropTable("transfer_credit_lines");
        m.DropTable("lifecycle_decision_events");
        m.DropTable("graduation_applications");
        m.DropTable("transfer_credit_petitions");
        m.DropTable("admission_applications");
    }
}
