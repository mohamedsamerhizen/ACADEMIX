using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Globalization;
using Academix.Api.Data;
using Academix.Api.Domain.Institutional;
using Academix.Api.Domain.Registrar;
using Academix.Api.Middleware;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;

namespace Academix.Api.Services.Institutional;

public static class DocumentIntegrity
{
    public static string Digest(byte[] bytes)=>Convert.ToHexString(SHA256.HashData(bytes));
    public static string Sign(byte[] key,Guid id,string digest,string snapshot)=>Convert.ToHexString(HMACSHA256.HashData(key,
        Encoding.UTF8.GetBytes("ACADEMIX-DOCUMENT-V2:"+id.ToString("N")+":"+digest+":"+Digest(CanonicalSnapshot(snapshot)))));
    private static byte[] CanonicalSnapshot(string snapshot)
    {
        using var document=JsonDocument.Parse(snapshot);using var buffer=new MemoryStream();
        using(var writer=new Utf8JsonWriter(buffer))Write(document.RootElement,writer);
        return buffer.ToArray();
    }
    private static void Write(JsonElement node,Utf8JsonWriter writer)
    {
        switch(node.ValueKind)
        {
            case JsonValueKind.Object:
                writer.WriteStartObject();foreach(var property in node.EnumerateObject().OrderBy(x=>x.Name,StringComparer.Ordinal))
                {writer.WritePropertyName(property.Name);Write(property.Value,writer);}writer.WriteEndObject();break;
            case JsonValueKind.Array:
                writer.WriteStartArray();foreach(var item in node.EnumerateArray())Write(item,writer);writer.WriteEndArray();break;
            case JsonValueKind.Number:
                if(node.TryGetDecimal(out var number))writer.WriteRawValue(number.ToString("G29",CultureInfo.InvariantCulture));
                else writer.WriteNumberValue(node.GetDouble());break;
            default:node.WriteTo(writer);break;
        }
    }
    public static bool Equal(string? first,string? second)=>first is {Length:64}&&second is {Length:64}&&first.All(Uri.IsHexDigit)&&second.All(Uri.IsHexDigit)&&
        CryptographicOperations.FixedTimeEquals(Convert.FromHexString(first),Convert.FromHexString(second));
}
public sealed class DocumentKeyService(AcademixDbContext db,IDataProtectionProvider protection)
{
    private IDataProtector Protector=>protection.CreateProtector("ACADEMIX.DocumentVerificationKeys.v2");
    private byte[] Secret(DocumentVerificationKey key)
    {
        try { return Convert.FromBase64String(Protector.Unprotect(key.ProtectedSecret)); }
        catch(Exception ex) when(ex is CryptographicException or FormatException)
        { throw ApiFault.Conflict("Document verification key cannot be decrypted with the configured keyring."); }
    }
    public async Task<DocumentVerificationKey> Active(Guid inst,CancellationToken ct)
    {
        var key=await db.Set<DocumentVerificationKey>().SingleOrDefaultAsync(x=>x.InstitutionId==inst&&x.RetiredAtUtc==null&&x.RevokedAtUtc==null,ct);
        if(key is not null)return key;
        key=new(){InstitutionId=inst,ProtectedSecret=Protector.Protect(Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)))};
        db.Set<DocumentVerificationKey>().Add(key);return key;
    }
    public string Sign(DocumentVerificationKey key,IssuedAcademicDocument document)=>DocumentIntegrity.Sign(
        Secret(key),document.Id,"Proof:"+document.Kind+":"+document.TemplateId,document.SnapshotJson);
    public string SignArtifact(DocumentVerificationKey key,IssuedAcademicDocument document)=>DocumentIntegrity.Sign(
        Secret(key),document.Id,"Artifact:"+document.Kind+":"+document.TemplateId+":"+document.ArtifactDigest,document.SnapshotJson);
    public async Task<bool> ArtifactSignatureValid(IssuedAcademicDocument document,CancellationToken ct)
    {
        if(document.VerificationKeyId is not Guid id)return document.ArtifactKey is null;
        var key=await db.Set<DocumentVerificationKey>().AsNoTracking().SingleOrDefaultAsync(x=>x.Id==id&&x.InstitutionId==document.InstitutionId,ct)
            ??throw ApiFault.Conflict("Document verification key is unavailable.");
        return DocumentIntegrity.Equal(document.ArtifactSignature,SignArtifact(key,document));
    }
    public async Task<string> Expected(IssuedAcademicDocument document,CancellationToken ct)
    {
        if(document.VerificationKeyId is not Guid id)return document.LegacyProof??throw ApiFault.Conflict("Legacy verification proofs must be materialized before rotating the legacy signing key.");
        var key=await db.Set<DocumentVerificationKey>().AsNoTracking().SingleOrDefaultAsync(x=>x.Id==id&&x.InstitutionId==document.InstitutionId,ct)
            ??throw ApiFault.Conflict("Document verification key is unavailable.");
        return Sign(key,document);
    }
}

// Compatibility bridge: capture historical proofs once, before HTTP begins serving.
// New issuance never reads or uses the access-token signing key. Historical proofs
// require the explicitly supplied original legacy key once; guessing it is unsafe.
public sealed class LegacyDocumentProofInitializer(IServiceScopeFactory scopes,IConfiguration config):IHostedService
{
    public async Task StartAsync(CancellationToken ct)
    {
        await using var scope=scopes.CreateAsyncScope();var db=scope.ServiceProvider.GetRequiredService<AcademixDbContext>();
        var ids=await db.IssuedAcademicDocuments.AsNoTracking().Where(x=>x.VerificationKeyId==null&&x.LegacyProof==null).Select(x=>x.Id).ToListAsync(ct);
        if(ids.Count==0)return;
        var key=config["Documents:LegacyVerificationKey"];
        if(string.IsNullOrWhiteSpace(key)||Encoding.UTF8.GetByteCount(key)<32)throw new InvalidOperationException("Documents:LegacyVerificationKey must contain the original historical document key for one-time proof capture.");
        foreach(var id in ids)
        {
            var proof=Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(key),Encoding.UTF8.GetBytes("ACADEMIX-DOCUMENT-V1:"+id.ToString("N"))));
            await db.IssuedAcademicDocuments.Where(x=>x.Id==id&&x.LegacyProof==null&&x.VerificationKeyId==null).ExecuteUpdateAsync(s=>s.SetProperty(x=>x.LegacyProof,proof),ct);
        }
    }
    public Task StopAsync(CancellationToken ct)=>Task.CompletedTask;
}
