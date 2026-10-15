using System.Security.Claims;
using ShiftWise.Api.Common;
using ShiftWise.Api.Domain;

namespace ShiftWise.Api.Auth;

// Services take this instead of HttpContext so they're easy to test.
public record CurrentUser(int UserId, int CompanyId, UserRole Role)
{
    public bool IsManager => Role == UserRole.Manager;

    public void RequireManager()
    {
        if (!IsManager)
        {
            throw AppException.Forbidden("Only managers can do that.");
        }
    }
}

public static class ClaimsPrincipalExtensions
{
    public static CurrentUser ToCurrentUser(this ClaimsPrincipal principal)
    {
        var userId = principal.FindFirst(ClaimNames.UserId)?.Value;
        var companyId = principal.FindFirst(ClaimNames.CompanyId)?.Value;
        var role = principal.FindFirst(ClaimNames.Role)?.Value;

        if (userId is null || companyId is null || !Enum.TryParse<UserRole>(role, out var parsedRole))
        {
            throw AppException.Unauthorized("Token is missing required claims.");
        }

        return new CurrentUser(int.Parse(userId), int.Parse(companyId), parsedRole);
    }
}
