using Academix.Api.Domain.Academic;
using Academix.Api.Domain.Advising;
using Academix.Api.Domain.Courses;
using Academix.Api.Domain.Curriculum;
using Academix.Api.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Academix.Api.Data;

public static class AcademicProgressConfiguration
{
    public static void Apply(ModelBuilder b)
    {
        EntityTypeBuilder<T> Entity<T>(string table) where T:class
        {
            var e=b.Entity<T>();e.ToTable(table);e.HasKey("Id");e.HasAlternateKey("Id","InstitutionId");
            e.HasOne<Institution>().WithMany().HasForeignKey("InstitutionId").OnDelete(DeleteBehavior.Restrict);return e;
        }
        void User<T>(EntityTypeBuilder<T> e,string property) where T:class
            =>e.HasOne<ApplicationUser>().WithMany().HasForeignKey(property).OnDelete(DeleteBehavior.Restrict);
        var policy=b.Entity<AcademicAdvisingPolicy>();policy.ToTable("academic_advising_policies",t=>t.HasCheckConstraint("CK_p20_advising_policy",
            "(\"MaximumAdvisees\" IS NULL OR \"MaximumAdvisees\" BETWEEN 1 AND 10000) AND \"RepeatedFailureThreshold\" BETWEEN 2 AND 20 AND \"MinimumProgressCredits\" BETWEEN 0 AND 500"));
        policy.HasKey(x=>x.InstitutionId);policy.HasOne<Institution>().WithMany().HasForeignKey(x=>x.InstitutionId).OnDelete(DeleteBehavior.Restrict);policy.Property(x=>x.Version).IsConcurrencyToken();
        var a=Entity<AdvisorAssignment>("academic_advisor_assignments");
        a.HasIndex(x=>new{x.InstitutionId,x.StudentId}).IsUnique().HasFilter("\"EndedAtUtc\" IS NULL");
        a.HasIndex(x=>new{x.InstitutionId,x.AdvisorId,x.EndedAtUtc});a.Property(x=>x.Reason).HasMaxLength(500);
        a.ToTable(t=>t.HasCheckConstraint("CK_p20_advisor","\"StudentId\" <> \"AdvisorId\" AND (\"EndedAtUtc\" IS NULL OR \"EndedAtUtc\" >= \"AssignedAtUtc\")"));
        User(a,nameof(AdvisorAssignment.StudentId));User(a,nameof(AdvisorAssignment.AdvisorId));User(a,nameof(AdvisorAssignment.AssignedBy));
        var s=Entity<AcademicAdvisingSession>("academic_advising_sessions");
        s.HasIndex(x=>new{x.InstitutionId,x.StudentId,x.ClientRequestId}).IsUnique();s.HasIndex(x=>new{x.InstitutionId,x.StudentId,x.ScheduledAtUtc});
        s.Property(x=>x.Version).IsConcurrencyToken();s.Property(x=>x.Status).HasMaxLength(20);s.Property(x=>x.InternalNotes).HasMaxLength(4000);s.Property(x=>x.Recommendation).HasMaxLength(2000);
        s.ToTable(t=>t.HasCheckConstraint("CK_p20_session","\"Status\" IN ('Scheduled','Completed','Cancelled') AND \"StudentId\" <> \"AdvisorId\" AND (\"AcknowledgedAtUtc\" IS NULL OR \"Status\" = 'Completed')"));
        User(s,nameof(AcademicAdvisingSession.StudentId));User(s,nameof(AcademicAdvisingSession.AdvisorId));
        var p=Entity<StudentStudyPlan>("student_study_plans");p.HasIndex(x=>new{x.InstitutionId,x.StudentId}).IsUnique();p.Property(x=>x.Version).IsConcurrencyToken();
        p.HasOne<CurriculumPlan>().WithMany().HasForeignKey(x=>new{x.CurriculumPlanId,x.InstitutionId}).HasPrincipalKey(x=>new{x.Id,x.InstitutionId}).OnDelete(DeleteBehavior.Restrict);User(p,nameof(StudentStudyPlan.StudentId));
        var r=Entity<StudyPlanRevision>("study_plan_revisions");r.HasIndex(x=>new{x.StudyPlanId,x.Revision}).IsUnique();
        r.HasAlternateKey(x=>new{x.StudyPlanId,x.Revision,x.InstitutionId});r.Property(x=>x.Status).HasMaxLength(20);r.Property(x=>x.Rationale).HasMaxLength(1000);r.Property(x=>x.Recommendation).HasMaxLength(2000);
        r.HasOne<StudentStudyPlan>().WithMany().HasForeignKey(x=>new{x.StudyPlanId,x.InstitutionId}).HasPrincipalKey(x=>new{x.Id,x.InstitutionId}).OnDelete(DeleteBehavior.Restrict);
        r.HasOne<CurriculumPlan>().WithMany().HasForeignKey(x=>new{x.CurriculumPlanId,x.InstitutionId}).HasPrincipalKey(x=>new{x.Id,x.InstitutionId}).OnDelete(DeleteBehavior.Restrict);
        r.ToTable(t=>t.HasCheckConstraint("CK_p20_plan_revision","\"Revision\" > 0 AND \"Status\" IN ('Proposed','Approved','Rejected','Acknowledged') AND (\"Status\" NOT IN ('Approved','Acknowledged') OR \"ReviewedBy\" IS NOT NULL) AND (\"Status\" <> 'Acknowledged' OR \"AcknowledgedAtUtc\" IS NOT NULL)"));
        User(r,nameof(StudyPlanRevision.CreatedBy));User(r,nameof(StudyPlanRevision.ReviewedBy));
        p.HasOne<StudyPlanRevision>().WithMany().HasForeignKey(x=>new{x.Id,x.ApprovedRevision,x.InstitutionId}).HasPrincipalKey(x=>new{x.StudyPlanId,x.Revision,x.InstitutionId}).OnDelete(DeleteBehavior.Restrict);
        var pc=Entity<StudyPlanCourse>("study_plan_courses");pc.HasIndex(x=>new{x.RevisionId,x.CourseId}).IsUnique();
        pc.HasOne<StudyPlanRevision>().WithMany().HasForeignKey(x=>new{x.RevisionId,x.InstitutionId}).HasPrincipalKey(x=>new{x.Id,x.InstitutionId}).OnDelete(DeleteBehavior.Restrict);
        pc.HasOne<Course>().WithMany().HasForeignKey(x=>new{x.CourseId,x.InstitutionId}).HasPrincipalKey(x=>new{x.Id,x.InstitutionId}).OnDelete(DeleteBehavior.Restrict);
        pc.HasOne<AcademicTerm>().WithMany().HasForeignKey(x=>new{x.TermId,x.InstitutionId}).HasPrincipalKey(x=>new{x.Id,x.InstitutionId}).OnDelete(DeleteBehavior.Restrict);
        var g=Entity<CurriculumElectiveGroup>("curriculum_elective_groups");g.HasAlternateKey(x=>new{x.Id,x.PlanId,x.InstitutionId});g.HasIndex(x=>new{x.PlanId,x.Name}).IsUnique();g.Property(x=>x.Name).HasMaxLength(120);
        g.HasOne<CurriculumPlan>().WithMany().HasForeignKey(x=>new{x.PlanId,x.InstitutionId}).HasPrincipalKey(x=>new{x.Id,x.InstitutionId}).OnDelete(DeleteBehavior.Restrict);
        g.ToTable(t=>t.HasCheckConstraint("CK_p20_group_credits","\"MinimumCredits\" > 0 AND \"MinimumCredits\" <= 500"));
        var gc=Entity<CurriculumElectiveGroupCourse>("curriculum_elective_group_courses");gc.HasIndex(x=>new{x.PlanId,x.CourseId}).IsUnique();
        gc.HasOne<CurriculumElectiveGroup>().WithMany().HasForeignKey(x=>new{x.GroupId,x.PlanId,x.InstitutionId}).HasPrincipalKey(x=>new{x.Id,x.PlanId,x.InstitutionId}).OnDelete(DeleteBehavior.Restrict);
        b.Entity<CurriculumCourse>().HasAlternateKey(x=>new{x.PlanId,x.CourseId,x.InstitutionId});
        gc.HasOne<CurriculumCourse>().WithMany().HasForeignKey(x=>new{x.PlanId,x.CourseId,x.InstitutionId}).HasPrincipalKey(x=>new{x.PlanId,x.CourseId,x.InstitutionId}).OnDelete(DeleteBehavior.Restrict);
        var ex=Entity<AcademicRequirementException>("academic_requirement_exceptions");ex.Property(x=>x.Version).IsConcurrencyToken();ex.Property(x=>x.Kind).HasMaxLength(20);ex.Property(x=>x.Status).HasMaxLength(20);ex.Property(x=>x.Reason).HasMaxLength(1000);ex.Property(x=>x.DecisionReason).HasMaxLength(1000);
        ex.HasIndex(x=>new{x.InstitutionId,x.StudentId,x.PlanId,x.TargetCourseId}).IsUnique().HasFilter("\"Status\" IN ('Submitted','Approved')");
        ex.HasIndex(x=>new{x.InstitutionId,x.StudentId,x.PlanId,x.SourceCourseId}).IsUnique().HasFilter("\"Status\" = 'Approved' AND \"SourceCourseId\" IS NOT NULL");
        ex.HasOne<CurriculumCourse>().WithMany().HasForeignKey(x=>new{x.PlanId,x.TargetCourseId,x.InstitutionId}).HasPrincipalKey(x=>new{x.PlanId,x.CourseId,x.InstitutionId}).OnDelete(DeleteBehavior.Restrict);
        ex.HasOne<Course>().WithMany().HasForeignKey(x=>new{x.SourceCourseId,x.InstitutionId}).HasPrincipalKey(x=>new{x.Id,x.InstitutionId}).OnDelete(DeleteBehavior.Restrict);
        ex.ToTable(t=>t.HasCheckConstraint("CK_p20_exception","\"Status\" IN ('Submitted','Approved','Rejected') AND ((\"Kind\"='Waiver' AND \"SourceCourseId\" IS NULL) OR (\"Kind\"='Substitution' AND \"SourceCourseId\" IS NOT NULL AND \"SourceCourseId\" <> \"TargetCourseId\")) AND (\"Status\" <> 'Approved' OR (\"ReviewedBy\" IS NOT NULL AND \"ReviewedBy\" <> \"RequestedBy\" AND \"ReviewedBy\" <> \"StudentId\"))"));
        User(ex,nameof(AcademicRequirementException.StudentId));User(ex,nameof(AcademicRequirementException.RequestedBy));User(ex,nameof(AcademicRequirementException.ReviewedBy));
        var d=Entity<DepartmentGraduationReviewer>("department_graduation_reviewers");d.HasIndex(x=>new{x.DepartmentId,x.ReviewerId}).IsUnique();
        d.HasOne<Department>().WithMany().HasForeignKey(x=>new{x.DepartmentId,x.InstitutionId}).HasPrincipalKey(x=>new{x.Id,x.InstitutionId}).OnDelete(DeleteBehavior.Restrict);User(d,nameof(DepartmentGraduationReviewer.ReviewerId));
        var graduation=b.Entity<Academix.Api.Domain.Lifecycle.GraduationApplication>();
        graduation.HasIndex(x=>new{x.InstitutionId,x.StudentId,x.ClientRequestId}).IsUnique().HasFilter("\"ClientRequestId\" IS NOT NULL");
        graduation.Property(x=>x.ApprovalEvidenceJson).HasColumnType("jsonb");graduation.Property(x=>x.FinalEvidenceJson).HasColumnType("jsonb");
        graduation.Property(x=>x.DepartmentReviewReason).HasMaxLength(1000);
        graduation.HasOne<ApplicationUser>().WithMany().HasForeignKey(x=>x.DepartmentReviewedById).OnDelete(DeleteBehavior.Restrict);
        graduation.ToTable(t=>t.HasCheckConstraint("CK_p20_graduation_evidence","\"ClientRequestId\" IS NULL OR \"Status\" <> 'Issued' OR \"FinalEvidenceJson\" IS NOT NULL"));
    }
}
