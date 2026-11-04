using System.Security.Claims;
using HotChocolate.Authorization;
using ShiftWise.Api.Auth;
using ShiftWise.Api.Contracts;
using ShiftWise.Api.Services;

namespace ShiftWise.Api.GraphQL;

[Authorize]
public class Mutation
{
    [Authorize(Roles = ["Manager"])]
    public Task<ShiftDto> CreateShift(SaveShiftRequest input, ClaimsPrincipal user, ShiftService shifts) =>
        shifts.CreateAsync(user.ToCurrentUser(), input);

    [Authorize(Roles = ["Manager"])]
    public Task<ShiftDto> UpdateShift(int id, SaveShiftRequest input, ClaimsPrincipal user, ShiftService shifts) =>
        shifts.UpdateAsync(user.ToCurrentUser(), id, input);

    [Authorize(Roles = ["Manager"])]
    public async Task<bool> DeleteShift(int id, ClaimsPrincipal user, ShiftService shifts)
    {
        await shifts.DeleteAsync(user.ToCurrentUser(), id);
        return true;
    }

    public Task<SwapDto> RequestSwap(CreateSwapRequest input, ClaimsPrincipal user, SwapService swaps) =>
        swaps.CreateAsync(user.ToCurrentUser(), input);

    [Authorize(Roles = ["Manager"])]
    public Task<SwapDto> ApproveSwap(int id, ClaimsPrincipal user, SwapService swaps) =>
        swaps.ApproveAsync(user.ToCurrentUser(), id);

    [Authorize(Roles = ["Manager"])]
    public Task<SwapDto> RejectSwap(int id, ClaimsPrincipal user, SwapService swaps) =>
        swaps.RejectAsync(user.ToCurrentUser(), id);
}
