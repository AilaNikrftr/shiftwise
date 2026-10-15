using Microsoft.EntityFrameworkCore;
using ShiftWise.Api.Auth;
using ShiftWise.Api.Common;
using ShiftWise.Api.Contracts;
using ShiftWise.Api.Data;
using ShiftWise.Api.Domain;

namespace ShiftWise.Api.Services;

public class ShiftService(AppDbContext db)
{
    public static readonly TimeSpan MaxShiftLength = TimeSpan.FromHours(16);

    // Everyone in the company can see the full schedule, not just their own shifts.
    public async Task<List<ShiftDto>> ListAsync(CurrentUser me, DateTime from, DateTime to)
    {
        if (to <= from)
        {
            throw AppException.BadRequest("'to' must be after 'from'.");
        }

        var shifts = await db.Shifts
            .Include(s => s.Employee)
            .Where(s => s.CompanyId == me.CompanyId && s.StartsAt < to && s.EndsAt > from)
            .OrderBy(s => s.StartsAt)
            .ToListAsync();

        return shifts.Select(ToDto).ToList();
    }

    public async Task<ShiftDto> CreateAsync(CurrentUser me, SaveShiftRequest request)
    {
        me.RequireManager();
        var employee = await ValidateAsync(me, request, ignoreShiftId: null);

        var shift = new Shift
        {
            CompanyId = me.CompanyId,
            EmployeeId = employee.Id,
            Employee = employee,
            StartsAt = request.StartsAt,
            EndsAt = request.EndsAt,
            Position = request.Position.Trim(),
            Notes = CleanNotes(request.Notes)
        };

        db.Shifts.Add(shift);
        await db.SaveChangesAsync();

        return ToDto(shift);
    }

    public async Task<ShiftDto> UpdateAsync(CurrentUser me, int shiftId, SaveShiftRequest request)
    {
        me.RequireManager();
        var shift = await FindAsync(me, shiftId);
        var employee = await ValidateAsync(me, request, ignoreShiftId: shift.Id);

        shift.EmployeeId = employee.Id;
        shift.Employee = employee;
        shift.StartsAt = request.StartsAt;
        shift.EndsAt = request.EndsAt;
        shift.Position = request.Position.Trim();
        shift.Notes = CleanNotes(request.Notes);

        await db.SaveChangesAsync();
        return ToDto(shift);
    }

    public async Task DeleteAsync(CurrentUser me, int shiftId)
    {
        me.RequireManager();
        var shift = await FindAsync(me, shiftId);

        db.Shifts.Remove(shift);
        await db.SaveChangesAsync();
    }

    private async Task<Shift> FindAsync(CurrentUser me, int shiftId)
    {
        // Filtering by company means another company's shift id just looks like it doesn't exist.
        return await db.Shifts.FirstOrDefaultAsync(s => s.Id == shiftId && s.CompanyId == me.CompanyId)
            ?? throw AppException.NotFound("Shift not found.");
    }

    private async Task<User> ValidateAsync(CurrentUser me, SaveShiftRequest request, int? ignoreShiftId)
    {
        if (string.IsNullOrWhiteSpace(request.Position))
        {
            throw AppException.BadRequest("Position is required.");
        }

        if (request.EndsAt <= request.StartsAt)
        {
            throw AppException.BadRequest("A shift has to end after it starts.");
        }

        if (request.EndsAt - request.StartsAt > MaxShiftLength)
        {
            throw AppException.BadRequest($"Shifts can't be longer than {MaxShiftLength.TotalHours} hours.");
        }

        var employee = await db.Users.FirstOrDefaultAsync(u =>
                u.Id == request.EmployeeId && u.CompanyId == me.CompanyId && u.IsActive)
            ?? throw AppException.NotFound("Employee not found.");

        var busy = await db.EmployeeIsBusyAsync(
            me.CompanyId, employee.Id, request.StartsAt, request.EndsAt, ignoreShiftId);

        if (busy)
        {
            throw AppException.Conflict($"{employee.FullName} already has a shift during that time.");
        }

        return employee;
    }

    private static string? CleanNotes(string? notes) =>
        string.IsNullOrWhiteSpace(notes) ? null : notes.Trim();

    public static ShiftDto ToDto(Shift shift) => new(
        shift.Id,
        shift.EmployeeId,
        shift.Employee?.FullName ?? "",
        shift.StartsAt,
        shift.EndsAt,
        shift.Position,
        shift.Notes);
}
