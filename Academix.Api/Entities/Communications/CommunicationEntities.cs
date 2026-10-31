namespace Academix.Api.Domain.Communications;

public sealed class SectionMessageThread
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public Guid SectionId { get; set; }
    public Guid MemberLowId { get; set; }
    public Guid MemberHighId { get; set; }
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? LastReadLowAtUtc { get; set; }
    public DateTime? LastReadHighAtUtc { get; set; }
}

public sealed class SectionDirectMessage
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public Guid ThreadId { get; set; }
    public Guid SenderId { get; set; }
    public Guid ClientMessageId { get; set; }
    public string Body { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public sealed class InAppNotification
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public Guid RecipientId { get; set; }
    public string Kind { get; set; } = "";
    public Guid SourceId { get; set; }
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAtUtc { get; set; }
}

public sealed class CommunicationPreference
{
    public Guid UserId { get; set; }
    public Guid InstitutionId { get; set; }
    public bool MessageNotifications { get; set; } = true;
    public bool AnnouncementNotifications { get; set; } = true;
}

public sealed class ScheduledSectionAnnouncement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid InstitutionId { get; set; }
    public Guid SectionId { get; set; }
    public Guid AuthorId { get; set; }
    public string Title { get; set; } = "";
    public string Body { get; set; } = "";
    public string Audience { get; set; } = "All";
    public DateTime ScheduledForUtc { get; set; }
    public DateTime? DispatchedAtUtc { get; set; }
    public bool IsCancelled { get; set; }
    public long Version { get; set; } = 1;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}
