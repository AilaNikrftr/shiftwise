using ShiftWise.Api.Common;
using ShiftWise.Api.Contracts;
using ShiftWise.Api.Services;

namespace ShiftWise.Api.Tests;

public class ShiftServiceTests : IDisposable
{
    private readonly TestData _data = new();
    private readonly ShiftService _service;
    private readonly DateTime _tomorrow = TestData.Today.AddDays(1);

    public ShiftServiceTests()
    {
        _service = new ShiftService(_data.Db);
    }

    [Fact]
    public async Task Create_saves_shift_for_employee()
    {
        var request = new SaveShiftRequest(_data.Alice.Id, _tomorrow.AddHours(9), _tomorrow.AddHours(17), " Barista ", null);

        var created = await _service.CreateAsync(TestData.As(_data.ManagerA), request);

        Assert.Equal("Alice", created.EmployeeName);
        Assert.Equal("Barista", created.Position);
        Assert.Single(_data.Db.Shifts);
    }

    [Fact]
    public async Task Create_rejects_overlapping_shift_for_same_employee()
    {
        _data.AddShift(_data.Alice, _tomorrow.AddHours(9), _tomorrow.AddHours(17));
        var request = new SaveShiftRequest(_data.Alice.Id, _tomorrow.AddHours(16), _tomorrow.AddHours(20), "Cashier", null);

        var ex = await Assert.ThrowsAsync<AppException>(() => _service.CreateAsync(TestData.As(_data.ManagerA), request));

        Assert.Equal(409, ex.StatusCode);
    }

    [Fact]
    public async Task Create_allows_back_to_back_shifts()
    {
        _data.AddShift(_data.Alice, _tomorrow.AddHours(9), _tomorrow.AddHours(15));
        var request = new SaveShiftRequest(_data.Alice.Id, _tomorrow.AddHours(15), _tomorrow.AddHours(20), "Cashier", null);

        await _service.CreateAsync(TestData.As(_data.ManagerA), request);

        Assert.Equal(2, _data.Db.Shifts.Count());
    }

    [Fact]
    public async Task Create_rejects_shift_that_ends_before_it_starts()
    {
        var request = new SaveShiftRequest(_data.Alice.Id, _tomorrow.AddHours(17), _tomorrow.AddHours(9), "Barista", null);

        var ex = await Assert.ThrowsAsync<AppException>(() => _service.CreateAsync(TestData.As(_data.ManagerA), request));

        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task Employees_cannot_create_shifts()
    {
        var request = new SaveShiftRequest(_data.Alice.Id, _tomorrow.AddHours(9), _tomorrow.AddHours(17), "Barista", null);

        var ex = await Assert.ThrowsAsync<AppException>(() => _service.CreateAsync(TestData.As(_data.Alice), request));

        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task Manager_cannot_schedule_someone_from_another_company()
    {
        var request = new SaveShiftRequest(_data.Carol.Id, _tomorrow.AddHours(9), _tomorrow.AddHours(17), "Barista", null);

        var ex = await Assert.ThrowsAsync<AppException>(() => _service.CreateAsync(TestData.As(_data.ManagerA), request));

        Assert.Equal(404, ex.StatusCode);
    }

    [Fact]
    public async Task List_only_returns_shifts_from_own_company()
    {
        _data.AddShift(_data.Alice, _tomorrow.AddHours(9), _tomorrow.AddHours(17));
        _data.AddShift(_data.Carol, _tomorrow.AddHours(9), _tomorrow.AddHours(17));

        var shifts = await _service.ListAsync(TestData.As(_data.ManagerA), TestData.Today, TestData.Today.AddDays(7));

        var shift = Assert.Single(shifts);
        Assert.Equal(_data.Alice.Id, shift.EmployeeId);
    }

    [Fact]
    public async Task Update_can_move_a_shift_without_conflicting_with_itself()
    {
        var shift = _data.AddShift(_data.Alice, _tomorrow.AddHours(9), _tomorrow.AddHours(17));
        var request = new SaveShiftRequest(_data.Alice.Id, _tomorrow.AddHours(10), _tomorrow.AddHours(18), "Barista", "Opening");

        var updated = await _service.UpdateAsync(TestData.As(_data.ManagerA), shift.Id, request);

        Assert.Equal(_tomorrow.AddHours(10), updated.StartsAt);
        Assert.Equal("Opening", updated.Notes);
    }

    [Fact]
    public async Task Delete_from_another_company_is_not_found()
    {
        var shift = _data.AddShift(_data.Alice, _tomorrow.AddHours(9), _tomorrow.AddHours(17));

        var ex = await Assert.ThrowsAsync<AppException>(() => _service.DeleteAsync(TestData.As(_data.ManagerB), shift.Id));

        Assert.Equal(404, ex.StatusCode);
        Assert.Single(_data.Db.Shifts);
    }

    public void Dispose() => _data.Dispose();
}
