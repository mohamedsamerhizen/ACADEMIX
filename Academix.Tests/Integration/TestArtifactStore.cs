using System.Collections.Concurrent;
using Academix.Api.Storage;
namespace Academix.Tests.Integration;
// Only the external object store is substituted. These tests still use real PostgreSQL,
// real Identity, real transactions, migrations, policies and the real PDF renderer.
public sealed class TestArtifactStore:IPrivateFileStore
{
    private readonly ConcurrentDictionary<string,byte[]> objects=new();
    public int FailNextPut;
    public string? FailRemoveKey;
    public bool Contains(string key) => objects.ContainsKey(key);
    public Task PutAsync(string key,Stream content,long length,string type,CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();if(Interlocked.Exchange(ref FailNextPut,0)!=0)throw new IOException("Injected object store failure");
        using var buffer=new MemoryStream();content.CopyTo(buffer);objects[key]=buffer.ToArray();return Task.CompletedTask;
    }
    public Task<byte[]> GetAsync(string key,CancellationToken ct){ct.ThrowIfCancellationRequested();return Task.FromResult(objects[key].ToArray());}
    public Task RemoveAsync(string key,CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if(FailRemoveKey==key)throw new IOException("Injected cleanup failure");
        objects.TryRemove(key,out _);return Task.CompletedTask;
    }
    public void Corrupt(string key){var copy=objects[key].ToArray();copy[^1]^=1;objects[key]=copy;}
}
