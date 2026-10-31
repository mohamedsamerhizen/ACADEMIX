using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Academix.Api.Data;
using Academix.Api.Domain.Academic;
using Academix.Api.Domain.Courses;
using Academix.Api.Domain.Identity;
using Academix.Api.Middleware;
using Academix.Api.Services.Infrastructure;
using Academix.Api.Services.Learning;
using Academix.Api.Storage;
using DotNet.Testcontainers.Builders;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Npgsql;
using Xunit.Abstractions;

namespace Academix.Tests.Integration;

public sealed class RealMinioIntegrationTests(ITestOutputHelper output)
{
    // Every successful operation reaches the real provider. Bound outage waits without replacing storage.
    private sealed class BoundedStore(PrivateFileStore store) : IPrivateFileStore
    {
        private static async Task<T> Bound<T>(Func<CancellationToken, Task<T>> action, CancellationToken ct)
        {
            using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ct);
            deadline.CancelAfter(TimeSpan.FromSeconds(5));
            try { return await action(deadline.Token); }
            catch (OperationCanceledException ex) when (!ct.IsCancellationRequested) { throw new IOException("Real object store unavailable within the test deadline.", ex); }
        }
        public Task PutAsync(string key, Stream content, long length, string type, CancellationToken ct)
            => Bound(async token => { await store.PutAsync(key, content, length, type, token); return true; }, ct);
        public Task<byte[]> GetAsync(string key, CancellationToken ct) => Bound(token => store.GetAsync(key, token), ct);
        public Task RemoveAsync(string key, CancellationToken ct)
            => Bound(async token => { await store.RemoveAsync(key, token); return true; }, ct);
    }

    private static ClaimsPrincipal Principal(Guid user, Guid institution) => new(new ClaimsIdentity(
        [new(ClaimTypes.NameIdentifier, user.ToString()), new("institution_id", institution.ToString())], "test"));

    [Fact]
    public async Task RealProviderPreservesCommittedUploadsAndRecoversDurableCleanupAfterOutage()
    {
        var secret = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(24));
        // Explicitly retain the same disposable host port across the stop/start outage.
        var portReservation = new System.Net.Sockets.TcpListener(IPAddress.Loopback, 0);
        portReservation.Start();
        var hostPort = ((IPEndPoint)portReservation.LocalEndpoint).Port;
        portReservation.Stop();
        await using var minio = new ContainerBuilder("cgr.dev/chainguard/minio:latest")
            .WithEnvironment("MINIO_ROOT_USER", "isolated-verification")
            .WithEnvironment("MINIO_ROOT_PASSWORD", secret)
            .WithCommand("server", "/data", "--console-address", ":9001")
            .WithPortBinding(hostPort, 9000)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(request => request.ForPort(9000).ForPath("/minio/health/live")))
            .Build();
        await minio.StartAsync();
        var minioVersion = await minio.ExecAsync(new[] { "minio", "--version" });
        Assert.Equal(0, minioVersion.ExitCode); output.WriteLine(minioVersion.Stdout.Trim());
        var endpoint = $"{minio.Hostname}:{minio.GetMappedPublicPort(9000)}";
        var bucket = "verification-" + Guid.NewGuid().ToString("N");
        var fixture = new ApiFixture
        {
            StorageConfiguration = new Dictionary<string, string?>
            {
                ["Storage:Endpoint"] = endpoint, ["Storage:AccessKey"] = "isolated-verification",
                ["Storage:SecretKey"] = secret, ["Storage:Bucket"] = bucket, ["Storage:UseSsl"] = "false"
            },
            ConfigureTestServices = services =>
            {
                services.RemoveAll<IPrivateFileStore>();
                services.AddSingleton<IPrivateFileStore>(p => new BoundedStore(p.GetRequiredService<PrivateFileStore>()));
            }
        };
        try
        {
            await fixture.InitializeAsync();
            await using var scope = fixture.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AcademixDbContext>();
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var institution = new Institution { Code = "MINIO", Name = "Isolated real-storage verification" };
            db.Add(institution); await db.SaveChangesAsync();
            async Task<Guid> AddUser(string role)
            {
                var email = Guid.NewGuid().ToString("N") + "@example.test";
                var user = new ApplicationUser { InstitutionId = institution.Id, UserName = email, Email = email };
                Assert.True((await users.CreateAsync(user, "Safe!RealMinio2026")).Succeeded);
                Assert.True((await users.AddToRoleAsync(user, role)).Succeeded); return user.Id;
            }
            var adminId = await AddUser(AcademixRoles.InstitutionAdministrator);
            var ownerId = await AddUser(AcademixRoles.Student);
            var otherId = await AddUser(AcademixRoles.Student);
            var term = new AcademicTerm { InstitutionId = institution.Id, Code = "T", Name = "Term", Status = "Active", StartsOn = new(2026,1,1), EndsOn = new(2027,12,31) };
            var course = Course.Create(Guid.NewGuid(), institution.Id, "MI101", "Real private storage");
            var section = new CourseSection { InstitutionId = institution.Id, TermId = term.Id, CourseId = course.Id, Code = "A", Status = "Open", Capacity = 10 };
            db.AddRange(term, course, section);
            foreach (var student in new[] { ownerId, otherId }) db.StudentEnrollments.Add(new() { InstitutionId = institution.Id, SectionId = section.Id, StudentId = student });
            await db.SaveChangesAsync();
            var owner = Principal(ownerId, institution.Id); var other = Principal(otherId, institution.Id); var admin = Principal(adminId, institution.Id);
            var operations = scope.ServiceProvider.GetRequiredService<LearningOperations>();
            var files = scope.ServiceProvider.GetRequiredService<IPrivateFileStore>();
            var outbox = scope.ServiceProvider.GetRequiredService<OutboxService>();
            var bytes = Encoding.UTF8.GetBytes("Private evidence stored in actual MinIO");
            async Task Upload(string name)
            {
                using var content = new MemoryStream(bytes);
                await operations.Upload(owner, section.Id, new FormFile(content, 0, bytes.Length, "file", name)
                    { Headers = new HeaderDictionary(), ContentType = "text/plain" }, default);
            }
            await Upload("committed.txt");
            var saved = await db.LearningFiles.AsNoTracking().SingleAsync();
            Assert.Equal(bytes, (await operations.Download(owner, saved.Id, default)).Bytes);
            Assert.Equal(bytes, (await operations.Download(admin, saved.Id, default)).Bytes);
            Assert.Equal(403, (await Assert.ThrowsAsync<ApiFault>(() => operations.Download(other, saved.Id, default))).StatusCode);
            using (var anonymous = new HttpClient())
            using (var denied = await anonymous.GetAsync($"http://{endpoint}/{bucket}/{saved.ObjectKey}"))
                Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
            await outbox.ProcessBatch(default); Assert.Equal(bytes, await files.GetAsync(saved.ObjectKey, default));
            var function = "minio_fault_" + Guid.NewGuid().ToString("N");
            await using var sql = new NpgsqlConnection(db.Database.GetConnectionString()); await sql.OpenAsync();
            await using (var command = new NpgsqlCommand($"CREATE FUNCTION {function}() RETURNS trigger LANGUAGE plpgsql AS $$ BEGIN RAISE EXCEPTION 'post-upload verification fault' USING ERRCODE='23514'; END $$; CREATE TRIGGER {function} BEFORE INSERT ON lms_files FOR EACH ROW EXECUTE FUNCTION {function}();", sql))
                await command.ExecuteNonQueryAsync();
            try { await Assert.ThrowsAsync<DbUpdateException>(() => Upload("rolled-back.txt")); }
            finally
            {
                await using var command = new NpgsqlCommand($"DROP TRIGGER {function} ON lms_files; DROP FUNCTION {function}();", sql); await command.ExecuteNonQueryAsync();
            }
            Assert.Equal(1, await db.LearningFiles.CountAsync());
            var intent = await db.InstitutionalOutboxEvents.AsNoTracking().SingleAsync(x => x.Kind == "LearningArtifactCleanup" && x.Status == "Pending");
            var orphanId = JsonDocument.Parse(intent.PayloadJson).RootElement.GetProperty("FileId").GetGuid();
            var orphanKey = LearningArtifactCleanup.Key(institution.Id, section.Id, orphanId);
            Assert.Equal(bytes, await files.GetAsync(orphanKey, default));
            await minio.StopAsync();
            await outbox.ProcessBatch(default);
            intent = await db.InstitutionalOutboxEvents.AsNoTracking().SingleAsync(x => x.Id == intent.Id);
            Assert.Equal("Retry", intent.Status); Assert.True(intent.Attempts > 0);
            await minio.StartAsync();
            Assert.Equal(endpoint, $"{minio.Hostname}:{minio.GetMappedPublicPort(9000)}");
            Assert.Equal(bytes, await files.GetAsync(saved.ObjectKey, default));
            Assert.Equal(bytes, await files.GetAsync(orphanKey, default));
            await outbox.Control(admin, institution.Id, intent.Id, false, default); await outbox.ProcessBatch(default);
            Assert.Equal("Delivered", await db.InstitutionalOutboxEvents.Where(x => x.Id == intent.Id).Select(x => x.Status).SingleAsync());
            await Assert.ThrowsAnyAsync<Exception>(() => files.GetAsync(orphanKey, default));
            Assert.Equal(bytes, (await operations.Download(owner, saved.Id, default)).Bytes);
            await using var check = fixture.Services.CreateAsyncScope();
            Assert.Equal(saved.ObjectKey, await check.ServiceProvider.GetRequiredService<AcademixDbContext>().LearningFiles.Select(x => x.ObjectKey).SingleAsync());
            // Upload has no client idempotency token: repeated HTTP-equivalent calls are distinct artifacts.
            await Upload("committed.txt");
            var second = await db.LearningFiles.AsNoTracking().SingleAsync(x => x.Id != saved.Id);
            Assert.NotEqual(saved.ObjectKey, second.ObjectKey); Assert.Equal(2, await db.LearningFiles.CountAsync());
            Assert.Equal(bytes, (await operations.Download(owner, second.Id, default)).Bytes);
            await outbox.ProcessBatch(default);
            Assert.Equal(bytes, await files.GetAsync(saved.ObjectKey, default));
            Assert.Equal(bytes, await files.GetAsync(second.ObjectKey, default));
            var documents = new Phase19CompletionWorkflowTests(fixture);
            await documents.PdfIssueSurvivesKeyRotationAndChecksStorageOwnershipRevocationAndSupersession();
            await documents.LegacyProofCaptureAndNewKeyRevocationAreIndependentOfJwtRotation();
            await new FinalRemediationIntegrationTests(fixture).IssuedGraduationPdfBecomesSourceChangedAndReissuanceRetainsOriginalArtifact();
            output.WriteLine("Real MinIO PDF regressions: stored PDF retrieval/digest, ownership, proof validation, key rotation/revocation, supersession, source invalidation and reissuance preserve original artifacts.");
            output.WriteLine("Real MinIO: upload, authorized owner/staff download, unrelated-student and anonymous denial, metadata persistence, referenced-object preservation, post-upload database rollback, actual server outage, durable Retry, restart persistence and cleanup recovery passed. No TestArtifactStore used.");
        }
        finally { await fixture.DisposeAsync(); }
    }
}

