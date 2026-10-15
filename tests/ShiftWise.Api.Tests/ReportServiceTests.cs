using ShiftWise.Api.Common;
using ShiftWise.Api.Contracts;
using ShiftWise.Api.Services;

namespace ShiftWise.Api.Tests;

public class ReportServiceTests : IDisposable
{
    private readonly TestData _data = new();
    private readonly ReportService _service;

    public ReportServiceTests()
    {
        _service = new ReportService(_data.Db);
    }

    [Fact]
    public async Task Hours_adds_up_shift_lengths_per_employee()
    {
        var day1 = TestData.Today.AddDays(1);
        var day2 = TestData.Today.AddDays(2);
        _data.AddShift(_data.Alice, day1.AddHours(9), day1.AddHours(17));            // 8h
        _data.AddShift(_data.Alice, day2.AddHours(9), day2.AddHours(13).AddMinutes(30)); // 4.5h
        _data.AddShift(_data.Carol, day1.AddHours(9), day1.AddHours(17));            // other company

        var rows = await _service.HoursAsync(TestData.As(_data.ManagerA), TestData.Today, TestData.Today.AddDays(7));

        var alice = rows.Single(r => r.EmployeeId == _data.Alice.Id);
        Assert.Equal(2, alice.ShiftCount);
        Assert.Equal(12.5, alice.TotalHours);

        // Bob and the manager show up with zero hours. Carol doesn't show up at all.
        Assert.Equal(3, rows.Count);
        Assert.Equal(0.0, rows.Single(r => r.EmployeeId == _data.Bob.Id).TotalHours);
        Assert.DoesNotContain(rows, r => r.EmployeeId == _data.Carol.Id);
    }

    [Fact]
    public async Task Employees_cannot_run_reports()
    {
        var ex = await Assert.ThrowsAsync<AppException>(() =>
            _service.HoursAsync(TestData.As(_data.Alice), TestData.Today, TestData.Today.AddDays(7)));

        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public void Csv_quotes_names_with_commas()
    {
        var csv = ReportService.ToCsv(new[] { new HoursRow(1, "Lee, Jordan", 2, 12.5) });

        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        Assert.Equal("employee_id,employee_name,shift_count,total_hours", lines[0]);
        Assert.Equal("1,\"Lee, Jordan\",2,12.5", lines[1]);
    }

    public void Dispose() => _data.Dispose();
}
