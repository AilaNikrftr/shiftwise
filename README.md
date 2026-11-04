# ShiftWise

Shift scheduling for small businesses like cafés, clinics and shops. Managers build the weekly schedule, employees ask coworkers to take shifts they can't work, managers approve the swap, and the hours report exports straight to CSV for payroll.

Each business only ever sees its own staff, shifts and requests, even though they all share one database.

**Stack:** C# / ASP.NET Core 10 Web API · GraphQL (Hot Chocolate) · Entity Framework Core · SQL Server · Angular 21 · xUnit · Vitest · GitHub Actions

## Features

- **Accounts and roles.** A manager signs up and creates the business, then adds employees with a temporary password. Login uses JWT bearer tokens, and the role is checked on every request.
- **Weekly schedule.** A grid of staff × days. Managers click to add, edit or delete shifts. The API rejects shifts that overlap for the same person, end before they start, or run longer than 16 hours. Overnight shifts work.
- **Shift swaps.** An employee picks one of their upcoming shifts and a coworker. The manager approves or rejects it. On approval the shift moves to the coworker, unless that person has been booked into an overlapping shift since the request was made.
- **Hours report.** Total scheduled hours per person for any date range, with CSV export.
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
dotnet test ShiftWise.slnx      # API
cd client && npm test           # client
```

CI runs both on every push and pull request.

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

Errors come back as `ProblemDetails` JSON with the reason in `title`. The OpenAPI document is at `/openapi/v1.json` in Development.

## GraphQL

The same features are also available as GraphQL at `POST /graphql` (Hot Chocolate), using the same JWT. Resolvers call the same services as the REST controllers, so company scoping, roles and business rules are enforced in one place, not twice.

```graphql
query Week {
  shifts(from: "2030-03-04T00:00:00Z", to: "2030-03-11T00:00:00Z") {
    id employeeName startsAt endsAt position
  }
  swaps { id status requestedByName targetEmployeeName }
}

mutation {
  createShift(input: { employeeId: 2, startsAt: "2030-03-05T09:00:00Z",
                       endsAt: "2030-03-05T17:00:00Z", position: "Barista" }) {
    id employeeName
  }
}
```

| Field | Who |
|---|---|
| `employees`, `shifts(from, to)`, `swaps` | signed in |
| `hoursReport(from, to)` | manager |
| `createShift`, `updateShift`, `deleteShift`, `approveSwap`, `rejectSwap` | manager |
| `requestSwap` | signed in |

- Authorization uses `[Authorize]` / `[Authorize(Roles = ["Manager"])]`, so a missing token returns `AUTH_NOT_AUTHENTICATED` and an employee calling a manager field gets `AUTH_NOT_AUTHORIZED`.
- Business-rule failures (overlapping shift, shift not found, ...) come back as GraphQL errors with the service's message and a code (`BAD_REQUEST`, `NOT_FOUND`, `CONFLICT`, ...), mapped from `AppException` by an error filter.
- Query depth is capped and exception details are never sent to clients.
- `GraphQLTests` runs real GraphQL documents against the schema, checking company isolation, auth, errors and the full swap flow.

In Development, open `/graphql` in a browser for the Nitro IDE.

## How it's put together

```
src/ShiftWise.Api
  Controllers/   thin: read the user from the token, call a service, return the result
  Services/      business rules (overlaps, who can do what, swap flow, report)
  Domain/        entities: Company, User, Shift, SwapRequest
  Data/          EF Core DbContext, shared queries, demo seeder
  Auth/          JWT creation, claim names, CurrentUser
  Common/        AppException and the handler that turns it into ProblemDetails
tests/ShiftWise.Api.Tests   xUnit tests against the services with EF's in-memory provider
client/                     Angular app (standalone components, signals)
```

A few decisions worth explaining:

- **Keeping businesses separate.** Every table has a `CompanyId`, and every query filters on the company from the caller's token. A record from another business returns 404 rather than 403, so the API doesn't confirm it exists.
- **Rules live in services, not controllers.** Services take a `CurrentUser` record instead of `HttpContext`, so the tests call them directly without spinning up a web server. Role checks happen in both places: `[Authorize(Roles = ...)]` on the controller and `RequireManager()` in the service.
- **Checking the swap at approval time.** The coworker's availability is checked again when the manager approves, not just when the request is made, because the schedule can change in between.
- **Time handling.** Times are stored as the business's local time with no offset. That's simple and correct for a single-location business, but it would need to change for one spanning time zones.

## Not done yet

- **Migrations.** The API uses `EnsureCreated()` so first run is one command. The next step is `dotnet ef migrations add InitialCreate` and switching to `Database.Migrate()`.
- **Live updates** with SignalR, so a manager sees new swap requests without refreshing.
- **Deployment** to Azure App Service + Azure SQL.
- **Password reset**, and letting employees change their temporary password.
