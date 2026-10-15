using Microsoft.EntityFrameworkCore;
using ShiftWise.Api.Auth;
using ShiftWise.Api.Common;
using ShiftWise.Api.Contracts;
using ShiftWise.Api.Data;
using ShiftWise.Api.Domain;

namespace ShiftWise.Api.Services;

public class SwapService(AppDbContext db, TimeProvider clock)
{
    // Managers see every request in the company. Employees only see
    // requests they made or ones where they'd be picking up the shift.
    public async Task<List<SwapDto>> ListAsync(CurrentUser me)
    {
        var query = WithDetails().Where(r => r.CompanyId == me.CompanyId);

        if (!me.IsManager)
        {
            query = query.Where(r => r.RequestedById == me.UserId || r.TargetEmployeeId == me.UserId);
        }

        var requests = await query.OrderByDescending(r => r.CreatedAt).ToListAsync();
        return requests.Select(ToDto).ToList();
    }

    public async Task<SwapDto> CreateAsync(CurrentUser me, CreateSwapRequest request)
    {
        var shift = await db.Shifts
            .FirstOrDefaultAsync(s => s.Id == request.ShiftId && s.CompanyId == me.CompanyId)
            ?? throw AppException.NotFound("Shift not found.");

        if (shift.EmployeeId != me.UserId)
        {
            throw AppException.Forbidden("You can only request a swap for your own shift.");
        }

        if (shift.StartsAt <= Now())
        {
            throw AppException.BadRequest("That shift has already started.");
        }

        if (request.TargetEmployeeId == me.UserId)
        {
            throw AppException.BadRequest("Pick a coworker to take the shift.");
        }

        var targetExists = await db.Users.AnyAsync(u =>
            u.Id == request.TargetEmployeeId && u.CompanyId == me.CompanyId && u.IsActive);
        if (!targetExists)
        {
            throw AppException.NotFound("Coworker not found.");
        }

        var alreadyPending = await db.SwapRequests.AnyAsync(r =>
            r.ShiftId == shift.Id && r.Status == SwapStatus.Pending);
        if (alreadyPending)
        {
            throw AppException.Conflict("There's already a pending swap request for this shift.");
        }

        var swap = new SwapRequest
        {
            CompanyId = me.CompanyId,
            ShiftId = shift.Id,
            RequestedById = me.UserId,
            TargetEmployeeId = request.TargetEmployeeId,
            Note = string.IsNullOrWhiteSpace(request.Note) ? null : request.Note.Trim(),
            CreatedAt = Now()
        };

        db.SwapRequests.Add(swap);
        await db.SaveChangesAsync();

        return ToDto(await LoadAsync(me, swap.Id));
    }

    public async Task<SwapDto> ApproveAsync(CurrentUser me, int swapId)
    {
        me.RequireManager();
        var swap = await LoadPendingAsync(me, swapId);
        var shift = swap.Shift!;

        // The original owner might have been given another shift since the
        // request was made, so check the person taking it over right now.
        var busy = await db.EmployeeIsBusyAsync(
            me.CompanyId, swap.TargetEmployeeId, shift.StartsAt, shift.EndsAt, ignoreShiftId: shift.Id);
        if (busy)
        {
            throw AppException.Conflict($"{swap.TargetEmployee!.FullName} is already working during that shift.");
        }

        shift.EmployeeId = swap.TargetEmployeeId;
        shift.Employee = swap.TargetEmployee;
        Resolve(swap, me, SwapStatus.Approved);

        await db.SaveChangesAsync();
        return ToDto(swap);
    }

    public async Task<SwapDto> RejectAsync(CurrentUser me, int swapId)
    {
        me.RequireManager();
        var swap = await LoadPendingAsync(me, swapId);

        Resolve(swap, me, SwapStatus.Rejected);
        await db.SaveChangesAsync();
        return ToDto(swap);
    }

    public async Task<SwapDto> CancelAsync(CurrentUser me, int swapId)
    {
        var swap = await LoadPendingAsync(me, swapId);

        if (swap.RequestedById != me.UserId)
        {
            throw AppException.Forbidden("Only the person who asked for the swap can cancel it.");
        }

        Resolve(swap, me, SwapStatus.Cancelled);
        await db.SaveChangesAsync();
        return ToDto(swap);
    }

    private void Resolve(SwapRequest swap, CurrentUser me, SwapStatus status)
    {
        swap.Status = status;
        swap.ResolvedAt = Now();
        swap.ResolvedById = me.UserId;
    }

    private async Task<SwapRequest> LoadPendingAsync(CurrentUser me, int swapId)
    {
        var swap = await LoadAsync(me, swapId);

        if (swap.Status != SwapStatus.Pending)
        {
            throw AppException.Conflict($"This request was already {swap.Status.ToString().ToLowerInvariant()}.");
        }

        return swap;
    }

    private async Task<SwapRequest> LoadAsync(CurrentUser me, int swapId)
    {
        return await WithDetails().FirstOrDefaultAsync(r => r.Id == swapId && r.CompanyId == me.CompanyId)
            ?? throw AppException.NotFound("Swap request not found.");
    }

    private IQueryable<SwapRequest> WithDetails() => db.SwapRequests
        .Include(r => r.Shift)
        .Include(r => r.RequestedBy)
        .Include(r => r.TargetEmployee);

    private DateTime Now() => clock.GetLocalNow().DateTime;

    private static SwapDto ToDto(SwapRequest r) => new(
        r.Id,
        r.ShiftId,
        r.Shift!.StartsAt,
        r.Shift.EndsAt,
        r.Shift.Position,
        r.RequestedById,
        r.RequestedBy!.FullName,
        r.TargetEmployeeId,
        r.TargetEmployee!.FullName,
        r.Status,
        r.Note,
        r.CreatedAt);
}
