using api.Services.IServices;
using Microsoft.AspNetCore.Mvc;

namespace api.Controllers;

/// <summary>
/// Test-only helpers for BDD scenario setup.
/// Active only when ASPNETCORE_ENVIRONMENT is Development or Test.
/// </summary>
[ApiController]
[Route("api/debug")]
public sealed class DebugController(IUserService userService, IWebHostEnvironment env) : ControllerBase
{
    [HttpPost("ensure-user")]
    public async Task<IActionResult> EnsureUser([FromBody] EnsureUserRequest request)
    {
        if (!env.IsDevelopment() && env.EnvironmentName != "Test")
            return NotFound();

        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            return BadRequest("username and password required");

        var existing = await userService.GetUserByUsernameAsync(request.Username, null);
        if (existing.IsSuccess)
            return Ok(new { message = "exists" });

        var email = request.Email ?? $"{request.Username}@example.com";
        var result = await userService.RegisterUserAsync(
            new RegisterRequest(email, request.Username, request.Password, request.Password));

        return result.IsSuccess
            ? Ok(new { message = "created" })
            : BadRequest(new { error = result.Error });
    }

    [HttpPost("ensure-deleted")]
    public async Task<IActionResult> EnsureDeleted([FromBody] EnsureDeletedRequest request)
    {
        if (!env.IsDevelopment() && env.EnvironmentName != "Test")
            return NotFound();

        if (string.IsNullOrWhiteSpace(request.Username))
            return BadRequest("username required");

        var existing = await userService.GetUserByUsernameAsync(request.Username, null);
        if (!existing.IsSuccess)
            return Ok(new { message = "not_found" });

        var result = await userService.DeleteUserByUsernameAsync(request.Username);
        return result.IsSuccess
            ? Ok(new { message = "deleted" })
            : BadRequest(new { error = result.Error });
    }

    [HttpPost("ensure-follow")]
    public async Task<IActionResult> EnsureFollow([FromBody] EnsureFollowRequest request)
    {
        if (!env.IsDevelopment() && env.EnvironmentName != "Test")
            return NotFound();

        if (string.IsNullOrWhiteSpace(request.Follower) || string.IsNullOrWhiteSpace(request.Following))
            return BadRequest("follower and following required");

        var result = await userService.FollowUserAsync(request.Following, request.Follower);
        return result.IsSuccess
            ? Ok(new { message = "following" })
            : BadRequest(new { error = result.Error });
    }
}

public record EnsureUserRequest(string Username, string Password, string? Email = null);
public record EnsureFollowRequest(string Follower, string Following);
public record EnsureDeletedRequest(string Username);
