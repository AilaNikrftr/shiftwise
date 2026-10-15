using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ShiftWise.Api.Auth;
using ShiftWise.Api.Common;
using ShiftWise.Api.Contracts;
using ShiftWise.Api.Data;
using ShiftWise.Api.Domain;

namespace ShiftWise.Api.Services;

public class AuthService(AppDbContext db, TokenService tokens, IPasswordHasher<User> hasher)
{
    public const int MinPasswordLength = 8;

    // Creates a new company and its first manager in one go.
    public async Task<AuthResponse> RegisterCompanyAsync(RegisterCompanyRequest request)
    {
        var companyName = request.CompanyName?.Trim() ?? "";
        var fullName = request.FullName?.Trim() ?? "";
        var email = NormalizeEmail(request.Email);

        if (companyName.Length == 0 || fullName.Length == 0 || email.Length == 0)
        {
            throw AppException.BadRequest("Company name, your name and email are all required.");
        }

        ValidatePassword(request.Password);

        if (await db.Users.AnyAsync(u => u.Email == email))
        {
            throw AppException.Conflict("An account with that email already exists.");
        }

        var company = new Company { Name = companyName };
        var manager = new User
        {
            Company = company,
            FullName = fullName,
            Email = email,
            Role = UserRole.Manager
        };
        manager.PasswordHash = hasher.HashPassword(manager, request.Password);

        db.Users.Add(manager);
        await db.SaveChangesAsync();

        return new AuthResponse(tokens.CreateToken(manager), ToDto(manager));
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        var email = NormalizeEmail(request.Email);
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email && u.IsActive);

        // Same message for "no such user" and "wrong password" so the response
        // doesn't reveal which emails are registered.
        if (user is null)
        {
            throw AppException.Unauthorized("Invalid email or password.");
        }

        var result = hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password ?? "");
        if (result == PasswordVerificationResult.Failed)
        {
            throw AppException.Unauthorized("Invalid email or password.");
        }

        if (result == PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash = hasher.HashPassword(user, request.Password!);
            await db.SaveChangesAsync();
        }

        return new AuthResponse(tokens.CreateToken(user), ToDto(user));
    }

    public async Task<UserDto> GetMeAsync(CurrentUser me)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Id == me.UserId && u.CompanyId == me.CompanyId)
            ?? throw AppException.NotFound("User not found.");

        return ToDto(user);
    }

    public static string NormalizeEmail(string? email) => (email ?? "").Trim().ToLowerInvariant();

    public static void ValidatePassword(string? password)
    {
        if (string.IsNullOrEmpty(password) || password.Length < MinPasswordLength)
        {
            throw AppException.BadRequest($"Password must be at least {MinPasswordLength} characters.");
        }
    }

    public static UserDto ToDto(User user) => new(user.Id, user.FullName, user.Email, user.Role);
}
