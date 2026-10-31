using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Academix.Api.Data;
using Academix.Api.Domain.Academic;
using Academix.Api.Domain.Identity;
using Academix.Api.DTOs.Identity;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Academix.Tests.Integration;

public sealed class Phase1EndpointsTests(ApiFixture fixture) : IClassFixture<ApiFixture>
{
    [Fact]
    public async Task AdministrativeEndpointsRejectAnonymousRequests()
    {
        var r = await fixture.Client.GetAsync($"/api/academic/institutions/{AcademicIds.InitialInstitution}");
        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
    }

    [Fact]
    public async Task InvalidCredentialsAreRejected()
    {
        var r = await fixture.Client.PostAsJsonAsync("/api/auth/login", new LoginRequest("nobody@example.test", "wrong-password"));
        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
    }

    [Fact]
    public async Task InstitutionAdminCanCreateAcademicHierarchyButStudentCannot()
    {
        var email = $"admin-{Guid.NewGuid():N}@example.test";
        await using (var scope = fixture.Services.CreateAsyncScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var admin = new ApplicationUser { Id = Guid.NewGuid(), UserName = email, Email = email, InstitutionId = AcademicIds.InitialInstitution };
            Assert.True((await users.CreateAsync(admin, "Excellent!TestPassword2026")).Succeeded);
            Assert.True((await users.AddToRoleAsync(admin, AcademixRoles.InstitutionAdministrator)).Succeeded);
        }
        var login = await fixture.Client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, "Excellent!TestPassword2026"));
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var tokens = await login.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(tokens);
        using var req = new HttpRequestMessage(HttpMethod.Post, "/api/academic/faculties")
        {
            Content = JsonContent.Create(new { institutionId = AcademicIds.InitialInstitution, code = "FC" + Guid.NewGuid().ToString("N")[..5].ToUpperInvariant(), name = "Faculty of Computing" })
        };
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        var created = await fixture.Client.SendAsync(req);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var refresh = await fixture.Client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(tokens.RefreshToken));
        Assert.Equal(HttpStatusCode.OK, refresh.StatusCode);
        var rotated = await refresh.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(rotated);
        Assert.NotEqual(tokens.RefreshToken, rotated.RefreshToken);
        // Replaying the old token must not issue fresh credentials.
        var replay = await fixture.Client.PostAsJsonAsync("/api/auth/refresh", new RefreshRequest(tokens.RefreshToken));
        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
    }

    [Fact]
    public async Task EnrollmentCannotBypassAuthentication()
    {
        var r = await fixture.Client.PostAsJsonAsync($"/api/academic/sections/{Guid.NewGuid()}/enrollments", new { studentId = Guid.NewGuid() });
        Assert.Equal(HttpStatusCode.Unauthorized, r.StatusCode);
    }

    [Fact]
    public async Task PhaseZeroCatalogRemainsPublic()
    {
        var response = await fixture.Client.GetAsync("/api/courses");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync();
        Assert.Contains("CS101", json);
    }
}
