using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ShiftWise.Api.Auth;
using ShiftWise.Api.Common;
using ShiftWise.Api.Contracts;
using ShiftWise.Api.Data;
using ShiftWise.Api.Domain;

namespace ShiftWise.Api.Services;

public class EmployeeService(AppDbContext db, IPasswordHasher<User> hasher)
{
    public async Task<List<UserDto>> ListAsync(CurrentUser me)
    {
        var users = await db.Users
            .Where(u => u.CompanyId == me.CompanyId && u.IsActive)
            .OrderBy(u => u.FullName)
            .ToListAsync();

        return users.Select(AuthService.ToDto).ToList();
    }

    public async Task<UserDto> CreateAsync(CurrentUser me, CreateEmployeeRequest request)
    {
        me.RequireManager();

        var fullName = request.FullName?.Trim() ?? "";
        var email = AuthService.NormalizeEmail(request.Email);

        if (fullName.Length == 0 || email.Length == 0)
        {
            throw AppException.BadRequest("Name and email are required.");
        }

        AuthService.ValidatePassword(request.TemporaryPassword);

        if (await db.Users.AnyAsync(u => u.Email == email))
        {
            throw AppException.Conflict("An account with that email already exists.");
        }

        var employee = new User
        {
            CompanyId = me.CompanyId,
            FullName = fullName,
            Email = email,
            Role = UserRole.Employee
        };
        employee.PasswordHash = hasher.HashPassword(employee, request.TemporaryPassword);

        db.Users.Add(employee);
        await db.SaveChangesAsync();

        return AuthService.ToDto(employee);
    }
}
