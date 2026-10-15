using ShiftWise.Api.Common;
using ShiftWise.Api.Contracts;
using ShiftWise.Api.Domain;
using ShiftWise.Api.Services;

namespace ShiftWise.Api.Tests;

public class SwapServiceTests : IDisposable
{
    private readonly TestData _data = new();
    private readonly SwapService _service;
    private readonly DateTime _tomorrow = TestData.Today.AddDays(1);

    public SwapServiceTests()
    {
        _service = new SwapService(_data.Db, _data.Clock);
    }

    [Fact]
    public async Task Approving_a_swap_moves_the_shift_to_the_coworker()
    {
        var shift = _data.AddShift(_data.Alice, _tomorrow.AddHours(9), _tomorrow.AddHours(17));
        var swap = await _service.CreateAsync(TestData.As(_data.Alice), new CreateSwapRequest(shift.Id, _data.Bob.Id, "Dentist"));

        var approved = await _service.ApproveAsync(TestData.As(_data.ManagerA), swap.Id);

        Assert.Equal(SwapStatus.Approved, approved.Status);
        Assert.Equal(_data.Bob.Id, _data.Db.Shifts.Single().EmployeeId);
    }

    [Fact]
    public async Task Approve_fails_if_coworker_is_already_working()
    {
        var shift = _data.AddShift(_data.Alice, _tomorrow.AddHours(9), _tomorrow.AddHours(17));
        var swap = await _service.CreateAsync(TestData.As(_data.Alice), new CreateSwapRequest(shift.Id, _data.Bob.Id, null));

        // Bob gets scheduled after the request was made.
        _data.AddShift(_data.Bob, _tomorrow.AddHours(12), _tomorrow.AddHours(20));

        var ex = await Assert.ThrowsAsync<AppException>(() => _service.ApproveAsync(TestData.As(_data.ManagerA), swap.Id));

        Assert.Equal(409, ex.StatusCode);
        Assert.Equal(_data.Alice.Id, _data.Db.Shifts.First(s => s.Id == shift.Id).EmployeeId);
    }

    [Fact]
    public async Task Cannot_request_swap_for_someone_elses_shift()
    {
        var shift = _data.AddShift(_data.Alice, _tomorrow.AddHours(9), _tomorrow.AddHours(17));

        var ex = await Assert.ThrowsAsync<AppException>(() =>
            _service.CreateAsync(TestData.As(_data.Bob), new CreateSwapRequest(shift.Id, _data.Bob.Id, null)));

        Assert.Equal(403, ex.StatusCode);
    }

    [Fact]
    public async Task Cannot_hand_a_shift_to_someone_in_another_company()
    {
        var shift = _data.AddShift(_data.Alice, _tomorrow.AddHours(9), _tomorrow.AddHours(17));

        var ex = await Assert.ThrowsAsync<AppException>(() =>
            _service.CreateAsync(TestData.As(_data.Alice), new CreateSwapRequest(shift.Id, _data.Carol.Id, null)));

        Assert.Equal(404, ex.StatusCode);
    }

    [Fact]
    public async Task Only_one_pending_request_per_shift()
    {
        var shift = _data.AddShift(_data.Alice, _tomorrow.AddHours(9), _tomorrow.AddHours(17));
        await _service.CreateAsync(TestData.As(_data.Alice), new CreateSwapRequest(shift.Id, _data.Bob.Id, null));

        var ex = await Assert.ThrowsAsync<AppException>(() =>
            _service.CreateAsync(TestData.As(_data.Alice), new CreateSwapRequest(shift.Id, _data.Bob.Id, null)));

        Assert.Equal(409, ex.StatusCode);
    }

    [Fact]
    public async Task Cannot_swap_a_shift_that_already_started()
    {
        // The test clock says 08:00 today.
        var shift = _data.AddShift(_data.Alice, TestData.Today.AddHours(7), TestData.Today.AddHours(15));

        var ex = await Assert.ThrowsAsync<AppException>(() =>
            _service.CreateAsync(TestData.As(_data.Alice), new CreateSwapRequest(shift.Id, _data.Bob.Id, null)));

        Assert.Equal(400, ex.StatusCode);
    }

    [Fact]
    public async Task Only_the_requester_can_cancel()
    {
        var shift = _data.AddShift(_data.Alice, _tomorrow.AddHours(9), _tomorrow.AddHours(17));
        var swap = await _service.CreateAsync(TestData.As(_data.Alice), new CreateSwapRequest(shift.Id, _data.Bob.Id, null));

        var ex = await Assert.ThrowsAsync<AppException>(() => _service.CancelAsync(TestData.As(_data.Bob), swap.Id));
        Assert.Equal(403, ex.StatusCode);

        var cancelled = await _service.CancelAsync(TestData.As(_data.Alice), swap.Id);
        Assert.Equal(SwapStatus.Cancelled, cancelled.Status);
    }

    [Fact]
    public async Task Employees_only_see_swaps_they_are_part_of()
    {
        var shift = _data.AddShift(_data.Alice, _tomorrow.AddHours(9), _tomorrow.AddHours(17));
        await _service.CreateAsync(TestData.As(_data.Alice), new CreateSwapRequest(shift.Id, _data.Bob.Id, null));

        Assert.Single(await _service.ListAsync(TestData.As(_data.Alice)));
        Assert.Single(await _service.ListAsync(TestData.As(_data.Bob)));
        Assert.Single(await _service.ListAsync(TestData.As(_data.ManagerA)));
        Assert.Empty(await _service.ListAsync(TestData.As(_data.ManagerB)));
    }

    public void Dispose() => _data.Dispose();
}
