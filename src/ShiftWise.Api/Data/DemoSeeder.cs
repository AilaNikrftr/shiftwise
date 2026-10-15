using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ShiftWise.Api.Domain;

namespace ShiftWise.Api.Data;

// Fills an empty database with one café and a week of shifts so the app
// has something to show on first run. Does nothing if any company exists.
public static class DemoSeeder
{
    public const string DemoPassword = "shiftwise123";

    public static async Task SeedAsync(AppDbContext db, IPasswordHasher<User> hasher, TimeProvider clock)
    {
        if (await db.Companies.AnyAsync())
        {
            return;
        }

        var company = new Company { Name = "Harbour Street Café" };

        var manager = NewUser(company, "Jordan Lee", "manager@harbour.test", UserRole.Manager, hasher);
        var staff = new[]
        {
            NewUser(company, "Sam Patel", "sam@harbour.test", UserRole.Employee, hasher),
            NewUser(company, "Priya Nair", "priya@harbour.test", UserRole.Employee, hasher),
            NewUser(company, "Alex Moreau", "alex@harbour.test", UserRole.Employee, hasher),
            NewUser(company, "Taylor Brooks", "taylor@harbour.test", UserRole.Employee, hasher)
        };

        db.Users.Add(manager);
        db.Users.AddRange(staff);
        await db.SaveChangesAsync();

        var today = clock.GetLocalNow().DateTime.Date;
        var monday = today.AddDays(-(((int)today.DayOfWeek + 6) % 7));

        // (start hour, end hour, position) for the three daily slots.
        var slots = new (int Start, int End, string Position)[]
        {
            (7, 15, "Barista"),
            (11, 19, "Cashier"),
            (15, 22, "Barista")
        };

        var shifts = new List<Shift>();
        for (var day = 0; day < 7; day++)
        {
            for (var slot = 0; slot < slots.Length; slot++)
            {
                // Rotate people through the slots so everyone gets a mix of days.
                var employee = staff[(day + slot) % staff.Length];
                var date = monday.AddDays(day);

                shifts.Add(new Shift
                {
                    CompanyId = company.Id,
                    EmployeeId = employee.Id,
                    StartsAt = date.AddHours(slots[slot].Start),
                    EndsAt = date.AddHours(slots[slot].End),
                    Position = slots[slot].Position
                });
            }
        }

        db.Shifts.AddRange(shifts);
        await db.SaveChangesAsync();
    }

    private static User NewUser(Company company, string name, string email, UserRole role, IPasswordHasher<User> hasher)
    {
        var user = new User { Company = company, FullName = name, Email = email, Role = role };
        user.PasswordHash = hasher.HashPassword(user, DemoPassword);
        return user;
    }
}
