using System.Text;
using Academix.Api.Middleware;

namespace Academix.Api.Services.Learning;

/// <summary>
/// Performs bounded format checks on untrusted LMS upload streams.
/// These checks do not assert that a document or image is fully well-formed,
/// and do not replace malware scanning or external-file processing safeguards.
/// </summary>
public static class LearningFileContentPolicy
{
    private static readonly byte[] Pdf = "%PDF-"u8.ToArray();
    private static readonly byte[] Png = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];
    private static readonly byte[] Jpeg = [0xFF, 0xD8, 0xFF];

    public static async Task ValidateAsync(Stream stream, string contentType, CancellationToken ct = default)
    {
        if (stream is null || !stream.CanRead) throw ApiFault.Bad("File cannot be read.");
        switch (contentType)
        {
            case "application/pdf":
                await RequireSignatureAsync(stream, Pdf, ct);
                break;
            case "image/png":
                await RequireSignatureAsync(stream, Png, ct);
                break;
            case "image/jpeg":
                await RequireSignatureAsync(stream, Jpeg, ct);
                break;
            case "text/plain":
                await RequireUtf8TextAsync(stream, ct);
                break;
            default:
                throw ApiFault.Bad("File content type is not allowed.");
        }
    }

    private static async Task RequireSignatureAsync(Stream stream, byte[] expected, CancellationToken ct)
    {
        var prefix = new byte[expected.Length];
        var length = 0;
        while (length < prefix.Length)
        {
            var read = await stream.ReadAsync(prefix.AsMemory(length), ct);
            if (read == 0) break;
            length += read;
        }
        if (length != expected.Length || !prefix.AsSpan().SequenceEqual(expected))
            throw ApiFault.Bad("File contents do not match the declared content type.");
    }

    private static async Task RequireUtf8TextAsync(Stream stream, CancellationToken ct)
    {
        // Strict UTF-8 decoding rejects malformed encoding; NUL/control characters
        // typical of binary payloads are rejected as well. Memory remains bounded.
        using var reader = new StreamReader(stream, new UTF8Encoding(false, true),
            detectEncodingFromByteOrderMarks: false, bufferSize: 8192, leaveOpen: true);
        var buffer = new char[4096];
        try
        {
            while (true)
            {
                var read = await reader.ReadAsync(buffer.AsMemory(), ct);
                if (read == 0) break;
                for (var i = 0; i < read; i++)
                {
                    var c = buffer[i];
                    if (char.IsControl(c) && c is not ('\r' or '\n' or '\t'))
                        throw ApiFault.Bad("Text file contains invalid control characters.");
                }
            }
        }
        catch (DecoderFallbackException)
        {
            throw ApiFault.Bad("Text file must contain valid UTF-8.");
        }
    }
}
