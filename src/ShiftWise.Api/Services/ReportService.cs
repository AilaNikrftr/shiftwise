using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using ShiftWise.Api.Auth;
using ShiftWise.Api.Common;
using ShiftWise.Api.Contracts;
using ShiftWise.Api.Data;

namespace ShiftWise.Api.Services;

public class ReportService(AppDbContext db)
{
    // Hours per employee for shifts that start inside [from, to).
    // Everyone active shows up, including people with zero hours,
    // so payroll can spot who wasn't scheduled.
    public async Task<List<HoursRow>> HoursAsync(CurrentUser me, DateTime from, DateTime to)
    {
        me.RequireManager();

        if (to <= from)
        {
            throw AppException.BadRequest("'to' must be after 'from'.");
        }

        var employees = await db.Users
            .Where(u => u.CompanyId == me.CompanyId && u.IsActive)
            .OrderBy(u => u.FullName)
            .Select(u => new { u.Id, u.FullName })
            .ToListAsync();

        // Pulling the shifts into memory first because EF can't translate
        // (EndsAt - StartsAt).TotalHours into SQL. Fine for a week or a month of data.
        var shifts = await db.Shifts
            .Where(s => s.CompanyId == me.CompanyId && s.StartsAt >= from && s.StartsAt < to)
            .Select(s => new { s.EmployeeId, s.StartsAt, s.EndsAt })
            .ToListAsync();

        var byEmployee = shifts
            .GroupBy(s => s.EmployeeId)
            .ToDictionary(
                g => g.Key,
                g => (Count: g.Count(), Hours: g.Sum(s => (s.EndsAt - s.StartsAt).TotalHours)));

        return employees
            .Select(e =>
            {
                byEmployee.TryGetValue(e.Id, out var totals);
                return new HoursRow(e.Id, e.FullName, totals.Count, Math.Round(totals.Hours, 2));
            })
            .ToList();
    }

    public static string ToCsv(IEnumerable<HoursRow> rows)
    {
        var sb = new StringBuilder();
        sb.AppendLine("employee_id,employee_name,shift_count,total_hours");

        foreach (var row in rows)
        {
            sb.Append(row.EmployeeId).Append(',')
              .Append(EscapeCsv(row.EmployeeName)).Append(',')
              .Append(row.ShiftCount).Append(',')
              .Append(row.TotalHours.ToString("0.##", CultureInfo.InvariantCulture))
              .AppendLine();
        }

        return sb.ToString();
    }

    private static string EscapeCsv(string value)
    {
        if (value.IndexOfAny(new[] { ',', '"', '\n', '\r' }) < 0)
        {
            return value;
        }

        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}
