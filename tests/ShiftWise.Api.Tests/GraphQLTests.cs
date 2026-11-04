using System.Security.Claims;
using System.Text.Json;
using HotChocolate.Execution;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using ShiftWise.Api.Auth;
using ShiftWise.Api.Domain;
using ShiftWise.Api.GraphQL;
using ShiftWise.Api.Services;

namespace ShiftWise.Api.Tests;

// Runs real GraphQL documents against the same schema Program.cs serves,
// with the in-memory TestData database behind the services.
public class GraphQLTests : IDisposable
{
    private readonly TestData _data = new();
    private readonly ServiceProvider _services;
    private readonly DateTime _tomorrow = TestData.Today.AddDays(1);

    private const string Week = "from: \"2030-03-04T00:00:00Z\", to: \"2030-03-11T00:00:00Z\"";

    public GraphQLTests()
    {
        var services = new ServiceCollection();
        services.AddSingleton(_data.Db);
        services.AddSingleton<TimeProvider>(_data.Clock);
        services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();
        services.AddScoped<EmployeeService>();
        services.AddScoped<ShiftService>();
        services.AddScoped<SwapService>();
        services.AddScoped<ReportService>();
        services.AddLogging();
        services.AddAuthorization();
        services.AddShiftWiseGraphQL();
        _services = services.BuildServiceProvider();
    }

    private static ClaimsPrincipal SignedIn(User user) => new(new ClaimsIdentity(
        [
            new Claim(ClaimNames.UserId, user.Id.ToString()),
            new Claim(ClaimNames.CompanyId, user.CompanyId.ToString()),
            new Claim(ClaimNames.Role, user.Role.ToString()),
            new Claim(ClaimNames.Name, user.FullName)
        ],
        authenticationType: "test",
        nameType: ClaimNames.Name,
        roleType: ClaimNames.Role));

    private async Task<JsonElement> Run(string document, User? user)
    {
        var executor = await _services.GetRequestExecutorAsync();
        await using var scope = _services.CreateAsyncScope();

        var request = OperationRequestBuilder.New()
            .SetDocument(document)
            .SetServices(scope.ServiceProvider);
        if (user is not null)
        {
            request.SetUser(SignedIn(user));
        }

        var result = (IOperationResult)await executor.ExecuteAsync(request.Build());

        // Turn the typed result into the same JSON shape a client would receive.
        var json = new Dictionary<string, object?> { ["data"] = result.Data };
        if (result.Errors is { Count: > 0 } errors)
        {
            json["errors"] = errors
                .Select(e => new { message = e.Message, extensions = new { code = e.Code } })
                .ToList();
        }

        return JsonSerializer.SerializeToElement(json);
    }

    private static string? ErrorCode(JsonElement result) =>
        result.TryGetProperty("errors", out var errors)
            ? errors[0].GetProperty("extensions").GetProperty("code").GetString()
            : null;

    private string CreateShift(User employee, int startHour, int endHour) => $$"""
        mutation {
          createShift(input: {
            employeeId: {{employee.Id}},
            startsAt: "{{_tomorrow.AddHours(startHour):yyyy-MM-ddTHH:mm:ss}}Z",
            endsAt: "{{_tomorrow.AddHours(endHour):yyyy-MM-ddTHH:mm:ss}}Z",
            position: "Barista"
          }) { id employeeName position }
        }
        """;

    [Fact]
    public async Task Shifts_query_only_returns_the_callers_company()
    {
        _data.AddShift(_data.Alice, _tomorrow.AddHours(9), _tomorrow.AddHours(17));
        _data.AddShift(_data.Carol, _tomorrow.AddHours(9), _tomorrow.AddHours(17)); // company B

        var result = await Run($"{{ shifts({Week}) {{ employeeName position }} }}", _data.Bob);

        var shifts = result.GetProperty("data").GetProperty("shifts");
        Assert.Equal(1, shifts.GetArrayLength());
        Assert.Equal("Alice", shifts[0].GetProperty("employeeName").GetString());
    }

    [Fact]
    public async Task Anonymous_requests_are_rejected()
    {
        var result = await Run($"{{ shifts({Week}) {{ id }} }}", user: null);

        Assert.Equal("AUTH_NOT_AUTHENTICATED", ErrorCode(result));
    }

    [Fact]
    public async Task Manager_can_create_a_shift()
    {
        var result = await Run(CreateShift(_data.Alice, 9, 17), _data.ManagerA);

        Assert.Null(ErrorCode(result));
        var shift = result.GetProperty("data").GetProperty("createShift");
        Assert.Equal("Alice", shift.GetProperty("employeeName").GetString());
        Assert.Single(_data.Db.Shifts);
    }

    [Fact]
    public async Task Employees_cannot_create_shifts()
    {
        var result = await Run(CreateShift(_data.Alice, 9, 17), _data.Bob);

        Assert.Equal("AUTH_NOT_AUTHORIZED", ErrorCode(result));
        Assert.Empty(_data.Db.Shifts);
    }

    [Fact]
    public async Task Business_rule_failures_come_back_as_readable_errors()
    {
        _data.AddShift(_data.Alice, _tomorrow.AddHours(9), _tomorrow.AddHours(17));

        var result = await Run(CreateShift(_data.Alice, 16, 20), _data.ManagerA);

        Assert.Equal("CONFLICT", ErrorCode(result));
        Assert.Contains("already has a shift", result.GetProperty("errors")[0].GetProperty("message").GetString());
    }

    [Fact]
    public async Task Hours_report_is_for_managers_only()
    {
        _data.AddShift(_data.Alice, _tomorrow.AddHours(9), _tomorrow.AddHours(17));
        var query = $"{{ hoursReport({Week}) {{ employeeName totalHours }} }}";

        var asManager = await Run(query, _data.ManagerA);
        var asEmployee = await Run(query, _data.Alice);

        var alice = asManager.GetProperty("data").GetProperty("hoursReport").EnumerateArray()
            .Single(r => r.GetProperty("employeeName").GetString() == "Alice");
        Assert.Equal(8, alice.GetProperty("totalHours").GetDouble());
        Assert.Equal("AUTH_NOT_AUTHORIZED", ErrorCode(asEmployee));
    }

    [Fact]
    public async Task Swap_can_be_requested_and_approved_over_graphql()
    {
        var shift = _data.AddShift(_data.Alice, _tomorrow.AddHours(9), _tomorrow.AddHours(17));

        var requested = await Run(
            $"mutation {{ requestSwap(input: {{ shiftId: {shift.Id}, targetEmployeeId: {_data.Bob.Id} }}) {{ id status }} }}",
            _data.Alice);
        Assert.Null(ErrorCode(requested));
        var swapId = requested.GetProperty("data").GetProperty("requestSwap").GetProperty("id").GetInt32();

        var approved = await Run($"mutation {{ approveSwap(id: {swapId}) {{ status targetEmployeeName }} }}", _data.ManagerA);

        Assert.Null(ErrorCode(approved));
        Assert.Equal("APPROVED", approved.GetProperty("data").GetProperty("approveSwap").GetProperty("status").GetString());
        Assert.Equal(_data.Bob.Id, _data.Db.Shifts.Single().EmployeeId);
    }

    public void Dispose()
    {
        _services.Dispose();
        _data.Dispose();
    }
}
