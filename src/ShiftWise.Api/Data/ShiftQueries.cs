using Microsoft.EntityFrameworkCore;

namespace ShiftWise.Api.Data;

public static class ShiftQueries
{
    // Two ranges overlap when each one starts before the other ends.
    // Back-to-back shifts (one ends at 15:00, the next starts at 15:00) are allowed.
    public static Task<bool> EmployeeIsBusyAsync(
        this AppDbContext db,
        int companyId,
        int employeeId,
        DateTime startsAt,
        DateTime endsAt,
        int? ignoreShiftId = null)
    {
        return db.Shifts.AnyAsync(s =>
            s.CompanyId == companyId &&
            s.EmployeeId == employeeId &&
            (ignoreShiftId == null || s.Id != ignoreShiftId) &&
            s.StartsAt < endsAt &&
            s.EndsAt > startsAt);
    }
}
