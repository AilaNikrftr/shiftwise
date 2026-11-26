# ShiftWise

[![CI](https://github.com/AilaNikrftr/shiftwise/actions/workflows/ci.yml/badge.svg)](https://github.com/AilaNikrftr/shiftwise/actions/workflows/ci.yml)

Shift scheduling for small businesses like cafés, clinics and shops. Managers build the weekly schedule, employees ask coworkers to take shifts they can't work, managers approve the swap, and the hours report exports straight to CSV for payroll.

Each business only ever sees its own staff, shifts and requests, even though they all share one database.

**Stack:** C# / ASP.NET Core 10 Web API · Entity Framework Core · SQL Server · Azure Blob Storage (Azurite locally) · Angular 21 · xUnit · Vitest · GitHub Actions

## Features

- **Accounts and roles.** A manager signs up and creates the business, then adds employees with a temporary password. Login uses JWT bearer tokens, and the role is checked on every request.
- **Weekly schedule.** A grid of staff × days. Managers click to add, edit or delete shifts. The API rejects shifts that overlap for the same person, end before they start, or run longer than 16 hours. Overnight shifts work.
- **Shift swaps.** An employee picks one of their upcoming shifts and a coworker. The manager approves or rejects it. On approval the shift moves to the coworker, unless that person has been booked into an overlapping shift since the request was made.
- **Hours report.** Total scheduled hours per person for any date range, with CSV export.
- **Payroll archive (optional).** Managers can save a copy of each payroll CSV to Azure Blob Storage, list past exports and get a short-lived, read-only download link for any of them.
- **Demo data.** On first run in Development, the API seeds a café with a manager, four staff and a week of shifts.

## Running it locally

You need the [.NET 10 SDK](https://dotnet.microsoft.com/download), Node 22+, and Docker.

```bash
# 1. Start SQL Server
docker compose up -d

# 2. Start the API (http://localhost:5080). It creates the database and demo data on first run.
cd src/ShiftWise.Api
dotnet run

# 3. In another terminal, start the Angular app (http://localhost:4200)
cd client
npm install
npm start
```

The Angular dev server proxies `/api` to the API, so there's no CORS setup.

**Demo logins** (password `shiftwise123`):

| Role     | Email                  |
|----------|------------------------|
| Manager  | manager@harbour.test   |
| Employee | sam@harbour.test       |
| Employee | priya@harbour.test     |

The sign-in page has buttons that fill these in.

### Tests

```bash
npx azurite-blob --location .azurite &   # Blob Storage emulator for the archive tests
dotnet test ShiftWise.slnx               # API
cd client && npm test                    # client
```

CI starts Azurite and runs both on every push and pull request.

### Payroll archive (Azure Blob Storage)

Off by default. To try it locally, start [Azurite](https://learn.microsoft.com/azure/storage/common/storage-use-azurite) and point the API at it:

```bash
npx azurite-blob --location .azurite
export PayrollArchive__ConnectionString="UseDevelopmentStorage=true"
cd src/ShiftWise.Api && dotnet run
```

Any Azure Storage connection string works the same way. Without one, the archive routes return `503` and the rest of the app runs as before.

## API

All routes except register and login need `Authorization: Bearer <token>`.

| Method | Route                          | Who       | What |
|--------|--------------------------------|-----------|------|
| POST   | `/api/auth/register`           | anyone    | Create a business and its manager |
| POST   | `/api/auth/login`              | anyone    | Get a token |
| GET    | `/api/auth/me`                 | signed in | Current user |
| GET    | `/api/employees`               | signed in | Everyone at your business |
| POST   | `/api/employees`               | manager   | Add an employee |
| GET    | `/api/shifts?from=&to=`        | signed in | Shifts in a range (defaults to this week) |
| POST   | `/api/shifts`                  | manager   | Create a shift |
| PUT    | `/api/shifts/{id}`             | manager   | Update a shift |
| DELETE | `/api/shifts/{id}`             | manager   | Delete a shift |
| GET    | `/api/swaps`                   | signed in | Managers see all; employees see their own |
| POST   | `/api/swaps`                   | signed in | Ask a coworker to take your shift |
| POST   | `/api/swaps/{id}/approve`      | manager   | Approve and reassign the shift |
| POST   | `/api/swaps/{id}/reject`       | manager   | Reject |
| POST   | `/api/swaps/{id}/cancel`       | requester | Withdraw your request |
| GET    | `/api/reports/hours?from=&to=` | manager   | Hours per employee |
| GET    | `/api/reports/hours/export`    | manager   | Same, as CSV |
| POST   | `/api/reports/hours/archive?from=&to=` | manager | Save the CSV to Blob Storage, get a download link |
| GET    | `/api/reports/archive`         | manager   | Past exports for your business, newest first |
| GET    | `/api/reports/archive/{name}`  | manager   | New download link for one export |

Errors come back as `ProblemDetails` JSON with the reason in `title`. The OpenAPI document is at `/openapi/v1.json` in Development.

## How it's put together

```
src/ShiftWise.Api
  Controllers/   thin: read the user from the token, call a service, return the result
  Services/      business rules (overlaps, who can do what, swap flow, report)
  Domain/        entities: Company, User, Shift, SwapRequest
  Data/          EF Core DbContext, shared queries, demo seeder
  Auth/          JWT creation, claim names, CurrentUser
  Common/        AppException and the handler that turns it into ProblemDetails
  Storage/       payroll archive on Azure Blob Storage
tests/ShiftWise.Api.Tests   xUnit tests against the services with EF's in-memory provider,
                            plus archive tests against Azurite
client/                     Angular app (standalone components, signals)
```

A few decisions worth explaining:

- **Keeping businesses separate.** Every table has a `CompanyId`, and every query filters on the company from the caller's token. A record from another business returns 404 rather than 403, so the API doesn't confirm it exists.
- **Rules live in services, not controllers.** Services take a `CurrentUser` record instead of `HttpContext`, so the tests call them directly without spinning up a web server. Role checks happen in both places: `[Authorize(Roles = ...)]` on the controller and `RequireManager()` in the service.
- **Checking the swap at approval time.** The coworker's availability is checked again when the manager approves, not just when the request is made, because the schedule can change in between.
- **Payroll archive layout.** One private container, one folder per business (`company-{id}/`). File names end in a UTC timestamp and uploads use `If-None-Match: *`, so re-running a month never overwrites the copy payroll already has. Period, row count and archive time go in blob metadata, so listing doesn't download any files. Links are service SAS URLs scoped to one blob, read-only, and valid for 15 minutes. Blob names from the URL are checked against the caller's folder, so one business can't request another's file.
- **Time handling.** Times are stored as the business's local time with no offset. That's simple and correct for a single-location business, but it would need to change for one spanning time zones.

## Not done yet

- **Migrations.** The API uses `EnsureCreated()` so first run is one command. The next step is `dotnet ef migrations add InitialCreate` and switching to `Database.Migrate()`.
- **Live updates** with SignalR, so a manager sees new swap requests without refreshing.
- **Deployment** to Azure App Service + Azure SQL. The archive is only tested against Azurite so far. On a real account it would use a managed identity and user-delegation SAS instead of an account key.
- **Password reset**, and letting employees change their temporary password.
