using System.Text;
using Academix.Api.Middleware;
using Academix.Api.Services.Learning;

namespace Academix.Tests.Unit;

public sealed class LearningFileContentPolicyTests
{
    [Theory]
    [InlineData("application/pdf", "%PDF-1.7\n1 0 obj\n")]
    [InlineData("text/plain", "Text in valid UTF-8: hello\r\n")]
    public async Task AcceptsRecognizedSimpleContent(string mime, string body)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(body));
        await LearningFileContentPolicy.ValidateAsync(stream, mime);
    }

    [Fact]
    public async Task AcceptsPngSignature()
    {
        using var stream = new MemoryStream([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00]);
        await LearningFileContentPolicy.ValidateAsync(stream, "image/png");
    }

    [Fact]
    public async Task AcceptsJpegSignature()
    {
        using var stream = new MemoryStream([0xFF, 0xD8, 0xFF, 0xE0, 0x00]);
        await LearningFileContentPolicy.ValidateAsync(stream, "image/jpeg");
    }

    [Theory]
    [InlineData("application/pdf")]
    [InlineData("image/png")]
    [InlineData("image/jpeg")]
    public async Task RejectsSpoofedBinaryMime(string mime)
    {
        using var stream = new MemoryStream("this is not an image or PDF"u8.ToArray());
        var error = await Assert.ThrowsAsync<ApiFault>(() => LearningFileContentPolicy.ValidateAsync(stream, mime));
        Assert.Equal(400, error.StatusCode);
    }

    [Fact]
    public async Task RejectsTruncatedPng()
    {
        using var stream = new MemoryStream([0x89, 0x50, 0x4E]);
        await Assert.ThrowsAsync<ApiFault>(() => LearningFileContentPolicy.ValidateAsync(stream, "image/png"));
    }

    [Fact]
    public async Task RejectsInvalidUtf8Text()
    {
        using var stream = new MemoryStream([0xC3, 0x28]);
        await Assert.ThrowsAsync<ApiFault>(() => LearningFileContentPolicy.ValidateAsync(stream, "text/plain"));
    }

    [Fact]
    public async Task RejectsBinaryControlCharactersInText()
    {
        using var stream = new MemoryStream([0x41, 0x00, 0x42]);
        await Assert.ThrowsAsync<ApiFault>(() => LearningFileContentPolicy.ValidateAsync(stream, "text/plain"));
    }

    [Fact]
    public async Task RejectsUnsupportedMediaType()
    {
        using var stream = new MemoryStream("{}"u8.ToArray());
        await Assert.ThrowsAsync<ApiFault>(() => LearningFileContentPolicy.ValidateAsync(stream, "application/json"));
    }
}
