using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ShiftWise.Api.Auth;
using ShiftWise.Api.Contracts;
using ShiftWise.Api.Domain;
using ShiftWise.Api.Services;

namespace ShiftWise.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/employees")]
public class EmployeesController(EmployeeService employees) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<UserDto>>> List()
    {
        return await employees.ListAsync(User.ToCurrentUser());
    }

    [Authorize(Roles = nameof(UserRole.Manager))]
    [HttpPost]
    public async Task<ActionResult<UserDto>> Create(CreateEmployeeRequest request)
    {
        var created = await employees.CreateAsync(User.ToCurrentUser(), request);
        return StatusCode(StatusCodes.Status201Created, created);
    }
}
