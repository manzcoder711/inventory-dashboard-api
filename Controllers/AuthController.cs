using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using InventoryApi.DTOs;
using InventoryApi.Middleware;
using InventoryApi.Services;

namespace InventoryApi.Controllers;

[ApiController]
[Route("api/auth")]
[EnableRateLimiting(RateLimitPolicies.Auth)]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken ct)
    {
        var success = await _authService.RegisterAsync(request, ct);
        if (!success)
        {
            return Conflict(new { message = "An account with this email already exists." });
        }

        return Ok();
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request, CancellationToken ct)
    {
        var response = await _authService.LoginAsync(request, ct);
        if (response is null)
        {
            return Unauthorized(new { message = "Invalid email or password." });
        }

        return response;
    }
}
