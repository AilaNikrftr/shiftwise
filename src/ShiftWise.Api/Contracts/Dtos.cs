using ShiftWise.Api.Domain;

namespace ShiftWise.Api.Contracts;

// Auth
public record RegisterCompanyRequest(string CompanyName, string FullName, string Email, string Password);
public record LoginRequest(string Email, string Password);
public record AuthResponse(string Token, UserDto User);

// People
public record UserDto(int Id, string FullName, string Email, UserRole Role);
public record CreateEmployeeRequest(string FullName, string Email, string TemporaryPassword);

// Shifts
public record ShiftDto(
    int Id,
    int EmployeeId,
    string EmployeeName,
    DateTime StartsAt,
    DateTime EndsAt,
    string Position,
    string? Notes);

public record SaveShiftRequest(
    int EmployeeId,
    DateTime StartsAt,
    DateTime EndsAt,
    string Position,
    string? Notes);

// Swaps
public record CreateSwapRequest(int ShiftId, int TargetEmployeeId, string? Note);

public record SwapDto(
    int Id,
    int ShiftId,
    DateTime ShiftStartsAt,
    DateTime ShiftEndsAt,
    string Position,
    int RequestedById,
    string RequestedByName,
    int TargetEmployeeId,
    string TargetEmployeeName,
    SwapStatus Status,
    string? Note,
    DateTime CreatedAt);

// Reports
public record HoursRow(int EmployeeId, string EmployeeName, int ShiftCount, double TotalHours);
