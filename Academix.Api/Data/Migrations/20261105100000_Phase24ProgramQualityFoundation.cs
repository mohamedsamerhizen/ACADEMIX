using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Academix.Api.Data.Migrations;

public partial class Phase24ProgramQualityFoundation : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.CreateTable("quality_program_outcomes", columns: t => new
        {
            Id = t.Column<Guid>("uuid", nullable: false),
            InstitutionId = t.Column<Guid>("uuid", nullable: false),
            ProgramId = t.Column<Guid>("uuid", nullable: false),
            Code = t.Column<string>("character varying(30)", maxLength: 30, nullable: false),
            Description = t.Column<string>("character varying(1500)", maxLength: 1500, nullable: false),
            TargetPassingRate = t.Column<decimal>("numeric(5,2)", nullable: false),
            IsActive = t.Column<bool>("boolean", nullable: false),
            Version = t.Column<long>("bigint", nullable: false)
        }, constraints: t =>
        {
            t.PrimaryKey("PK_quality_program_outcomes", x => x.Id);
            t.UniqueConstraint("AK_quality_program_outcomes_Id_ProgramId_InstitutionId", x => new { x.Id, x.ProgramId, x.InstitutionId });
            t.CheckConstraint("CK_p24_target", "\"TargetPassingRate\" BETWEEN 0 AND 100");
            t.ForeignKey("FK_p24_outcome_program", x => new { x.ProgramId, x.InstitutionId },
                "academic_programs", new[] { "Id", "InstitutionId" }, onDelete: ReferentialAction.Restrict);
        });
        m.CreateIndex("IX_quality_program_outcomes_ProgramId_InstitutionId", "quality_program_outcomes", new[] { "ProgramId", "InstitutionId" });
        m.CreateIndex("IX_quality_program_outcomes_InstitutionId_ProgramId_Code", "quality_program_outcomes",
            new[] { "InstitutionId", "ProgramId", "Code" }, unique: true);

        m.CreateTable("quality_course_alignments", columns: t => new
        {
            Id = t.Column<Guid>("uuid", nullable: false),
            InstitutionId = t.Column<Guid>("uuid", nullable: false),
            ProgramId = t.Column<Guid>("uuid", nullable: false),
            OutcomeId = t.Column<Guid>("uuid", nullable: false),
            CourseId = t.Column<Guid>("uuid", nullable: false),
            IsActive = t.Column<bool>("boolean", nullable: false),
            CreatedAtUtc = t.Column<DateTime>("timestamp with time zone", nullable: false)
        }, constraints: t =>
        {
            t.PrimaryKey("PK_quality_course_alignments", x => x.Id);
            t.ForeignKey("FK_p24_map_outcome", x => new { x.OutcomeId, x.ProgramId, x.InstitutionId },
                "quality_program_outcomes", new[] { "Id", "ProgramId", "InstitutionId" }, onDelete: ReferentialAction.Restrict);
            t.ForeignKey("FK_p24_map_course", x => new { x.ProgramId, x.CourseId },
                "program_courses", new[] { "ProgramId", "CourseId" }, onDelete: ReferentialAction.Restrict);
        });
        m.CreateIndex("IX_quality_course_alignments_OutcomeId_ProgramId_InstitutionId", "quality_course_alignments",
            new[] { "OutcomeId", "ProgramId", "InstitutionId" });
        m.CreateIndex("IX_quality_course_alignments_ProgramId_CourseId", "quality_course_alignments", new[] { "ProgramId", "CourseId" });
        m.CreateIndex("IX_quality_course_alignments_InstitutionId_OutcomeId_CourseId", "quality_course_alignments",
            new[] { "InstitutionId", "OutcomeId", "CourseId" }, unique: true);

        m.CreateTable("quality_program_reviews", columns: t => new
        {
            Id = t.Column<Guid>("uuid", nullable: false),
            InstitutionId = t.Column<Guid>("uuid", nullable: false),
            ProgramId = t.Column<Guid>("uuid", nullable: false),
            TermId = t.Column<Guid>("uuid", nullable: false),
            Status = t.Column<string>("character varying(20)", maxLength: 20, nullable: false),
            SnapshotJson = t.Column<string>("text", nullable: false),
            CreatedBy = t.Column<Guid>("uuid", nullable: false),
            CreatedAtUtc = t.Column<DateTime>("timestamp with time zone", nullable: false),
            PublishedAtUtc = t.Column<DateTime>("timestamp with time zone", nullable: true),
            Version = t.Column<long>("bigint", nullable: false)
        }, constraints: t =>
        {
            t.PrimaryKey("PK_quality_program_reviews", x => x.Id);
            t.UniqueConstraint("AK_quality_program_reviews_Id_InstitutionId", x => new { x.Id, x.InstitutionId });
            t.CheckConstraint("CK_p24_review_status", "\"Status\" IN ('Draft','Published')");
            t.ForeignKey("FK_p24_review_program", x => new { x.ProgramId, x.InstitutionId },
                "academic_programs", new[] { "Id", "InstitutionId" }, onDelete: ReferentialAction.Restrict);
            t.ForeignKey("FK_p24_review_term", x => new { x.TermId, x.InstitutionId },
                "academic_terms", new[] { "Id", "InstitutionId" }, onDelete: ReferentialAction.Restrict);
            t.ForeignKey("FK_p24_review_actor", x => x.CreatedBy,
                "identity_users", "Id", onDelete: ReferentialAction.Restrict);
        });
        m.CreateIndex("IX_quality_program_reviews_CreatedBy", "quality_program_reviews", "CreatedBy");
        m.CreateIndex("IX_quality_program_reviews_ProgramId_InstitutionId", "quality_program_reviews", new[] { "ProgramId", "InstitutionId" });
        m.CreateIndex("IX_quality_program_reviews_TermId_InstitutionId", "quality_program_reviews", new[] { "TermId", "InstitutionId" });
        m.CreateIndex("IX_quality_program_reviews_InstitutionId_ProgramId_TermId", "quality_program_reviews",
            new[] { "InstitutionId", "ProgramId", "TermId" }, unique: true);

        m.CreateTable("quality_improvement_actions", columns: t => new
        {
            Id = t.Column<Guid>("uuid", nullable: false),
            InstitutionId = t.Column<Guid>("uuid", nullable: false),
            ReviewId = t.Column<Guid>("uuid", nullable: false),
            OutcomeId = t.Column<Guid>("uuid", nullable: true),
            Description = t.Column<string>("character varying(1500)", maxLength: 1500, nullable: false),
            Status = t.Column<string>("character varying(20)", maxLength: 20, nullable: false),
            DueOn = t.Column<DateOnly>("date", nullable: true),
            ResolutionNote = t.Column<string>("character varying(1500)", maxLength: 1500, nullable: false),
            CreatedBy = t.Column<Guid>("uuid", nullable: false),
            CompletedBy = t.Column<Guid>("uuid", nullable: true),
            CreatedAtUtc = t.Column<DateTime>("timestamp with time zone", nullable: false),
            CompletedAtUtc = t.Column<DateTime>("timestamp with time zone", nullable: true),
            Version = t.Column<long>("bigint", nullable: false)
        }, constraints: t =>
        {
            t.PrimaryKey("PK_quality_improvement_actions", x => x.Id);
            t.CheckConstraint("CK_p24_action_status", "\"Status\" IN ('Open','InProgress','Completed')");
            t.ForeignKey("FK_p24_action_review", x => new { x.ReviewId, x.InstitutionId },
                "quality_program_reviews", new[] { "Id", "InstitutionId" }, onDelete: ReferentialAction.Restrict);
            t.ForeignKey("FK_p24_action_created_by", x => x.CreatedBy,
                "identity_users", "Id", onDelete: ReferentialAction.Restrict);
            t.ForeignKey("FK_p24_action_completed_by", x => x.CompletedBy,
                "identity_users", "Id", onDelete: ReferentialAction.Restrict);
        });
        m.CreateIndex("IX_quality_improvement_actions_ReviewId_InstitutionId", "quality_improvement_actions",
            new[] { "ReviewId", "InstitutionId" });
        m.CreateIndex("IX_quality_improvement_actions_CreatedBy", "quality_improvement_actions", "CreatedBy");
        m.CreateIndex("IX_quality_improvement_actions_CompletedBy", "quality_improvement_actions", "CompletedBy");
        m.CreateIndex("IX_quality_improvement_actions_InstitutionId_ReviewId_Status", "quality_improvement_actions",
            new[] { "InstitutionId", "ReviewId", "Status" });
    }

    protected override void Down(MigrationBuilder m)
    {
        m.DropTable("quality_improvement_actions");
        m.DropTable("quality_course_alignments");
        m.DropTable("quality_program_reviews");
        m.DropTable("quality_program_outcomes");
    }
}
