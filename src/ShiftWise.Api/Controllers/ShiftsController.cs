using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShiftWise.Api.Auth;
using ShiftWise.Api.Contracts;
using ShiftWise.Api.Domain;
using ShiftWise.Api.Services;

namespace ShiftWise.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/shifts")]
public class ShiftsController(ShiftService shifts, TimeProvider clock) : ControllerBase
{
    // Defaults to the current week (Monday to Monday) if no range is given.
    [HttpGet]
    public async Task<ActionResult<List<ShiftDto>>> List([FromQuery] DateTime? from, [FromQuery] DateTime? to)
    {
        var start = from ?? WeekStart(clock.GetLocalNow().DateTime);
        var end = to ?? start.AddDays(7);

        return await shifts.ListAsync(User.ToCurrentUser(), start, end);
    }

    [Authorize(Roles = nameof(UserRole.Manager))]
    [HttpPost]
    public async Task<ActionResult<ShiftDto>> Create(SaveShiftRequest request)
    {
        var created = await shifts.CreateAsync(User.ToCurrentUser(), request);
        return StatusCode(StatusCodes.Status201Created, created);
    }

    [Authorize(Roles = nameof(UserRole.Manager))]
    [HttpPut("{id:int}")]
    public async Task<ActionResult<ShiftDto>> Update(int id, SaveShiftRequest request)
    {
        return await shifts.UpdateAsync(User.ToCurrentUser(), id, request);
    }

    [Authorize(Roles = nameof(UserRole.Manager))]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        await shifts.DeleteAsync(User.ToCurrentUser(), id);
        return NoContent();
    }

    internal static DateTime WeekStart(DateTime date)
    {
        // DayOfWeek.Sunday is 0, so shift it to the end of the week.
        var daysSinceMonday = ((int)date.DayOfWeek + 6) % 7;
        return date.Date.AddDays(-daysSinceMonday);
    }
}
