using System.Security.Claims;
using HotChocolate.Authorization;
using ShiftWise.Api.Auth;
using ShiftWise.Api.Contracts;
using ShiftWise.Api.Services;

namespace ShiftWise.Api.GraphQL;

// GraphQL is a second way into the same services the REST controllers use,
// so company scoping and business rules live in one place. The JWT from
// /api/auth/login works here too.
[Authorize]
public class Query
{
    public Task<List<UserDto>> Employees(ClaimsPrincipal user, EmployeeService employees) =>
        employees.ListAsync(user.ToCurrentUser());

    public Task<List<ShiftDto>> Shifts(DateTime from, DateTime to, ClaimsPrincipal user, ShiftService shifts) =>
        shifts.ListAsync(user.ToCurrentUser(), from, to);

    public Task<List<SwapDto>> Swaps(ClaimsPrincipal user, SwapService swaps) =>
        swaps.ListAsync(user.ToCurrentUser());

    [Authorize(Roles = ["Manager"])]
    public Task<List<HoursRow>> HoursReport(DateTime from, DateTime to, ClaimsPrincipal user, ReportService reports) =>
        reports.HoursAsync(user.ToCurrentUser(), from, to);
}
