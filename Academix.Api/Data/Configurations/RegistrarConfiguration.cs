using Academix.Api.Domain.Academic;
using Academix.Api.Domain.Registrar;
using Academix.Api.Domain.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Academix.Api.Data;

public static class RegistrarConfiguration
{
    private static void Tenant<T>(EntityTypeBuilder<T> e, string table) where T : class
    {
        e.ToTable(table); e.HasKey("Id"); e.HasAlternateKey("Id", "InstitutionId");
        e.HasOne<Institution>().WithMany().HasForeignKey("InstitutionId").OnDelete(DeleteBehavior.Restrict);
    }
    public static void Apply(ModelBuilder b)
    {
        var p = b.Entity<AcademicPolicyRevision>(); Tenant(p, "registrar_policy_revisions");
        p.Property(x => x.RulesJson).HasColumnType("jsonb"); p.Property(x => x.Status).HasMaxLength(20);
        p.HasOne<AcademicProgram>().WithMany().HasForeignKey(x => new { x.ProgramId, x.InstitutionId }).HasPrincipalKey(x => new { x.Id, x.InstitutionId }).OnDelete(DeleteBehavior.Restrict);
        p.HasIndex(x => new { x.InstitutionId, x.ProgramId, x.EffectiveFromUtc });
        p.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);
        p.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.PublishedBy).OnDelete(DeleteBehavior.Restrict);
        p.ToTable(t => { t.HasCheckConstraint("CK_registrar_policy_interval", "\"EffectiveFromUtc\" < \"EffectiveUntilUtc\"");
            t.HasCheckConstraint("CK_registrar_policy_state", "\"Status\" IN ('Draft','Published') AND ((\"Status\" = 'Draft' AND \"PublishedBy\" IS NULL AND \"PublishedAtUtc\" IS NULL) OR (\"Status\" = 'Published' AND \"PublishedBy\" IS NOT NULL AND \"PublishedAtUtc\" IS NOT NULL AND \"PublishedBy\" <> \"CreatedBy\"))"); });
        var h = b.Entity<StudentAcademicHold>(); Tenant(h, "registrar_student_holds");
        h.Property(x => x.Reason).HasMaxLength(500); h.HasIndex(x => new { x.InstitutionId, x.StudentId, x.ReleasedAtUtc });
        h.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
        h.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);
        h.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ReleasedBy).OnDelete(DeleteBehavior.Restrict);
        var e = b.Entity<ApprovedAcademicException>(); Tenant(e, "registrar_exceptions");
        e.Property(x => x.Reason).HasMaxLength(500); e.Property(x => x.Rule).HasMaxLength(30); e.Property(x => x.Status).HasMaxLength(20);
        e.HasOne<AcademicPolicyRevision>().WithMany().HasForeignKey(x => new { x.PolicyId, x.InstitutionId }).HasPrincipalKey(x => new { x.Id, x.InstitutionId }).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<CourseSection>().WithMany().HasForeignKey(x => new { x.SectionId, x.InstitutionId }).HasPrincipalKey(x => new { x.Id, x.InstitutionId }).OnDelete(DeleteBehavior.Restrict);
        e.HasIndex(x => new { x.InstitutionId, x.StudentId, x.SectionId });
        e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.RequestedBy).OnDelete(DeleteBehavior.Restrict);
        e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ReviewedBy).OnDelete(DeleteBehavior.Restrict);
        e.ToTable(t => t.HasCheckConstraint("CK_registrar_exception", "\"Status\" IN ('Pending','Approved','Rejected') AND \"Rule\" IN ('Prerequisites','CreditLoad','EnrollmentApproval')"));
        var d = b.Entity<EligibilityDecision>(); Tenant(d, "registrar_eligibility_decisions");
        d.Property(x => x.SnapshotJson).HasColumnType("jsonb");
        d.HasOne<AcademicPolicyRevision>().WithMany().HasForeignKey(x => new { x.PolicyId, x.InstitutionId }).HasPrincipalKey(x => new { x.Id, x.InstitutionId }).OnDelete(DeleteBehavior.Restrict);
        d.HasOne<CourseSection>().WithMany().HasForeignKey(x => new { x.SectionId, x.InstitutionId }).HasPrincipalKey(x => new { x.Id, x.InstitutionId }).OnDelete(DeleteBehavior.Restrict);
        d.HasIndex(x => new { x.InstitutionId, x.StudentId, x.AtUtc });
        d.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
    }
}
