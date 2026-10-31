using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace Academix.Api.Data.Migrations;

public partial class Phase14AcademicRecords : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.CreateTable("student_program_memberships", columns: t => new
        {
            Id = t.Column<Guid>(type: "uuid", nullable: false),
            InstitutionId = t.Column<Guid>(type: "uuid", nullable: false),
            StudentId = t.Column<Guid>(type: "uuid", nullable: false),
            ProgramId = t.Column<Guid>(type: "uuid", nullable: false),
            AdmissionTermId = t.Column<Guid>(type: "uuid", nullable: false),
            CurrentTermId = t.Column<Guid>(type: "uuid", nullable: false),
            Status = t.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
            Version = t.Column<long>(type: "bigint", nullable: false),
            CreatedAtUtc = t.Column<DateTime>(type: "timestamp with time zone", nullable: false)
        }, constraints: t =>
        {
            t.PrimaryKey("PK_student_program_memberships", x => x.Id);
            t.CheckConstraint("CK_p14_membership_status", "\"Status\" IN ('Active','Completed')");
            t.ForeignKey("FK_p14_membership_program", x => new { x.ProgramId, x.InstitutionId }, "academic_programs",
                new[] { "Id", "InstitutionId" }, onDelete: ReferentialAction.Restrict);
            t.ForeignKey("FK_p14_membership_admission_term", x => new { x.AdmissionTermId, x.InstitutionId }, "academic_terms",
                new[] { "Id", "InstitutionId" }, onDelete: ReferentialAction.Restrict);
            t.ForeignKey("FK_p14_membership_current_term", x => new { x.CurrentTermId, x.InstitutionId }, "academic_terms",
                new[] { "Id", "InstitutionId" }, onDelete: ReferentialAction.Restrict);
            t.ForeignKey("FK_p14_membership_student", x => x.StudentId, "identity_users", "Id", onDelete: ReferentialAction.Restrict);
        });
        m.CreateIndex("IX_p14_membership_student", "student_program_memberships", new[] { "InstitutionId", "StudentId" }, unique: true);
        m.CreateIndex("IX_student_program_memberships_ProgramId_InstitutionId", "student_program_memberships", new[] { "ProgramId", "InstitutionId" });
        m.CreateIndex("IX_student_program_memberships_AdmissionTermId_InstitutionId", "student_program_memberships", new[] { "AdmissionTermId", "InstitutionId" });
        m.CreateIndex("IX_student_program_memberships_CurrentTermId_InstitutionId", "student_program_memberships", new[] { "CurrentTermId", "InstitutionId" });
        m.CreateIndex("IX_student_program_memberships_StudentId", "student_program_memberships", "StudentId");

        m.CreateTable("course_prerequisites", columns: t => new
        {
            Id = t.Column<Guid>(type: "uuid", nullable: false),
            InstitutionId = t.Column<Guid>(type: "uuid", nullable: false),
            CourseId = t.Column<Guid>(type: "uuid", nullable: false),
            RequiredCourseId = t.Column<Guid>(type: "uuid", nullable: false),
            MinimumPercentage = t.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
            CreatedAtUtc = t.Column<DateTime>(type: "timestamp with time zone", nullable: false)
        }, constraints: t =>
        {
            t.PrimaryKey("PK_course_prerequisites", x => x.Id);
            t.CheckConstraint("CK_p14_prereq_not_self", "\"CourseId\" <> \"RequiredCourseId\"");
            t.ForeignKey("FK_p14_prereq_course", x => new { x.CourseId, x.InstitutionId }, "courses",
                new[] { "id", "institution_id" }, onDelete: ReferentialAction.Restrict);
            t.ForeignKey("FK_p14_prereq_required_course", x => new { x.RequiredCourseId, x.InstitutionId }, "courses",
                new[] { "id", "institution_id" }, onDelete: ReferentialAction.Restrict);
        });
        m.CreateIndex("IX_p14_prerequisite_unique", "course_prerequisites", new[] { "InstitutionId", "CourseId", "RequiredCourseId" }, unique: true);
        m.CreateIndex("IX_course_prerequisites_CourseId_InstitutionId", "course_prerequisites", new[] { "CourseId", "InstitutionId" });
        m.CreateIndex("IX_course_prerequisites_RequiredCourseId_InstitutionId", "course_prerequisites", new[] { "RequiredCourseId", "InstitutionId" });

        m.CreateTable("student_transcript_entries", columns: t => new
        {
            Id = t.Column<Guid>(type: "uuid", nullable: false),
            InstitutionId = t.Column<Guid>(type: "uuid", nullable: false),
            StudentId = t.Column<Guid>(type: "uuid", nullable: false),
            SectionId = t.Column<Guid>(type: "uuid", nullable: false),
            CourseId = t.Column<Guid>(type: "uuid", nullable: false),
            TermId = t.Column<Guid>(type: "uuid", nullable: false),
            CourseCode = t.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
            CourseTitle = t.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
            Percentage = t.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
            Passed = t.Column<bool>(type: "boolean", nullable: false),
            Letter = t.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
            GradebookVersion = t.Column<long>(type: "bigint", nullable: false),
            Version = t.Column<long>(type: "bigint", nullable: false),
            SyncedAtUtc = t.Column<DateTime>(type: "timestamp with time zone", nullable: false)
        }, constraints: t =>
        {
            t.PrimaryKey("PK_student_transcript_entries", x => x.Id);
            t.ForeignKey("FK_p14_transcript_section", x => new { x.SectionId, x.InstitutionId }, "course_sections",
                new[] { "Id", "InstitutionId" }, onDelete: ReferentialAction.Restrict);
            t.ForeignKey("FK_p14_transcript_student", x => x.StudentId, "identity_users", "Id", onDelete: ReferentialAction.Restrict);
        });
        m.CreateIndex("IX_p14_transcript_unique", "student_transcript_entries", new[] { "InstitutionId", "SectionId", "StudentId" }, unique: true);
        m.CreateIndex("IX_p14_transcript_student_term", "student_transcript_entries", new[] { "InstitutionId", "StudentId", "TermId" });
        m.CreateIndex("IX_student_transcript_entries_SectionId_InstitutionId", "student_transcript_entries", new[] { "SectionId", "InstitutionId" });
        m.CreateIndex("IX_student_transcript_entries_StudentId", "student_transcript_entries", "StudentId");

        m.CreateTable("student_term_standings", columns: t => new
        {
            Id = t.Column<Guid>(type: "uuid", nullable: false),
            InstitutionId = t.Column<Guid>(type: "uuid", nullable: false),
            StudentId = t.Column<Guid>(type: "uuid", nullable: false),
            TermId = t.Column<Guid>(type: "uuid", nullable: false),
            CompletedCourses = t.Column<int>(type: "integer", nullable: false),
            PassedCourses = t.Column<int>(type: "integer", nullable: false),
            AveragePercentage = t.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
            Status = t.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
            Version = t.Column<long>(type: "bigint", nullable: false),
            CalculatedAtUtc = t.Column<DateTime>(type: "timestamp with time zone", nullable: false)
        }, constraints: t =>
        {
            t.PrimaryKey("PK_student_term_standings", x => x.Id);
            t.CheckConstraint("CK_p14_standing_status", "\"Status\" IN ('InsufficientData','Probation','AtRisk','GoodStanding')");
            t.ForeignKey("FK_p14_standing_term", x => new { x.TermId, x.InstitutionId }, "academic_terms",
                new[] { "Id", "InstitutionId" }, onDelete: ReferentialAction.Restrict);
            t.ForeignKey("FK_p14_standing_student", x => x.StudentId, "identity_users", "Id", onDelete: ReferentialAction.Restrict);
        });
        m.CreateIndex("IX_p14_standing_unique", "student_term_standings", new[] { "InstitutionId", "StudentId", "TermId" }, unique: true);
        m.CreateIndex("IX_student_term_standings_TermId_InstitutionId", "student_term_standings", new[] { "TermId", "InstitutionId" });
        m.CreateIndex("IX_student_term_standings_StudentId", "student_term_standings", "StudentId");

        m.CreateTable("student_lifecycle_events", columns: t => new
        {
            Id = t.Column<Guid>(type: "uuid", nullable: false),
            InstitutionId = t.Column<Guid>(type: "uuid", nullable: false),
            StudentId = t.Column<Guid>(type: "uuid", nullable: false),
            ActorId = t.Column<Guid>(type: "uuid", nullable: false),
            Kind = t.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
            Detail = t.Column<string>(type: "character varying(800)", maxLength: 800, nullable: false),
            AtUtc = t.Column<DateTime>(type: "timestamp with time zone", nullable: false)
        }, constraints: t =>
        {
            t.PrimaryKey("PK_student_lifecycle_events", x => x.Id);
            t.ForeignKey("FK_p14_event_student", x => x.StudentId, "identity_users", "Id", onDelete: ReferentialAction.Restrict);
            t.ForeignKey("FK_p14_event_actor", x => x.ActorId, "identity_users", "Id", onDelete: ReferentialAction.Restrict);
        });
        m.CreateIndex("IX_p14_event_institution", "student_lifecycle_events", new[] { "InstitutionId", "AtUtc" });
        m.CreateIndex("IX_p14_event_student", "student_lifecycle_events", new[] { "InstitutionId", "StudentId", "AtUtc" });
        m.CreateIndex("IX_student_lifecycle_events_StudentId", "student_lifecycle_events", "StudentId");
        m.CreateIndex("IX_student_lifecycle_events_ActorId", "student_lifecycle_events", "ActorId");
    }

    protected override void Down(MigrationBuilder m)
    {
        m.DropTable("student_lifecycle_events");
        m.DropTable("student_term_standings");
        m.DropTable("student_transcript_entries");
        m.DropTable("course_prerequisites");
        m.DropTable("student_program_memberships");
    }
}
