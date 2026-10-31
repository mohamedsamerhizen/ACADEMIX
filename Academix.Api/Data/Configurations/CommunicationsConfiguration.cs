using Academix.Api.Domain.Academic;
using Academix.Api.Domain.Communications;
using Academix.Api.Domain.Identity;
using Microsoft.EntityFrameworkCore;

namespace Academix.Api.Data;

public static class CommunicationsConfiguration
{
    public static void Apply(ModelBuilder b)
    {
        b.Entity<SectionMessageThread>(e =>
        {
            e.ToTable("communication_threads"); e.HasKey(x => x.Id);
            e.HasAlternateKey(x => new { x.Id, x.InstitutionId });
            e.HasIndex(x => new { x.SectionId, x.MemberLowId, x.MemberHighId }).IsUnique();
            e.HasOne<CourseSection>().WithMany().HasForeignKey(x => new { x.SectionId, x.InstitutionId })
                .HasPrincipalKey(x => new { x.Id, x.InstitutionId }).OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_p10_thread_section");
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.MemberLowId)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_p10_thread_low");
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.MemberHighId)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_p10_thread_high");
        });
        b.Entity<SectionDirectMessage>(e =>
        {
            e.ToTable("communication_messages"); e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.ThreadId, x.SenderId, x.ClientMessageId }).IsUnique();
            e.HasIndex(x => new { x.ThreadId, x.CreatedAtUtc });
            e.Property(x => x.Body).HasMaxLength(4000).IsRequired();
            e.HasOne<SectionMessageThread>().WithMany().HasForeignKey(x => new { x.ThreadId, x.InstitutionId })
                .HasPrincipalKey(x => new { x.Id, x.InstitutionId }).OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_p10_message_thread");
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.SenderId)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_p10_message_sender");
        });
        b.Entity<InAppNotification>(e =>
        {
            e.ToTable("communication_notifications"); e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.RecipientId, x.Kind, x.SourceId }).IsUnique();
            e.HasIndex(x => new { x.RecipientId, x.CreatedAtUtc });
            e.Property(x => x.Kind).HasMaxLength(32).IsRequired();
            e.Property(x => x.Title).HasMaxLength(160).IsRequired();
            e.Property(x => x.Body).HasMaxLength(4000).IsRequired();
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.RecipientId)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_p10_notification_recipient");
        });
        b.Entity<CommunicationPreference>(e =>
        {
            e.ToTable("communication_preferences"); e.HasKey(x => x.UserId);
            e.Property(x => x.UserId).ValueGeneratedNever();
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_p10_preference_user");
        });
        b.Entity<ScheduledSectionAnnouncement>(e =>
        {
            e.ToTable("communication_announcements"); e.HasKey(x => x.Id);
            e.HasIndex(x => new { x.SectionId, x.ScheduledForUtc });
            e.Property(x => x.Title).HasMaxLength(160).IsRequired();
            e.Property(x => x.Body).HasMaxLength(4000).IsRequired();
            e.Property(x => x.Audience).HasMaxLength(16).IsRequired();
            e.Property(x => x.Version).IsConcurrencyToken();
            e.HasOne<CourseSection>().WithMany().HasForeignKey(x => new { x.SectionId, x.InstitutionId })
                .HasPrincipalKey(x => new { x.Id, x.InstitutionId }).OnDelete(DeleteBehavior.Restrict)
                .HasConstraintName("FK_p10_announcement_section");
            e.HasOne<ApplicationUser>().WithMany().HasForeignKey(x => x.AuthorId)
                .OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_p10_announcement_author");
        });
    }
}
