using Academix.Api.Middleware;
using Academix.Api.Services.Communications;

namespace Academix.Tests.Unit;

public sealed class CommunicationPolicyTests
{
    [Fact]
    public void ParticipantOrderIsStableRegardlessOfRequestDirection()
    {
        var a = Guid.NewGuid(); var b = Guid.NewGuid();
        Assert.Equal(CommunicationPolicy.OrderedMembers(a, b), CommunicationPolicy.OrderedMembers(b, a));
        Assert.Throws<ApiFault>(() => CommunicationPolicy.OrderedMembers(a, a));
        Assert.Throws<ApiFault>(() => CommunicationPolicy.OrderedMembers(a, Guid.Empty));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public void BlankMessagesAreRejected(string? value)
        => Assert.Throws<ApiFault>(() => CommunicationPolicy.Text(value, 4000, "message"));

    [Fact]
    public void TextTrimmedAndLengthBounded()
    {
        Assert.Equal("hello", CommunicationPolicy.Text("  hello  ", 10, "message"));
        Assert.Throws<ApiFault>(() => CommunicationPolicy.Text(new string('x', 4001), 4000, "message"));
    }

    [Theory]
    [InlineData(-1, 10)]
    [InlineData(0, 0)]
    [InlineData(0, 101)]
    public void PaginationBoundsRejectInvalidRequests(int offset, int limit)
        => Assert.Throws<ApiFault>(() => CommunicationPolicy.Page(offset, limit));

    [Fact]
    public void AnnouncementAudienceAndUtcDatesAreValidated()
    {
        CommunicationPolicy.Schedule("Students", DateTime.UtcNow.AddMinutes(5));
        Assert.Throws<ApiFault>(() => CommunicationPolicy.Schedule("Public", DateTime.UtcNow.AddMinutes(5)));
        Assert.Throws<ApiFault>(() => CommunicationPolicy.Schedule("All", DateTime.Now.AddMinutes(5)));
        Assert.Throws<ApiFault>(() => CommunicationPolicy.Schedule("All", DateTime.UtcNow.AddDays(500)));
    }
}
