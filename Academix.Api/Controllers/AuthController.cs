using Academix.Api.DTOs.Identity;
using Academix.Api.Services.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Academix.Api.Controllers;

[ApiController]
[Route("api/auth")]
public sealed class AuthController(AuthService service) : ControllerBase
{
    [AllowAnonymous]
    [EnableRateLimiting("authentication")]
    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken ct)
        => Ok(await service.LoginAsync(request, ct));

    [AllowAnonymous]
    [EnableRateLimiting("authentication")]
    [HttpPost("refresh")]
    public async Task<ActionResult<AuthResponse>> Refresh(RefreshRequest request, CancellationToken ct)
        => Ok(await service.RefreshAsync(request, ct));

    [Authorize]
    [HttpPost("logout")]
    public async Task<IActionResult> Logout(RefreshRequest request, CancellationToken ct)
    {
        await service.LogoutAsync(User, request, ct);
        return NoContent();
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<object>> Me(CancellationToken ct)
        => Ok(await service.ProfileAsync(User, ct));
}
