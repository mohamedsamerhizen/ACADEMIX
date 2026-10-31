using System.Net.Mail;
using Academix.Api.DTOs.Identity;
using Academix.Api.Middleware;

namespace Academix.Api.Services.Identity;

public static class IdentityInputPolicy
{
    public static void Login(LoginRequest? request)
    {
        if (request is null || !ValidEmail(request.Email) || string.IsNullOrWhiteSpace(request.Password) || request.Password.Length > 1024)
            throw ApiFault.Unauthorized();
    }

    public static void Provision(ProvisionUserRequest? request)
    {
        if (request is null || !ValidEmail(request.Email) || string.IsNullOrWhiteSpace(request.Password) || request.Password.Length > 1024)
            throw ApiFault.Bad("Valid email and password required.");
    }

    private static bool ValidEmail(string? email)
        => !string.IsNullOrWhiteSpace(email) && email.Trim().Length <= 256 &&
            MailAddress.TryCreate(email.Trim(), out var parsed) && parsed.Address == email.Trim();
}
