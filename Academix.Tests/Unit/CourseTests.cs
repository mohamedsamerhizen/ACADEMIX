using Academix.Api.Domain.Courses;

namespace Academix.Tests.Unit;

public sealed class CourseTests
{
    [Fact]
    public void Create_NormalizesCodeAndTitle()
    {
        var course = Course.Create(Guid.NewGuid(), " cs101 ", "  Intro to CS  ");
        Assert.Equal("CS101", course.Code);
        Assert.Equal("Intro to CS", course.Title);
        Assert.True(course.IsActive);
    }

    [Fact]
    public void Create_RejectsEmptyId() =>
        Assert.Throws<ArgumentException>(() => Course.Create(Guid.Empty, "CS101", "Intro"));

    [Theory]
    [InlineData("CS")]
    [InlineData("1CS101")]
    [InlineData("")]
    public void Create_RejectsInvalidCodes(string code) =>
        Assert.Throws<ArgumentException>(() => Course.Create(Guid.NewGuid(), code, "Intro"));

    [Fact]
    public void Create_RejectsWhitespaceTitle() =>
        Assert.Throws<ArgumentException>(() => Course.Create(Guid.NewGuid(), "CS101", "  "));
}
