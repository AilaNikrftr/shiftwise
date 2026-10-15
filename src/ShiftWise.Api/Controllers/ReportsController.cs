using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShiftWise.Api.Auth;
using ShiftWise.Api.Contracts;
using ShiftWise.Api.Domain;
using ShiftWise.Api.Services;

namespace ShiftWise.Api.Controllers;

[ApiController]
[Authorize(Roles = nameof(UserRole.Manager))]
[Route("api/reports")]
public class ReportsController(ReportService reports) : ControllerBase
{
    [HttpGet("hours")]
    public async Task<ActionResult<List<HoursRow>>> Hours([FromQuery] DateTime from, [FromQuery] DateTime to)
    {
        return await reports.HoursAsync(User.ToCurrentUser(), from, to);
    }

    // Same data as /hours, as a CSV file for payroll.
    [HttpGet("hours/export")]
    public async Task<IActionResult> ExportHours([FromQuery] DateTime from, [FromQuery] DateTime to)
    {
        var rows = await reports.HoursAsync(User.ToCurrentUser(), from, to);
        var csv = ReportService.ToCsv(rows);
        var fileName = $"hours_{from:yyyy-MM-dd}_to_{to:yyyy-MM-dd}.csv";

        return File(Encoding.UTF8.GetBytes(csv), "text/csv", fileName);
    }
}
