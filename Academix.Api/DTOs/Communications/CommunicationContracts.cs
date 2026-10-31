namespace Academix.Api.DTOs.Communications;

public sealed record StartSectionThread(Guid RecipientId);
public sealed record SectionThreadView(Guid Id, Guid SectionId, Guid OtherMemberId, DateTime CreatedAtUtc, DateTime? LastReadAtUtc);
public sealed record SendSectionMessage(Guid ClientMessageId, string Body);
public sealed record SectionMessageView(Guid Id, Guid ThreadId, Guid SenderId, string Body, DateTime CreatedAtUtc);
public sealed record InboxNotificationView(Guid Id, string Kind, Guid SourceId, string Title, string Body, DateTime CreatedAtUtc, DateTime? ReadAtUtc);
public sealed record NotificationInbox(IReadOnlyList<InboxNotificationView> Items, int UnreadCount);
public sealed record UpdateCommunicationPreference(bool MessageNotifications, bool AnnouncementNotifications);
public sealed record CommunicationPreferenceView(bool MessageNotifications, bool AnnouncementNotifications);
public sealed record ScheduleSectionAnnouncement(string Title, string Body, string Audience, DateTime ScheduledForUtc);
public sealed record ReviseSectionAnnouncement(string Title, string Body, string Audience, DateTime ScheduledForUtc, long ExpectedVersion);
public sealed record SectionAnnouncementView(Guid Id, Guid SectionId, Guid AuthorId, string Title, string Body, string Audience,
    DateTime ScheduledForUtc, DateTime? DispatchedAtUtc, bool IsCancelled, long Version);
public sealed record AnnouncementDispatchResult(Guid AnnouncementId, int NotificationsCreated, DateTime DispatchedAtUtc);
