using Academix.Api.Domain.Identity;
using Academix.Api.Domain.Academic;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Academix.Api.Data;

public sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> b)
    {
        b.ToTable("identity_users");
        b.HasIndex(x => x.NormalizedEmail).IsUnique();
        b.Property(x => x.IsActive).HasDefaultValue(true);
        b.HasOne<Institution>().WithMany().HasForeignKey(x => x.InstitutionId).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class ApplicationRoleConfiguration : IEntityTypeConfiguration<ApplicationRole>
{
    public void Configure(EntityTypeBuilder<ApplicationRole> b) => b.ToTable("identity_roles");
}
public sealed class RefreshSessionConfiguration : IEntityTypeConfiguration<RefreshSession>
{
    public void Configure(EntityTypeBuilder<RefreshSession> b)
    {
        b.ToTable("identity_refresh_sessions"); b.HasKey(x => x.Id);
        b.HasIndex(x => x.TokenHash).IsUnique(); b.HasIndex(x => new { x.UserId, x.FamilyId });
        b.Property(x => x.TokenHash).HasMaxLength(64).IsRequired();
        b.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
    }
}
public sealed class SecurityAuditConfiguration : IEntityTypeConfiguration<SecurityAuditEvent>
{
    public void Configure(EntityTypeBuilder<SecurityAuditEvent> b)
    {
        b.ToTable("security_audit_events"); b.HasKey(x => x.Id);
        b.Property(x => x.Action).HasMaxLength(80).IsRequired();
        b.Property(x => x.Target).HasMaxLength(150).IsRequired();
        b.HasIndex(x => x.AtUtc);
    }
}
