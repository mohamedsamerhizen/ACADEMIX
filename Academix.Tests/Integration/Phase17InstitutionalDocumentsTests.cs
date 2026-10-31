using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Academix.Api.Data;
using Academix.Api.Domain.Academic;
using Academix.Api.Domain.Identity;
using Academix.Api.Domain.Records;
using Academix.Api.DTOs.Identity;
using Academix.Api.DTOs.Institutional;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace Academix.Tests.Integration;

// Requires Testcontainers PostgreSQL and the complete EF migration chain.
public sealed class Phase17InstitutionalDocumentsTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    private const string Password = "Academic!Documents2026Secure";

    private async Task<(Guid Id, string Token)> Account(Guid institution, string role)
    {
        var id = Guid.NewGuid();
        var email = $"documents-{id:N}@example.test";
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var manager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser { Id = id, InstitutionId = institution, UserName = email, Email = email };
            Assert.True((await manager.CreateAsync(user, Password)).Succeeded);
            Assert.True((await manager.AddToRoleAsync(user, role)).Succeeded);
        }
        using var login = await fixture.Client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, Password));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        return (id, (await login.Content.ReadFromJsonAsync<AuthResponse>())!.AccessToken);
    }

    private async Task<HttpResponseMessage> Call(HttpMethod method, string path, string? token, object? body = null)
    {
        using var request = new HttpRequestMessage(method, path);
        if (token is not null) request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        if (body is not null) request.Content = JsonContent.Create(body);
        return await fixture.Client.SendAsync(request);
    }

    [Fact]
    public async Task SubmitApproveIssueVerifyRevoke_EnforcesOwnershipAndVersion()
    {
        var inst = Guid.NewGuid(); var dept = Guid.NewGuid(); var program = Guid.NewGuid();
        var term = Guid.NewGuid();
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AcademixDbContext>();
            db.Institutions.Add(new Institution { Id = inst,
                Code = "DOC" + Guid.NewGuid().ToString("N")[..7].ToUpperInvariant(), Name = "Documents University" });
            db.Departments.Add(new Department { Id = dept, InstitutionId = inst, Code = "DOC", Name = "Records" });
            db.AcademicPrograms.Add(new AcademicProgram { Id = program, InstitutionId = inst,
                DepartmentId = dept, Code = "BSC", Name = "Computer Science" });
            db.AcademicTerms.Add(new AcademicTerm { Id = term, InstitutionId = inst,
                Code = "T1", Name = "Term One", StartsOn = new DateOnly(2026, 1, 1),
                EndsOn = new DateOnly(2027, 1, 1), Status = "Active" });
            await db.SaveChangesAsync();
        }
        var (studentId, student) = await Account(inst, AcademixRoles.Student);
        var (_, admin) = await Account(inst, AcademixRoles.InstitutionAdministrator);
        var (_, outsider) = await Account(inst, AcademixRoles.Student);
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AcademixDbContext>();
            db.StudentProgramMemberships.Add(new StudentProgramMembership {
                InstitutionId = inst, StudentId = studentId, ProgramId = program,
                AdmissionTermId = term, CurrentTermId = term, Status = "Active"
            });
            await db.SaveChangesAsync();
        }
        var idempotency = Guid.NewGuid();
        var body = new SubmitDocumentRequest(idempotency, "Enrollment", "Employment verification");
        using var submit = await Call(HttpMethod.Post, "/api/institutional-documents/requests", student, body);
        Assert.Equal(HttpStatusCode.OK, submit.StatusCode);
        var request = (await submit.Content.ReadFromJsonAsync<DocumentRequestView>())!;
        using var duplicate = await Call(HttpMethod.Post, "/api/institutional-documents/requests", student, body);
        Assert.Equal(HttpStatusCode.OK, duplicate.StatusCode);
        Assert.Equal(request.Id, (await duplicate.Content.ReadFromJsonAsync<DocumentRequestView>())!.Id);
        using var badReuse = await Call(HttpMethod.Post, "/api/institutional-documents/requests", student,
            new SubmitDocumentRequest(idempotency, "Standing", "Employment verification"));
        Assert.Equal(HttpStatusCode.Conflict, badReuse.StatusCode);

        using var forbidden = await Call(HttpMethod.Post, $"/api/institutional-documents/requests/{request.Id}/review",
            outsider, new DecideDocumentRequest(1, true, "Approved for work"));
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);
        using var reviewed = await Call(HttpMethod.Post, $"/api/institutional-documents/requests/{request.Id}/review",
            admin, new DecideDocumentRequest(1, true, "Verified enrollment"));
        Assert.Equal(HttpStatusCode.OK, reviewed.StatusCode);
        using var stale = await Call(HttpMethod.Post, $"/api/institutional-documents/requests/{request.Id}/issue",
            admin, new IssueDocumentRequest(1));
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using var issued = await Call(HttpMethod.Post, $"/api/institutional-documents/requests/{request.Id}/issue",
            admin, new IssueDocumentRequest(2));
        Assert.Equal(HttpStatusCode.OK, issued.StatusCode);
        var doc = (await issued.Content.ReadFromJsonAsync<AcademicDocumentView>())!;
        Assert.Contains("ACADEMIX-Enrollment-v1", doc.SnapshotJson);
        Assert.Equal(64, doc.VerificationProof.Length);
        using var noAccess = await Call(HttpMethod.Get, $"/api/institutional-documents/documents/{doc.Id}", outsider);
        Assert.Equal(HttpStatusCode.Forbidden, noAccess.StatusCode);
        using var verified = await Call(HttpMethod.Post, "/api/institutional-documents/verify", null,
            new VerifyAcademicDocument(doc.ReferenceNumber, doc.VerificationProof));
        Assert.Equal(HttpStatusCode.OK, verified.StatusCode);
        Assert.True((await verified.Content.ReadFromJsonAsync<AcademicDocumentVerification>())!.Valid);
        using var invalid = await Call(HttpMethod.Post, "/api/institutional-documents/verify", null,
            new VerifyAcademicDocument(doc.ReferenceNumber, new string('0', 64)));
        Assert.Equal(HttpStatusCode.NotFound, invalid.StatusCode);
        using var revoked = await Call(HttpMethod.Post, $"/api/institutional-documents/documents/{doc.Id}/revoke",
            admin, new RevokeAcademicDocument(1, "Issued by mistake"));
        Assert.Equal(HttpStatusCode.OK, revoked.StatusCode);
        using var revokedVerification = await Call(HttpMethod.Post, "/api/institutional-documents/verify", null,
            new VerifyAcademicDocument(doc.ReferenceNumber, doc.VerificationProof));
        Assert.Equal("Revoked", (await revokedVerification.Content.ReadFromJsonAsync<AcademicDocumentVerification>())!.Status);
    }
}
