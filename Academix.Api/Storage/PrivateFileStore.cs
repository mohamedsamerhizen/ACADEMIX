using Minio;
using Minio.DataModel.Args;

namespace Academix.Api.Storage;

// All objects are stored in a private bucket. No public URLs or presigned URLs are returned.
public interface IPrivateFileStore
{
    Task PutAsync(string key, Stream content, long length, string contentType, CancellationToken ct);
    Task<byte[]> GetAsync(string key, CancellationToken ct);
    Task RemoveAsync(string key, CancellationToken ct);
}
public sealed class PrivateFileStore(IConfiguration configuration) : IPrivateFileStore
{
    private readonly SemaphoreSlim _bucketLock = new(1, 1);
    private bool _initialized;
    private readonly string _bucket = configuration["Storage:Bucket"] ?? "academix-private";
    private readonly IMinioClient _client = new MinioClient()
        .WithEndpoint(configuration["Storage:Endpoint"] ?? "localhost:9000")
        .WithCredentials(configuration["Storage:AccessKey"] ?? "development-only",
                         configuration["Storage:SecretKey"] ?? "development-only")
        .WithSSL(configuration.GetValue<bool>("Storage:UseSsl"))
        .Build();

    private async Task EnsureBucket(CancellationToken ct)
    {
        if (_initialized) return;
        await _bucketLock.WaitAsync(ct);
        try
        {
            if (_initialized) return;
            var args = new BucketExistsArgs().WithBucket(_bucket);
            if (!await _client.BucketExistsAsync(args, ct))
                await _client.MakeBucketAsync(new MakeBucketArgs().WithBucket(_bucket), ct);
            _initialized = true;
        }
        finally { _bucketLock.Release(); }
    }

    public async Task PutAsync(string key, Stream content, long length, string contentType, CancellationToken ct)
    {
        await EnsureBucket(ct);
        await _client.PutObjectAsync(new PutObjectArgs().WithBucket(_bucket).WithObject(key)
            .WithStreamData(content).WithObjectSize(length).WithContentType(contentType), ct);
    }

    public async Task<byte[]> GetAsync(string key, CancellationToken ct)
    {
        await EnsureBucket(ct);
        using var destination = new MemoryStream();
        await _client.GetObjectAsync(new GetObjectArgs().WithBucket(_bucket).WithObject(key)
            .WithCallbackStream(source => source.CopyTo(destination)), cancellationToken: ct);
        return destination.ToArray();
    }

    public async Task RemoveAsync(string key, CancellationToken ct)
    {
        await EnsureBucket(ct);
        await _client.RemoveObjectAsync(new RemoveObjectArgs().WithBucket(_bucket).WithObject(key), ct);
    }
}
