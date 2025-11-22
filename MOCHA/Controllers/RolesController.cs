using Microsoft.AspNetCore.Mvc;
using MOCHA.Models.Auth;
using MOCHA.Services.Auth;

namespace MOCHA.Controllers;

[ApiController]
[Route("api/roles")]
public class RolesController : ControllerBase
{
    private readonly IUserRoleProvider _roleProvider;

    public RolesController(IUserRoleProvider roleProvider)
    {
        _roleProvider = roleProvider;
    }

    [HttpGet("{userId}")]
    public async Task<ActionResult<IEnumerable<string>>> GetRoles(string userId, CancellationToken cancellationToken)
    {
        if (!await RequireAdminAsync(cancellationToken))
        {
            return Forbid();
        }

        var roles = await _roleProvider.GetRolesAsync(userId, cancellationToken);
        return Ok(roles.Select(r => r.Value));
    }

    [HttpPost("assign")]
    public async Task<IActionResult> Assign([FromBody] RoleChangeRequest request, CancellationToken cancellationToken)
    {
        if (!await RequireAdminAsync(cancellationToken))
        {
            return Forbid();
        }

        if (string.IsNullOrWhiteSpace(request.UserId) || string.IsNullOrWhiteSpace(request.Role))
        {
            return BadRequest("UserId と Role は必須です。");
        }

        await _roleProvider.AssignAsync(request.UserId, UserRoleId.From(request.Role), cancellationToken);
        return Ok();
    }

    [HttpDelete("{userId}/{role}")]
    public async Task<IActionResult> Remove(string userId, string role, CancellationToken cancellationToken)
    {
        if (!await RequireAdminAsync(cancellationToken))
        {
            return Forbid();
        }

        await _roleProvider.RemoveAsync(userId, UserRoleId.From(role), cancellationToken);
        return Ok();
    }

    private async Task<bool> RequireAdminAsync(CancellationToken cancellationToken)
    {
        var userId = User.GetUserObjectId();
        if (userId is null)
        {
            return false;
        }

        return await _roleProvider.IsInRoleAsync(userId, UserRoleId.Predefined.Administrator.Value, cancellationToken);
    }
}

public sealed class RoleChangeRequest
{
    public string UserId { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}
