namespace Academix.Api.DTOs.Identity;

public sealed record LoginRequest(string Email, string Password);
public sealed record RefreshRequest(string RefreshToken);
public sealed record AuthResponse(
    string AccessToken,
    string RefreshToken,
    DateTime AccessExpiresAtUtc,
    DateTime SessionExpiresAtUtc);
public sealed record ProvisionUserRequest(Guid? InstitutionId, string Email, string Password, string Role);
public sealed record ChangeRoleRequest(string Role, bool Add);
public sealed record ChangeActiveRequest(bool IsActive);
