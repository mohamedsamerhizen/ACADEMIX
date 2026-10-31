using System.Security.Claims;
using Academix.Api.Data;
using Academix.Api.Domain.AdvancedLearning;
using Academix.Api.DTOs.AdvancedLearning;
using Academix.Api.Middleware;
using Microsoft.EntityFrameworkCore;

namespace Academix.Api.Services.AdvancedLearning;

public sealed class DiscussionService(AcademixDbContext db, AdvancedLearningAccess access)
{
    private static DiscussionTopicView Map(DiscussionTopic x) => new(x.Id, x.SectionId, x.CreatedBy,
        x.Title, x.Body, x.IsLocked, x.IsPinned, x.Version, x.CreatedAtUtc);
    private static DiscussionPostView Map(DiscussionPost x) => new(x.Id, x.TopicId, x.ParentId,
        x.AuthorId, x.Body, x.IsHidden, x.CreatedAtUtc);

    public async Task<DiscussionTopicView> Create(ClaimsPrincipal principal, Guid sectionId,
        DiscussionTopicCreate request, CancellationToken ct)
    {
        var section = await access.Section(sectionId, ct);
        var (actor, _) = await access.Viewer(principal, section, ct);
        var item = new DiscussionTopic
        {
            InstitutionId = section.InstitutionId, SectionId = sectionId, CreatedBy = actor,
            Title = AdvancedLearningAccess.Text(request.Title, 200, "discussion title"),
            Body = AdvancedLearningAccess.Text(request.Body, 10000, "discussion body")
        };
        db.DiscussionTopics.Add(item);
        await db.SaveChangesAsync(ct);
        return Map(item);
    }

    public async Task<IReadOnlyList<DiscussionTopicView>> List(ClaimsPrincipal principal, Guid sectionId,
        int offset, int limit, CancellationToken ct)
    {
        var section = await access.Section(sectionId, ct);
        await access.Viewer(principal, section, ct);
        if (offset < 0 || limit is < 1 or > 100) throw ApiFault.Bad("Invalid pagination.");
        return (await db.DiscussionTopics.AsNoTracking().Where(x => x.SectionId == sectionId)
            .OrderByDescending(x => x.IsPinned).ThenByDescending(x => x.CreatedAtUtc)
            .ThenBy(x => x.Id).Skip(offset).Take(limit).ToListAsync(ct)).Select(Map).ToArray();
    }

    public async Task<DiscussionPage> Read(ClaimsPrincipal principal, Guid id, int offset, int limit, CancellationToken ct)
    {
        var topic = await db.DiscussionTopics.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw ApiFault.NotFound();
        var section = await access.Section(topic.SectionId, ct);
        var (_, staff) = await access.Viewer(principal, section, ct);
        if (offset < 0 || limit is < 1 or > 100) throw ApiFault.Bad("Invalid pagination.");
        var posts = await db.DiscussionPosts.AsNoTracking().Where(x => x.TopicId == id && (staff || !x.IsHidden))
            .OrderBy(x => x.CreatedAtUtc).ThenBy(x => x.Id).Skip(offset).Take(limit).ToListAsync(ct);
        return new DiscussionPage(Map(topic), posts.Select(Map).ToArray());
    }

    public async Task<DiscussionPostView> Reply(ClaimsPrincipal principal, Guid id,
        DiscussionReplyCreate request, CancellationToken ct)
    {
        var topic = await db.DiscussionTopics.AsNoTracking().SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw ApiFault.NotFound();
        var section = await access.Section(topic.SectionId, ct);
        var (actor, staff) = await access.Viewer(principal, section, ct);
        if (topic.IsLocked && !staff) throw ApiFault.Conflict("Discussion is locked.");
        if (request.ParentId is Guid parent &&
            !await db.DiscussionPosts.AnyAsync(x => x.Id == parent && x.TopicId == id && !x.IsHidden, ct))
            throw ApiFault.Bad("Parent reply does not belong to this discussion or is hidden.");
        var reply = new DiscussionPost
        {
            InstitutionId = topic.InstitutionId, SectionId = topic.SectionId, TopicId = id,
            AuthorId = actor, ParentId = request.ParentId,
            Body = AdvancedLearningAccess.Text(request.Body, 10000, "reply")
        };
        db.DiscussionPosts.Add(reply);
        await db.SaveChangesAsync(ct);
        return Map(reply);
    }

    public async Task<DiscussionTopicView> UpdateState(ClaimsPrincipal principal, Guid id,
        DiscussionStateUpdate request, CancellationToken ct)
    {
        var topic = await db.DiscussionTopics.SingleOrDefaultAsync(x => x.Id == id, ct)
            ?? throw ApiFault.NotFound();
        await access.Staff(principal, await access.Section(topic.SectionId, ct), ct);
        if (topic.Version != request.ExpectedVersion) throw ApiFault.Conflict("Discussion version changed.");
        topic.IsLocked = request.IsLocked; topic.IsPinned = request.IsPinned; topic.Version++;
        try { await db.SaveChangesAsync(ct); }
        catch (DbUpdateConcurrencyException) { throw ApiFault.Conflict("Discussion changed concurrently."); }
        return Map(topic);
    }

    public async Task<DiscussionPostView> Moderate(ClaimsPrincipal principal, Guid postId, bool hidden, CancellationToken ct)
    {
        var post = await db.DiscussionPosts.SingleOrDefaultAsync(x => x.Id == postId, ct)
            ?? throw ApiFault.NotFound();
        await access.Staff(principal, await access.Section(post.SectionId, ct), ct);
        post.IsHidden = hidden;
        await db.SaveChangesAsync(ct);
        return Map(post);
    }
}
