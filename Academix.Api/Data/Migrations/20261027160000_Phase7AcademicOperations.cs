using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Academix.Api.Data.Migrations;

public partial class Phase7AcademicOperations : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "academic_timetable_slots",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
                SectionId = table.Column<Guid>(type: "uuid", nullable: false),
                Weekday = table.Column<int>(type: "integer", nullable: false),
                StartsAtMinute = table.Column<int>(type: "integer", nullable: false),
                DurationMinutes = table.Column<int>(type: "integer", nullable: false),
                Room = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                IsActive = table.Column<bool>(type: "boolean", nullable: false),
                Version = table.Column<long>(type: "bigint", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_academic_timetable_slots", x => x.Id);
                table.UniqueConstraint("AK_academic_timetable_slots_Id_SectionId_InstitutionId", x => new { x.Id, x.SectionId, x.InstitutionId });
                table.ForeignKey("FK_timetable_section", x => new { x.SectionId, x.InstitutionId }, "course_sections", new[] { "Id", "InstitutionId" }, onDelete: ReferentialAction.Restrict);
                table.CheckConstraint("CK_timetable_time", "\"Weekday\" BETWEEN 1 AND 7 AND \"StartsAtMinute\" BETWEEN 0 AND 1439 AND \"DurationMinutes\" BETWEEN 15 AND 600 AND \"StartsAtMinute\" + \"DurationMinutes\" <= 1440");
            });

        migrationBuilder.CreateIndex(name: "IX_slots_inst_section_day_start", table: "academic_timetable_slots", columns: new[] { "InstitutionId", "SectionId", "Weekday", "StartsAtMinute" });

        migrationBuilder.CreateTable(
            name: "academic_class_sessions",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
                SectionId = table.Column<Guid>(type: "uuid", nullable: false),
                TimetableSlotId = table.Column<Guid>(type: "uuid", nullable: true),
                StartsAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                EndsAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                Topic = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                Version = table.Column<long>(type: "bigint", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_academic_class_sessions", x => x.Id);
                table.UniqueConstraint("AK_academic_class_sessions_Id_SectionId_InstitutionId", x => new { x.Id, x.SectionId, x.InstitutionId });
                table.ForeignKey("FK_session_section", x => new { x.SectionId, x.InstitutionId }, "course_sections", new[] { "Id", "InstitutionId" }, onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_session_slot", x => new { x.TimetableSlotId, x.SectionId, x.InstitutionId }, "academic_timetable_slots", new[] { "Id", "SectionId", "InstitutionId" }, onDelete: ReferentialAction.Restrict);
                table.CheckConstraint("CK_session_times", "\"EndsAtUtc\" > \"StartsAtUtc\"");
                table.CheckConstraint("CK_session_status", "\"Status\" IN ('Planned','Completed','Cancelled')");
            });

        migrationBuilder.CreateIndex(name: "IX_academic_class_sessions_InstitutionId_SectionId_StartsAtUtc", table: "academic_class_sessions", columns: new[] { "InstitutionId", "SectionId", "StartsAtUtc" });
        migrationBuilder.CreateIndex(name: "IX_sessions_slot_section_inst", table: "academic_class_sessions", columns: new[] { "TimetableSlotId", "SectionId", "InstitutionId" });

        migrationBuilder.CreateTable(
            name: "academic_attendance_records",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
                SectionId = table.Column<Guid>(type: "uuid", nullable: false),
                SessionId = table.Column<Guid>(type: "uuid", nullable: false),
                StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                RecordedBy = table.Column<Guid>(type: "uuid", nullable: false),
                RecordedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_academic_attendance_records", x => x.Id);
                table.ForeignKey("FK_attendance_session", x => new { x.SessionId, x.SectionId, x.InstitutionId }, "academic_class_sessions", new[] { "Id", "SectionId", "InstitutionId" }, onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_attendance_student", x => x.StudentId, "identity_users", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_attendance_actor", x => x.RecordedBy, "identity_users", "Id", onDelete: ReferentialAction.Restrict);
                table.CheckConstraint("CK_attendance_status", "\"Status\" IN ('Present','Absent','Late','Excused')");
            });

        migrationBuilder.CreateIndex(name: "IX_academic_attendance_records_SessionId_StudentId", table: "academic_attendance_records", columns: new[] { "SessionId", "StudentId" }, unique: true);
        migrationBuilder.CreateIndex(name: "IX_attendance_inst_section_student", table: "academic_attendance_records", columns: new[] { "InstitutionId", "SectionId", "StudentId" });
        migrationBuilder.CreateIndex(name: "IX_academic_attendance_records_RecordedBy", table: "academic_attendance_records", column: "RecordedBy");
        migrationBuilder.CreateIndex(name: "IX_academic_attendance_records_StudentId", table: "academic_attendance_records", column: "StudentId");

        migrationBuilder.CreateTable(
            name: "academic_calendar_events",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
                TermId = table.Column<Guid>(type: "uuid", nullable: true),
                SectionId = table.Column<Guid>(type: "uuid", nullable: true),
                Title = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                Description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                StartsAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                EndsAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                Audience = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                IsPublished = table.Column<bool>(type: "boolean", nullable: false),
                CreatedBy = table.Column<Guid>(type: "uuid", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_academic_calendar_events", x => x.Id);
                table.ForeignKey("FK_calendar_institution", x => x.InstitutionId, "institutions", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_calendar_term", x => new { x.TermId, x.InstitutionId }, "academic_terms", new[] { "Id", "InstitutionId" }, onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_calendar_section", x => new { x.SectionId, x.InstitutionId }, "course_sections", new[] { "Id", "InstitutionId" }, onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_calendar_actor", x => x.CreatedBy, "identity_users", "Id", onDelete: ReferentialAction.Restrict);
                table.CheckConstraint("CK_calendar_times", "\"EndsAtUtc\" > \"StartsAtUtc\"");
                table.CheckConstraint("CK_calendar_audience", "\"Audience\" IN ('All','Staff','Students')");
            });

        migrationBuilder.CreateIndex(name: "IX_academic_calendar_events_InstitutionId_StartsAtUtc", table: "academic_calendar_events", columns: new[] { "InstitutionId", "StartsAtUtc" });
        migrationBuilder.CreateIndex(name: "IX_academic_calendar_events_TermId_InstitutionId", table: "academic_calendar_events", columns: new[] { "TermId", "InstitutionId" });
        migrationBuilder.CreateIndex(name: "IX_academic_calendar_events_SectionId_InstitutionId", table: "academic_calendar_events", columns: new[] { "SectionId", "InstitutionId" });
        migrationBuilder.CreateIndex(name: "IX_academic_calendar_events_CreatedBy", table: "academic_calendar_events", column: "CreatedBy");

        migrationBuilder.CreateTable(
            name: "student_academic_requests",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
                StudentId = table.Column<Guid>(type: "uuid", nullable: false),
                SectionId = table.Column<Guid>(type: "uuid", nullable: true),
                Kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                Subject = table.Column<string>(type: "character varying(160)", maxLength: 160, nullable: false),
                Details = table.Column<string>(type: "character varying(3000)", maxLength: 3000, nullable: false),
                Status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                SubmittedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                DecidedAtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                DecidedBy = table.Column<Guid>(type: "uuid", nullable: true),
                DecisionReason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                Version = table.Column<long>(type: "bigint", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_student_academic_requests", x => x.Id);
                table.UniqueConstraint("AK_student_academic_requests_Id_InstitutionId", x => new { x.Id, x.InstitutionId });
                table.ForeignKey("FK_request_institution", x => x.InstitutionId, "institutions", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_request_section", x => new { x.SectionId, x.InstitutionId }, "course_sections", new[] { "Id", "InstitutionId" }, onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_request_student", x => x.StudentId, "identity_users", "Id", onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_request_reviewer", x => x.DecidedBy, "identity_users", "Id", onDelete: ReferentialAction.Restrict);
                table.CheckConstraint("CK_academic_request_kind", "\"Kind\" IN ('General','AttendanceExcuse','EnrollmentException')");
                table.CheckConstraint("CK_academic_request_status", "\"Status\" IN ('Pending','Approved','Rejected','Cancelled')");
            });

        migrationBuilder.CreateIndex(name: "IX_requests_inst_status_date", table: "student_academic_requests", columns: new[] { "InstitutionId", "Status", "SubmittedAtUtc" });
        migrationBuilder.CreateIndex(name: "IX_student_academic_requests_StudentId_SubmittedAtUtc", table: "student_academic_requests", columns: new[] { "StudentId", "SubmittedAtUtc" });
        migrationBuilder.CreateIndex(name: "IX_student_academic_requests_SectionId_InstitutionId", table: "student_academic_requests", columns: new[] { "SectionId", "InstitutionId" });
        migrationBuilder.CreateIndex(name: "IX_student_academic_requests_DecidedBy", table: "student_academic_requests", column: "DecidedBy");

        migrationBuilder.CreateTable(
            name: "academic_request_history",
            columns: table => new
            {
                Id = table.Column<Guid>(type: "uuid", nullable: false),
                InstitutionId = table.Column<Guid>(type: "uuid", nullable: false),
                RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                ActorId = table.Column<Guid>(type: "uuid", nullable: false),
                FromStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                ToStatus = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                Reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                AtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_academic_request_history", x => x.Id);
                table.ForeignKey("FK_history_request", x => new { x.RequestId, x.InstitutionId }, "student_academic_requests", new[] { "Id", "InstitutionId" }, onDelete: ReferentialAction.Restrict);
                table.ForeignKey("FK_history_actor", x => x.ActorId, "identity_users", "Id", onDelete: ReferentialAction.Restrict);
                table.CheckConstraint("CK_request_history_to", "\"ToStatus\" IN ('Approved','Rejected','Cancelled')");
            });

        migrationBuilder.CreateIndex(name: "IX_academic_request_history_InstitutionId_RequestId_AtUtc", table: "academic_request_history", columns: new[] { "InstitutionId", "RequestId", "AtUtc" });
        migrationBuilder.CreateIndex(name: "IX_academic_request_history_ActorId", table: "academic_request_history", column: "ActorId");

    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropTable(name: "academic_request_history");
        migrationBuilder.DropTable(name: "student_academic_requests");
        migrationBuilder.DropTable(name: "academic_calendar_events");
        migrationBuilder.DropTable(name: "academic_attendance_records");
        migrationBuilder.DropTable(name: "academic_class_sessions");
        migrationBuilder.DropTable(name: "academic_timetable_slots");
    }
}
