using Microsoft.EntityFrameworkCore;
using ShiftWise.Api.Auth;
using ShiftWise.Api.Data;
using ShiftWise.Api.Domain;

namespace ShiftWise.Api.Tests;

public class FixedClock(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
    public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
}

// Two companies with a manager and two employees each, in a fresh in-memory database.
// Company B exists to check that nothing leaks between companies.
public class TestData : IDisposable
{
    public static readonly DateTime Today = new(2030, 3, 4); // a Monday, safely in the future

    public AppDbContext Db { get; }
    public FixedClock Clock { get; } = new(new DateTimeOffset(Today.AddHours(8), TimeSpan.Zero));

    public User ManagerA { get; private set; } = null!;
    public User Alice { get; private set; } = null!;
    public User Bob { get; private set; } = null!;
    public User ManagerB { get; private set; } = null!;
    public User Carol { get; private set; } = null!;

    public TestData()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        Db = new AppDbContext(options);
        Seed();
    }

    private void Seed()
    {
        var companyA = new Company { Name = "Company A" };
        var companyB = new Company { Name = "Company B" };

        ManagerA = NewUser(companyA, "Manager A", UserRole.Manager);
        Alice = NewUser(companyA, "Alice", UserRole.Employee);
        Bob = NewUser(companyA, "Bob", UserRole.Employee);
        ManagerB = NewUser(companyB, "Manager B", UserRole.Manager);
        Carol = NewUser(companyB, "Carol", UserRole.Employee);

        Db.Users.AddRange(ManagerA, Alice, Bob, ManagerB, Carol);
        Db.SaveChanges();
    }

    private static User NewUser(Company company, string name, UserRole role) => new()
    {
        Company = company,
        FullName = name,
        Email = name.Replace(" ", ".").ToLowerInvariant() + "@test.dev",
        PasswordHash = "not-used",
        Role = role
    };

    public static CurrentUser As(User user) => new(user.Id, user.CompanyId, user.Role);

    public Shift AddShift(User employee, DateTime start, DateTime end, string position = "Barista")
    {
        var shift = new Shift
        {
            CompanyId = employee.CompanyId,
            EmployeeId = employee.Id,
            StartsAt = start,
            EndsAt = end,
            Position = position
        };

        Db.Shifts.Add(shift);
        Db.SaveChanges();
        return shift;
    }

    public void Dispose() => Db.Dispose();
}
