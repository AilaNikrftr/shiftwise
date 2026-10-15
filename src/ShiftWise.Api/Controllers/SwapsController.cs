using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShiftWise.Api.Auth;
using ShiftWise.Api.Contracts;
using ShiftWise.Api.Domain;
using ShiftWise.Api.Services;

namespace ShiftWise.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/swaps")]
public class SwapsController(SwapService swaps) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<SwapDto>>> List()
    {
        return await swaps.ListAsync(User.ToCurrentUser());
    }

    [HttpPost]
    public async Task<ActionResult<SwapDto>> Create(CreateSwapRequest request)
    {
        var created = await swaps.CreateAsync(User.ToCurrentUser(), request);
        return StatusCode(StatusCodes.Status201Created, created);
    }

    [Authorize(Roles = nameof(UserRole.Manager))]
    [HttpPost("{id:int}/approve")]
    public async Task<ActionResult<SwapDto>> Approve(int id)
    {
        return await swaps.ApproveAsync(User.ToCurrentUser(), id);
    }

    [Authorize(Roles = nameof(UserRole.Manager))]
    [HttpPost("{id:int}/reject")]
    public async Task<ActionResult<SwapDto>> Reject(int id)
    {
        return await swaps.RejectAsync(User.ToCurrentUser(), id);
    }

    [HttpPost("{id:int}/cancel")]
    public async Task<ActionResult<SwapDto>> Cancel(int id)
    {
        return await swaps.CancelAsync(User.ToCurrentUser(), id);
    }
}
