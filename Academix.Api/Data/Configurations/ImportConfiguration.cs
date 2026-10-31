using Academix.Api.Domain.Academic;
using Academix.Api.Domain.Registrar;
using Academix.Api.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Academix.Api.Data;

public static class ImportConfiguration
{
    public static void Apply(ModelBuilder b)
    {
        var j = b.Entity<ImportJob>(); j.ToTable("institutional_import_jobs"); j.HasKey(x => x.Id); j.HasAlternateKey(x => new { x.Id, x.InstitutionId });
        j.HasOne<Institution>().WithMany().HasForeignKey(x => x.InstitutionId).OnDelete(DeleteBehavior.Restrict);
        j.HasIndex(x => new { x.InstitutionId, x.ClientRequestId }).IsUnique();
        j.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.CreatedBy).OnDelete(DeleteBehavior.Restrict);
        j.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.ApprovedBy).OnDelete(DeleteBehavior.Restrict);
        j.Property(x => x.SourceSystem).HasMaxLength(80); j.Property(x => x.Digest).HasMaxLength(64); j.Property(x => x.Status).HasMaxLength(20);
        j.ToTable(t => t.HasCheckConstraint("CK_import_job_state", "\"Status\" IN ('Draft','Approved') AND (\"Status\" <> 'Approved' OR (\"ApprovedBy\" IS NOT NULL AND \"ApprovedAtUtc\" IS NOT NULL AND \"ApprovedBy\" <> \"CreatedBy\"))"));
        var r = b.Entity<ImportRow>(); r.ToTable("institutional_import_rows"); r.HasKey(x => x.Id); r.HasAlternateKey(x => new { x.Id, x.InstitutionId });
        r.HasOne<ImportJob>().WithMany().HasForeignKey(x => new { x.JobId, x.InstitutionId }).HasPrincipalKey(x => new { x.Id, x.InstitutionId }).OnDelete(DeleteBehavior.Restrict);
        r.HasIndex(x => new { x.JobId, x.Number }).IsUnique(); r.Property(x => x.PayloadJson).HasColumnType("jsonb"); r.Property(x => x.Issue).HasMaxLength(500); r.Property(x => x.Status).HasMaxLength(20);
        r.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
        r.ToTable(t => t.HasCheckConstraint("CK_import_row_state", "\"Status\" IN ('Pending','Valid','Invalid','Applied','Failed','AwaitingActivation') AND \"Number\" BETWEEN 1 AND 1000"));
        var m = b.Entity<ExternalIdentityMap>(); m.ToTable("institutional_external_identities"); m.HasKey(x => x.Id);
        m.Property(x => x.SourceSystem).HasMaxLength(80); m.Property(x => x.ExternalId).HasMaxLength(100);
        m.HasIndex(x => new { x.InstitutionId, x.SourceSystem, x.ExternalId }).IsUnique();
        m.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.StudentId).OnDelete(DeleteBehavior.Restrict);
        m.HasOne<ImportRow>().WithMany().HasForeignKey(x => new { x.ImportRowId, x.InstitutionId }).HasPrincipalKey(x => new { x.Id, x.InstitutionId }).OnDelete(DeleteBehavior.Restrict);
        var o = b.Entity<InstitutionalOutboxEvent>(); o.ToTable("institutional_outbox_events"); o.HasKey(x => x.Id);
        o.Property(x => x.PayloadJson).HasColumnType("jsonb"); o.Property(x => x.Kind).HasMaxLength(50); o.HasIndex(x => x.ImportRowId).IsUnique();
        o.HasOne<ImportRow>().WithMany().HasForeignKey(x => new { x.ImportRowId, x.InstitutionId }).HasPrincipalKey(x => new { x.Id, x.InstitutionId }).OnDelete(DeleteBehavior.Restrict);
    }
}
