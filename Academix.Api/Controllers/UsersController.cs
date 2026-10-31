using Academix.Api.DTOs.Identity;
using Academix.Api.Services.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Academix.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/admin/users")]
public sealed class UsersController(AuthService service) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<object>> Create(ProvisionUserRequest request, CancellationToken ct)
        => Created("/api/auth/me", await service.CreateUserAsync(User, request, ct));

    [HttpPut("{id:guid}/roles")]
    public async Task<IActionResult> ChangeRole(Guid id, ChangeRoleRequest request, CancellationToken ct)
    {
        await service.ChangeRoleAsync(User, id, request, ct);
        return NoContent();
    }

    [HttpPut("{id:guid}/activation")]
    public async Task<IActionResult> ChangeActivation(Guid id, ChangeActiveRequest request, CancellationToken ct)
    {
        await service.ChangeActiveAsync(User, id, request, ct);
        return NoContent();
    }
}
