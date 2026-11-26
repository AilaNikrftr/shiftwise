using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShiftWise.Api.Auth;
using ShiftWise.Api.Contracts;
using ShiftWise.Api.Domain;
using ShiftWise.Api.Services;
using ShiftWise.Api.Storage;

namespace ShiftWise.Api.Controllers;

[ApiController]
[Authorize(Roles = nameof(UserRole.Manager))]
[Route("api/reports")]
public class ReportsController(ReportService reports, IServiceProvider services) : ControllerBase
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

    // Saves the CSV to Azure Blob Storage and returns a short-lived download link.
    [HttpPost("hours/archive")]
    public async Task<ActionResult<ArchiveResult>> ArchiveHours([FromQuery] DateTime from, [FromQuery] DateTime to,
        CancellationToken ct)
    {
        if (Archive is not { } archive) return ArchiveDisabled();

        var me = User.ToCurrentUser();
        var rows = await reports.HoursAsync(me, from, to);
        var result = await archive.SaveAsync(me.CompanyId, from, to, ReportService.ToCsv(rows), rows.Count, ct);
        return Created(result.DownloadUrl, result);
    }

    [HttpGet("archive")]
    public async Task<ActionResult<List<ArchivedExport>>> ListArchive(CancellationToken ct)
    {
        if (Archive is not { } archive) return ArchiveDisabled();

        var me = User.ToCurrentUser();
        me.RequireManager();
        return await archive.ListAsync(me.CompanyId, ct);
    }

    // Fresh link for an export archived earlier. {*name} keeps the slash in "company-1/...".
    [HttpGet("archive/{*name}")]
    public async Task<ActionResult<ArchiveResult>> ArchiveLink(string name, CancellationToken ct)
    {
        if (Archive is not { } archive) return ArchiveDisabled();

        var me = User.ToCurrentUser();
        me.RequireManager();
        return await archive.GetLinkAsync(me.CompanyId, Uri.UnescapeDataString(name), ct);
    }

    // Resolved per request so the API still starts when storage isn't configured.
    private PayrollArchive? Archive => services.GetService<PayrollArchive>();

    private ObjectResult ArchiveDisabled() =>
        Problem(statusCode: StatusCodes.Status503ServiceUnavailable,
            detail: "Payroll archive storage is not configured on this server.");
}
