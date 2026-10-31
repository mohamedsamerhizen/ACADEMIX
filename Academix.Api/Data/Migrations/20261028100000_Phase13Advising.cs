using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace Academix.Api.Data.Migrations;

public partial class Phase13Advising : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.CreateTable(name: "advising_cases", columns: table => new
        {
            Id = table.Column<Guid>(type: "uuid", nullable: false),
            InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
            SectionId = table.Column<Guid>(type: "uuid", nullable: false),
            StudentId = table.Column<Guid>(type: "uuid", nullable: false),
            OpenedBy = table.Column<Guid>(type: "uuid", nullable: false),
            ClientRequestId = table.Column<Guid>(type: "uuid", nullable: false),
            Title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
            InternalSummary = table.Column<string>(type: "character varying(3000)", maxLength: 3000, nullable: false),
            Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
            Version = table.Column<long>(type: "bigint", nullable: false),
            CreatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
            UpdatedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
        }, constraints: table =>
        {
            table.PrimaryKey("PK_advising_cases", x => x.Id);
            table.UniqueConstraint("AK_advising_cases_Id_InstitutionId", x => new { x.Id, x.InstitutionId });
            table.CheckConstraint("CK_p13_case_status", "\"Status\" IN ('Open','InProgress','Resolved')");
            table.ForeignKey("FK_p13_case_section", x => new { x.SectionId, x.InstitutionId },
                "course_sections", new[] { "Id", "InstitutionId" }, onDelete: ReferentialAction.Restrict);
            table.ForeignKey("FK_p13_case_student", x => x.StudentId,
                "identity_users", "Id", onDelete: ReferentialAction.Restrict);
            table.ForeignKey("FK_p13_case_actor", x => x.OpenedBy,
                "identity_users", "Id", onDelete: ReferentialAction.Restrict);
        });
        m.CreateIndex("IX_p13_case_request", "advising_cases", new[] { "InstitutionId", "ClientRequestId" }, unique: true);
        m.CreateIndex("IX_p13_case_section", "advising_cases", new[] { "InstitutionId", "SectionId", "Status", "CreatedAtUtc" });
        m.CreateIndex("IX_p13_case_student", "advising_cases", new[] { "InstitutionId", "StudentId", "CreatedAtUtc" });
        m.CreateIndex("IX_p13_case_actor", "advising_cases", "OpenedBy");
        m.CreateIndex("IX_advising_cases_SectionId_InstitutionId", "advising_cases", new[] { "SectionId", "InstitutionId" });
        m.CreateIndex("IX_advising_cases_StudentId", "advising_cases", "StudentId");

        m.CreateTable(name: "advising_notes", columns: table => new
        {
            Id = table.Column<Guid>(type: "uuid", nullable: false),
            InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
            CaseId = table.Column<Guid>(type: "uuid", nullable: false),
            AuthorId = table.Column<Guid>(type: "uuid", nullable: false),
            Body = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
            AtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
        }, constraints: table =>
        {
            table.PrimaryKey("PK_advising_notes", x => x.Id);
            table.ForeignKey("FK_p13_note_case", x => new { x.CaseId, x.InstitutionId },
                "advising_cases", new[] { "Id", "InstitutionId" }, onDelete: ReferentialAction.Restrict);
            table.ForeignKey("FK_p13_note_author", x => x.AuthorId,
                "identity_users", "Id", onDelete: ReferentialAction.Restrict);
        });
        m.CreateIndex("IX_p13_note_case", "advising_notes", new[] { "CaseId", "AtUtc" });
        m.CreateIndex("IX_p13_note_fk", "advising_notes", new[] { "CaseId", "InstitutionId" });
        m.CreateIndex("IX_advising_notes_AuthorId", "advising_notes", "AuthorId");

        m.CreateTable(name: "advising_tasks", columns: table => new
        {
            Id = table.Column<Guid>(type: "uuid", nullable: false),
            InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
            CaseId = table.Column<Guid>(type: "uuid", nullable: false),
            CreatedBy = table.Column<Guid>(type: "uuid", nullable: false),
            Description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
            DueAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
            Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
            Version = table.Column<long>(type: "bigint", nullable: false),
            ReportedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
            ReviewedBy = table.Column<Guid>(type: "uuid", nullable: true),
            ReviewedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
        }, constraints: table =>
        {
            table.PrimaryKey("PK_advising_tasks", x => x.Id);
            table.CheckConstraint("CK_p13_task_status", "\"Status\" IN ('Pending','ReportedDone','Completed')");
            table.ForeignKey("FK_p13_task_case", x => new { x.CaseId, x.InstitutionId },
                "advising_cases", new[] { "Id", "InstitutionId" }, onDelete: ReferentialAction.Restrict);
            table.ForeignKey("FK_p13_task_creator", x => x.CreatedBy,
                "identity_users", "Id", onDelete: ReferentialAction.Restrict);
            table.ForeignKey("FK_p13_task_reviewer", x => x.ReviewedBy,
                "identity_users", "Id", onDelete: ReferentialAction.Restrict);
        });
        m.CreateIndex("IX_p13_task_case_status", "advising_tasks", new[] { "CaseId", "Status", "DueAtUtc" });
        m.CreateIndex("IX_p13_task_fk", "advising_tasks", new[] { "CaseId", "InstitutionId" });
        m.CreateIndex("IX_p13_task_creator", "advising_tasks", "CreatedBy");
        m.CreateIndex("IX_p13_task_reviewer", "advising_tasks", "ReviewedBy");

        m.CreateTable(name: "advising_events", columns: table => new
        {
            Id = table.Column<Guid>(type: "uuid", nullable: false),
            InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
            CaseId = table.Column<Guid>(type: "uuid", nullable: false),
            ActorId = table.Column<Guid>(type: "uuid", nullable: false),
            Kind = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
            Description = table.Column<string>(type: "character varying(1100)", maxLength: 1100, nullable: false),
            AtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
        }, constraints: table =>
        {
            table.PrimaryKey("PK_advising_events", x => x.Id);
            table.ForeignKey("FK_p13_event_case", x => new { x.CaseId, x.InstitutionId },
                "advising_cases", new[] { "Id", "InstitutionId" }, onDelete: ReferentialAction.Restrict);
            table.ForeignKey("FK_p13_event_actor", x => x.ActorId,
                "identity_users", "Id", onDelete: ReferentialAction.Restrict);
        });
        m.CreateIndex("IX_p13_event_case", "advising_events", new[] { "CaseId", "AtUtc" });
        m.CreateIndex("IX_p13_event_fk", "advising_events", new[] { "CaseId", "InstitutionId" });
        m.CreateIndex("IX_p13_event_actor", "advising_events", "ActorId");
    }

    protected override void Down(MigrationBuilder m)
    {
        m.DropTable("advising_events");
        m.DropTable("advising_tasks");
        m.DropTable("advising_notes");
        m.DropTable("advising_cases");
    }
}
