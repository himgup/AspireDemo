# Aspire Order Processing Demo

Companion runnable scaffold for the "Orchestration Is All You Need" talk. Matches slide 18's
architecture: **Angular UI → .NET Order API → PostgreSQL/Redis → RabbitMQ → Spring Boot Order Worker → Notification Service**,
orchestrated locally by a **.NET Aspire AppHost (C#)**.

## Layout

- `AppHost/` — Aspire AppHost (C#). Declares Postgres, Redis, RabbitMQ, and wires all services together.
- `OrderApi/` — .NET minimal API. `POST /orders` creates an order (Postgres) and publishes to RabbitMQ. `GET /orders/{id}` reads through Redis cache. `PATCH /orders/{id}/status` updates status.
- `NotificationService/` — .NET minimal API. `POST /notify` is a mock webhook that logs the payload — no real email/SMS.
- `OrderWorker/` — Spring Boot (Java) service. Consumes the `orders` queue from RabbitMQ, simulates processing, calls back into Order API to update status, then calls the Notification Service.
- `frontend/` — Angular app. One page: a form to place an order plus a list showing live status.

## Prerequisites

- .NET SDK 10.0.302. This sample pins the AppHost SDK through the repository-local `global.json` and uses Aspire 13.5.3.
- Docker Desktop (or another OCI-compatible container runtime) running — Aspire starts Postgres/Redis/RabbitMQ containers automatically
- Java 21 + Maven (only needed for local IDE work; the worker builds inside Docker, so a local JDK is optional)
- Node.js 20+ for the Angular frontend
- Aspire CLI 13.5.3 for the AI coding agent walkthrough

## Run it

```powershell
aspire start `
	--apphost .\AppHost\AppHost.csproj `
	--non-interactive `
	--format Json
```

This starts every enabled resource and opens the Aspire Dashboard. First run will build the `OrderWorker` Docker image and install the frontend packages — expect it to take a few minutes the first time.

Place an order from the Angular UI (or `curl`), then watch it move through Pending → Processing → Shipped in the dashboard's logs/traces, and see the mock notification logged by `NotificationService`.

For the complete presentation flow, including what to show in the dashboard and how to demonstrate each Aspire feature, see [DEMO-GUIDE.md](DEMO-GUIDE.md).

The repository also includes official Aspire workflow skills under `.agents/skills/`. See the [AI coding agent walkthrough](DEMO-GUIDE.md#ai-coding-agent-demo-without-mcp) for the Skills + runtime inspection demo. MCP is intentionally left optional.

This project targets .NET 10 through the repository-local `global.json` and Aspire packages `13.5.3`. The SDK and package changes are scoped to this repository; they do not change the SDK selection or dependencies of other projects. `dotnet run --project .\AppHost\AppHost.csproj` remains available when you want to launch the AppHost directly.

## Demo controls

The enabled resources and frontend port can be changed in `AppHost/appsettings.json`:

```json
{
	"Demo": {
		"FrontendEnabled": true,
		"NotificationsEnabled": true,
		"WorkerEnabled": true,
		"FrontendPort": 4200
	}
}
```

## Notes / what's intentionally thin

- Order data model is minimal: `id, item, quantity, status`.
- `NotificationService` only logs — swap in a real email/SMS provider later.
- No auth, no retries/dead-lettering, no tests — this is a demo scaffold, not production code.
