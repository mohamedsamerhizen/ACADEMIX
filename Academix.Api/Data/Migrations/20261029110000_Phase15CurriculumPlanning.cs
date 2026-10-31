using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable
namespace Academix.Api.Data.Migrations;

public partial class Phase15CurriculumPlanning : Migration
{
    protected override void Up(MigrationBuilder m)
    {
        m.CreateTable("curriculum_plans", columns: t => new
        {
            Id = t.Column<Guid>(type: "uuid", nullable: false),
            InstitutionId = t.Column<Guid>(type: "uuid", nullable: false),
            ProgramId = t.Column<Guid>(type: "uuid", nullable: false),
            CatalogYear = t.Column<int>(type: "integer", nullable: false),
            Revision = t.Column<int>(type: "integer", nullable: false),
            Name = t.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
            MinimumTotalCredits = t.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
            MinimumElectiveCredits = t.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: false),
            MinimumCumulativeGpa = t.Column<decimal>(type: "numeric(3,2)", precision: 3, scale: 2, nullable: false),
            Status = t.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
            Version = t.Column<long>(type: "bigint", nullable: false),
            PublishedAtUtc = t.Column<DateTime>(type: "timestamp with time zone", nullable: true),
            CreatedAtUtc = t.Column<DateTime>(type: "timestamp with time zone", nullable: false)
        }, constraints: t =>
        {
            t.PrimaryKey("PK_curriculum_plans", x => x.Id);
            t.UniqueConstraint("AK_curriculum_plans_Id_InstitutionId", x => new { x.Id, x.InstitutionId });
            t.CheckConstraint("CK_p15_plan_status", "\"Status\" IN ('Draft','Published')");
            t.ForeignKey("FK_p15_plan_program", x => new { x.ProgramId, x.InstitutionId },
                "academic_programs", new[] { "Id", "InstitutionId" }, onDelete: ReferentialAction.Restrict);
        });
        m.CreateIndex("IX_p15_plan_revision", "curriculum_plans", new[] { "InstitutionId", "ProgramId", "CatalogYear", "Revision" }, unique: true);
        m.CreateIndex("IX_curriculum_plans_ProgramId_InstitutionId", "curriculum_plans", new[] { "ProgramId", "InstitutionId" });

        m.CreateTable("curriculum_courses", columns: t => new
        {
            Id = t.Column<Guid>(type: "uuid", nullable: false),
            InstitutionId = t.Column<Guid>(type: "uuid", nullable: false),
            PlanId = t.Column<Guid>(type: "uuid", nullable: false),
            CourseId = t.Column<Guid>(type: "uuid", nullable: false),
            RequirementKind = t.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
            CreditHours = t.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
            RecommendedTerm = t.Column<int>(type: "integer", nullable: false)
        }, constraints: t =>
        {
            t.PrimaryKey("PK_curriculum_courses", x => x.Id);
            t.CheckConstraint("CK_p15_course_kind", "\"RequirementKind\" IN ('Required','Elective')");
            t.ForeignKey("FK_p15_course_plan", x => new { x.PlanId, x.InstitutionId },
                "curriculum_plans", new[] { "Id", "InstitutionId" }, onDelete: ReferentialAction.Restrict);
            t.ForeignKey("FK_p15_course_course", x => new { x.CourseId, x.InstitutionId },
                "courses", new[] { "id", "institution_id" }, onDelete: ReferentialAction.Restrict);
        });
        m.CreateIndex("IX_p15_plan_course_unique", "curriculum_courses", new[] { "InstitutionId", "PlanId", "CourseId" }, unique: true);
        m.CreateIndex("IX_curriculum_courses_PlanId_InstitutionId", "curriculum_courses", new[] { "PlanId", "InstitutionId" });
        m.CreateIndex("IX_curriculum_courses_CourseId_InstitutionId", "curriculum_courses", new[] { "CourseId", "InstitutionId" });

        m.CreateTable("curriculum_grade_bands", columns: t => new
        {
            Id = t.Column<Guid>(type: "uuid", nullable: false),
            InstitutionId = t.Column<Guid>(type: "uuid", nullable: false),
            PlanId = t.Column<Guid>(type: "uuid", nullable: false),
            MinimumPercentage = t.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
            GradePoints = t.Column<decimal>(type: "numeric(3,2)", precision: 3, scale: 2, nullable: false),
            Letter = t.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false)
        }, constraints: t =>
        {
            t.PrimaryKey("PK_curriculum_grade_bands", x => x.Id);
            t.ForeignKey("FK_p15_grade_band_plan", x => new { x.PlanId, x.InstitutionId },
                "curriculum_plans", new[] { "Id", "InstitutionId" }, onDelete: ReferentialAction.Restrict);
        });
        m.CreateIndex("IX_p15_plan_grade_band", "curriculum_grade_bands", new[] { "InstitutionId", "PlanId", "MinimumPercentage" }, unique: true);
        m.CreateIndex("IX_curriculum_grade_bands_PlanId_InstitutionId", "curriculum_grade_bands", new[] { "PlanId", "InstitutionId" });

        m.CreateTable("student_curriculum_assignments", columns: t => new
        {
            Id = t.Column<Guid>(type: "uuid", nullable: false),
            InstitutionId = t.Column<Guid>(type: "uuid", nullable: false),
            StudentId = t.Column<Guid>(type: "uuid", nullable: false),
            PlanId = t.Column<Guid>(type: "uuid", nullable: false),
            AssignedById = t.Column<Guid>(type: "uuid", nullable: false),
            AssignedAtUtc = t.Column<DateTime>(type: "timestamp with time zone", nullable: false)
        }, constraints: t =>
        {
            t.PrimaryKey("PK_student_curriculum_assignments", x => x.Id);
            t.ForeignKey("FK_p15_assignment_plan", x => new { x.PlanId, x.InstitutionId },
                "curriculum_plans", new[] { "Id", "InstitutionId" }, onDelete: ReferentialAction.Restrict);
            t.ForeignKey("FK_p15_assignment_student", x => x.StudentId,
                "identity_users", "Id", onDelete: ReferentialAction.Restrict);
            t.ForeignKey("FK_p15_assignment_actor", x => x.AssignedById,
                "identity_users", "Id", onDelete: ReferentialAction.Restrict);
        });
        m.CreateIndex("IX_p15_student_assignment_unique", "student_curriculum_assignments", new[] { "InstitutionId", "StudentId" }, unique: true);
        m.CreateIndex("IX_student_curriculum_assignments_PlanId_InstitutionId", "student_curriculum_assignments", new[] { "PlanId", "InstitutionId" });
        m.CreateIndex("IX_student_curriculum_assignments_StudentId", "student_curriculum_assignments", "StudentId");
        m.CreateIndex("IX_student_curriculum_assignments_AssignedById", "student_curriculum_assignments", "AssignedById");
    }

    protected override void Down(MigrationBuilder m)
    {
        m.DropTable("student_curriculum_assignments");
        m.DropTable("curriculum_grade_bands");
        m.DropTable("curriculum_courses");
        m.DropTable("curriculum_plans");
    }
}
