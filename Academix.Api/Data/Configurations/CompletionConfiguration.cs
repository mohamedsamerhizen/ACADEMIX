using Academix.Api.Domain.Academic;
using Academix.Api.Domain.AcademicOperations;
using Academix.Api.Domain.Curriculum;
using Academix.Api.Domain.Identity;
using Academix.Api.Domain.Institutional;
using Academix.Api.Domain.Registrar;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Academix.Api.Data;

public static class CompletionConfiguration
{
    private static void Tenant<T>(EntityTypeBuilder<T> e, string table) where T:class
    {
        e.ToTable(table); e.HasKey("Id"); e.HasAlternateKey("Id","InstitutionId");
        e.HasOne<Institution>().WithMany().HasForeignKey("InstitutionId").OnDelete(DeleteBehavior.Restrict);
    }
    public static void Apply(ModelBuilder b)
    {
        var a=b.Entity<Applicant>(); Tenant(a,"registrar_applicants");
        a.HasIndex(x=>new{x.InstitutionId,x.ClientRequestId}).IsUnique(); a.HasIndex(x=>new{x.InstitutionId,x.NormalizedEmail}).IsUnique();
        a.HasIndex(x=>x.InvitationHash).IsUnique(); a.HasIndex(x=>x.StudentId).IsUnique();
        a.Property(x=>x.Email).HasMaxLength(256);a.Property(x=>x.NormalizedEmail).HasMaxLength(256);a.Property(x=>x.FullName).HasMaxLength(200);
        a.Property(x=>x.Statement).HasMaxLength(3000);a.Property(x=>x.Status).HasMaxLength(20);a.Property(x=>x.InvitationHash).HasMaxLength(64);
        a.Property(x=>x.DecisionReason).HasMaxLength(500);a.Property(x=>x.Version).IsConcurrencyToken();
        a.HasOne<AcademicProgram>().WithMany().HasForeignKey(x=>new{x.ProgramId,x.InstitutionId}).HasPrincipalKey(x=>new{x.Id,x.InstitutionId}).OnDelete(DeleteBehavior.Restrict);
        a.HasOne<AcademicTerm>().WithMany().HasForeignKey(x=>new{x.TermId,x.InstitutionId}).HasPrincipalKey(x=>new{x.Id,x.InstitutionId}).OnDelete(DeleteBehavior.Restrict);
        a.HasOne<ApplicationUser>().WithMany().HasForeignKey(x=>x.StudentId).OnDelete(DeleteBehavior.Restrict);
        a.HasOne<ApplicationUser>().WithMany().HasForeignKey(x=>x.CreatedBy).OnDelete(DeleteBehavior.Restrict);
        a.HasOne<ApplicationUser>().WithMany().HasForeignKey(x=>x.ReviewedBy).OnDelete(DeleteBehavior.Restrict);
        a.HasOne<Academix.Api.Domain.Lifecycle.AdmissionApplication>().WithMany().HasForeignKey(x=>x.AdmissionId).OnDelete(DeleteBehavior.Restrict);
        a.ToTable(t=>t.HasCheckConstraint("CK_applicant_conversion","\"Status\" <> 'Converted' OR (\"StudentId\" IS NOT NULL AND \"AdmissionId\" IS NOT NULL AND \"VerifiedAtUtc\" IS NOT NULL AND \"ReviewedBy\" IS NOT NULL)"));
        a.ToTable(t=>t.HasCheckConstraint("CK_applicant_status","\"Status\" IN ('Invited','Submitted','Accepted','Rejected','Converted')"));
        var h=b.Entity<ApplicantHistory>();Tenant(h,"registrar_applicant_history");h.Property(x=>x.Status).HasMaxLength(20);
        h.HasOne<ApplicationUser>().WithMany().HasForeignKey(x=>x.ActorId).OnDelete(DeleteBehavior.Restrict);
        h.HasOne<Applicant>().WithMany().HasForeignKey(x=>new{x.ApplicantId,x.InstitutionId}).HasPrincipalKey(x=>new{x.Id,x.InstitutionId}).OnDelete(DeleteBehavior.Restrict);
        var p=b.Entity<StudentProfile>();p.ToTable("registrar_student_profiles");p.HasKey(x=>x.StudentId);p.HasAlternateKey(x=>new{x.StudentId,x.InstitutionId});
        p.Property(x=>x.FullName).HasMaxLength(200);p.Property(x=>x.ExternalReference).HasMaxLength(100);p.Property(x=>x.Version).IsConcurrencyToken();
        p.HasOne<ApplicationUser>().WithMany().HasForeignKey(x=>x.StudentId).OnDelete(DeleteBehavior.Restrict);
        p.HasOne<Institution>().WithMany().HasForeignKey(x=>x.InstitutionId).OnDelete(DeleteBehavior.Restrict);
        var t=b.Entity<DocumentTemplateRevision>();Tenant(t,"institutional_document_templates");t.HasIndex(x=>new{x.InstitutionId,x.Kind,x.Revision}).IsUnique();
        t.Property(x=>x.Kind).HasMaxLength(20);t.Property(x=>x.Heading).HasMaxLength(200);t.Property(x=>x.Footer).HasMaxLength(1000);t.Property(x=>x.AccentColor).HasMaxLength(7);t.Property(x=>x.FontFamily).HasMaxLength(100);
        t.ToTable(x=>x.HasCheckConstraint("CK_document_template_revision","\"Revision\" > 0"));
        t.HasOne<ApplicationUser>().WithMany().HasForeignKey(x=>x.CreatedBy).OnDelete(DeleteBehavior.Restrict);
        var k=b.Entity<DocumentVerificationKey>();Tenant(k,"institutional_verification_keys");k.HasIndex(x=>x.InstitutionId).IsUnique().HasFilter("\"RetiredAtUtc\" IS NULL AND \"RevokedAtUtc\" IS NULL");
        var d=b.Entity<IssuedAcademicDocument>();d.Property(x=>x.ArtifactDigest).HasMaxLength(64);d.Property(x=>x.LegacyProof).HasMaxLength(64);d.Property(x=>x.ArtifactKey).HasMaxLength(400);
        d.Property(x=>x.ArtifactSignature).HasMaxLength(64);
        d.ToTable(t=>t.HasCheckConstraint("CK_p19_pdf_issuance","\"VerificationKeyId\" IS NULL OR (\"TemplateId\" IS NOT NULL AND \"ArtifactKey\" IS NOT NULL AND \"ArtifactDigest\" IS NOT NULL AND \"LegacyProof\" IS NOT NULL AND \"ArtifactSignature\" IS NOT NULL AND \"ArtifactSignature\" ~ '^[0-9A-F]{64}$' AND \"ArtifactDigest\" ~ '^[0-9A-F]{64}$' AND \"LegacyProof\" ~ '^[0-9A-F]{64}$')"));
        d.HasOne<DocumentTemplateRevision>().WithMany().HasForeignKey(x=>new{x.TemplateId,x.InstitutionId}).HasPrincipalKey(x=>new{x.Id,x.InstitutionId}).OnDelete(DeleteBehavior.Restrict);
        d.HasOne<DocumentVerificationKey>().WithMany().HasForeignKey(x=>new{x.VerificationKeyId,x.InstitutionId}).HasPrincipalKey(x=>new{x.Id,x.InstitutionId}).OnDelete(DeleteBehavior.Restrict);
        d.HasOne<IssuedAcademicDocument>().WithMany().HasForeignKey(x=>new{x.SupersededById,x.InstitutionId}).HasPrincipalKey(x=>new{x.Id,x.InstitutionId}).OnDelete(DeleteBehavior.Restrict);
        var c=b.Entity<CurriculumTransition>();Tenant(c,"registrar_curriculum_transitions");c.HasIndex(x=>new{x.InstitutionId,x.StudentId,x.ClientRequestId}).IsUnique();
        c.Property(x=>x.MappingJson).HasColumnType("jsonb");c.Property(x=>x.PriorAssignmentJson).HasColumnType("jsonb");c.Property(x=>x.Reason).HasMaxLength(500);c.Property(x=>x.Status).HasMaxLength(20);
        c.HasOne<CurriculumPlan>().WithMany().HasForeignKey(x=>new{x.FromPlanId,x.InstitutionId}).HasPrincipalKey(x=>new{x.Id,x.InstitutionId}).OnDelete(DeleteBehavior.Restrict);
        c.HasOne<CurriculumPlan>().WithMany().HasForeignKey(x=>new{x.ToPlanId,x.InstitutionId}).HasPrincipalKey(x=>new{x.Id,x.InstitutionId}).OnDelete(DeleteBehavior.Restrict);
        c.HasOne<ApplicationUser>().WithMany().HasForeignKey(x=>x.StudentId).OnDelete(DeleteBehavior.Restrict);
        c.HasOne<ApplicationUser>().WithMany().HasForeignKey(x=>x.RequestedBy).OnDelete(DeleteBehavior.Restrict);
        c.HasOne<ApplicationUser>().WithMany().HasForeignKey(x=>x.ApprovedBy).OnDelete(DeleteBehavior.Restrict);
        c.ToTable(t=>t.HasCheckConstraint("CK_transition_status","\"Status\" IN ('Pending','Approved','Rejected','Applied') AND \"FromPlanId\" <> \"ToPlanId\""));
        var credit=b.Entity<RecognizedCourseCredit>();Tenant(credit,"registrar_recognized_credits");credit.HasIndex(x=>new{x.InstitutionId,x.StudentId,x.PlanId,x.TargetCourseId}).IsUnique();
        credit.HasIndex(x=>new{x.TransitionId,x.SourceCourseId}).IsUnique();credit.Property(x=>x.CreditHours).HasPrecision(6,2);
        credit.HasOne<CurriculumTransition>().WithMany().HasForeignKey(x=>new{x.TransitionId,x.InstitutionId}).HasPrincipalKey(x=>new{x.Id,x.InstitutionId}).OnDelete(DeleteBehavior.Restrict);
        credit.HasOne<CurriculumPlan>().WithMany().HasForeignKey(x=>new{x.PlanId,x.InstitutionId}).HasPrincipalKey(x=>new{x.Id,x.InstitutionId}).OnDelete(DeleteBehavior.Restrict);
        credit.HasOne<ApplicationUser>().WithMany().HasForeignKey(x=>x.StudentId).OnDelete(DeleteBehavior.Restrict);
        credit.ToTable(t=>t.HasCheckConstraint("CK_recognized_positive_credit","\"CreditHours\" > 0"));
        foreach(var prop in new[]{"SourceCourseId","TargetCourseId"})credit.HasOne<Academix.Api.Domain.Courses.Course>().WithMany().HasForeignKey(prop,"InstitutionId").HasPrincipalKey("Id","InstitutionId").OnDelete(DeleteBehavior.Restrict);
        var receipt=b.Entity<InstitutionalEventReceipt>();receipt.ToTable("institutional_event_receipts");receipt.HasKey(x=>x.EventId);receipt.Property(x=>x.Kind).HasMaxLength(50);receipt.Property(x=>x.PayloadJson).HasColumnType("jsonb");
        receipt.HasOne<InstitutionalOutboxEvent>().WithMany().HasForeignKey(x=>new{x.EventId,x.InstitutionId}).HasPrincipalKey(x=>new{x.Id,x.InstitutionId}).OnDelete(DeleteBehavior.Restrict);
        receipt.HasIndex(x=>new{x.InstitutionId,x.ReceivedAtUtc});
        var o=b.Entity<InstitutionalOutboxEvent>();o.Property(x=>x.Status).HasMaxLength(20);o.Property(x=>x.DeduplicationKey).HasMaxLength(150);o.Property(x=>x.LastError).HasMaxLength(200);
        o.HasIndex(x=>new{x.InstitutionId,x.DeduplicationKey}).IsUnique();o.HasIndex(x=>new{x.Status,x.NextAttemptAtUtc});
        o.HasOne<Institution>().WithMany().HasForeignKey(x=>x.InstitutionId).OnDelete(DeleteBehavior.Restrict);
        o.ToTable(x=>x.HasCheckConstraint("CK_outbox_delivery","\"Status\" IN ('Pending','Processing','Retry','Delivered','Cancelled') AND \"Attempts\" >= 0"));
        b.Entity<InstitutionalDocumentRequest>().HasOne<IssuedAcademicDocument>().WithMany().HasForeignKey(x=>new{x.SupersedesDocumentId,x.InstitutionId}).HasPrincipalKey(x=>new{x.Id,x.InstitutionId}).OnDelete(DeleteBehavior.Restrict);
        var request=b.Entity<StudentAcademicRequest>();request.Property(x=>x.ExecutionDataJson).HasColumnType("jsonb");request.Property(x=>x.FulfillmentResult).HasMaxLength(200);
        request.HasOne<ApplicationUser>().WithMany().HasForeignKey(x=>x.FulfilledBy).OnDelete(DeleteBehavior.Restrict);
        request.HasIndex(x=>new{x.InstitutionId,x.StudentId,x.ClientRequestId}).IsUnique();
        request.ToTable(t=>t.HasCheckConstraint("CK_academic_request_status","\"Status\" IN ('Pending','UnderReview','Approved','Rejected','Cancelled','Completed')"));
        b.Entity<AcademicRequestHistory>().ToTable(t=>t.HasCheckConstraint("CK_request_history_to","\"ToStatus\" IN ('UnderReview','Approved','Rejected','Cancelled','Completed')"));
    }
}
