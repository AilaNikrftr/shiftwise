using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShiftWise.Api.Auth;
using ShiftWise.Api.Contracts;
using ShiftWise.Api.Services;

namespace ShiftWise.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(AuthService auth) : ControllerBase
{
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(RegisterCompanyRequest request)
    {
        return await auth.RegisterCompanyAsync(request);
    }

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(LoginRequest request)
    {
        return await auth.LoginAsync(request);
    }

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<UserDto>> Me()
    {
        return await auth.GetMeAsync(User.ToCurrentUser());
    }
}
