# Aspire Order Processing Demo

A short presenter guide for showing how .NET Aspire coordinates a small distributed application.

## What the audience will see

The demo has one user action and several cooperating resources:

```text
Angular UI
   |
   v
Order API ---- PostgreSQL
   |           Redis
   |
   v
RabbitMQ ---> Spring Boot Order Worker ---> Notification Service
```

The user places an order once. The order then moves through this lifecycle:

```text
Pending -> Processing -> Shipped
```

The Aspire Dashboard shows the resources, their health, endpoints, logs, traces, and startup dependencies.

## Prerequisites

Install or start these before the presentation:

- .NET SDK 10.0.302, selected by this repository's `global.json`
- Docker Desktop with the Linux container engine running
- Node.js 20 or newer
- Aspire CLI 13.5 or newer
- Internet access for the first NuGet, npm, and Docker image downloads

Java and Maven are optional for this demo. The Order Worker is built inside its Docker image by Aspire.

Check the important tools:

```powershell
dotnet --version
docker info
node --version
npm --version
aspire --version
```

The repository contains `global.json`, so this AppHost selects .NET SDK `10.0.302`. This setting is local to the repository and does not change the SDK used by other projects.

## Start the demo

Open PowerShell at the repository root:

```powershell
cd D:\Tech Talk\aspire-demo
aspire start `
  --apphost .\AppHost\AppHost.csproj `
  --non-interactive `
  --format Json
```

The first start can take a few minutes. Aspire may need to:

- restore NuGet packages
- install frontend npm packages
- build the Java worker Docker image
- download PostgreSQL, Redis, and RabbitMQ images
- create the PostgreSQL data volume

Open the Aspire Dashboard URL printed in the terminal. The exact ports are controlled by Aspire and may differ between machines.

The frontend normally appears as the `frontend` resource. Open its URL from the dashboard. The default frontend port is `http://localhost:4200`.

## AI coding agent demo without MCP

This repository includes the official Aspire workflow skills in `.agents/skills/`. They teach an AI coding agent how to use Aspire; they do not run the application or make the agent autonomous.

The setup uses the official Aspire CLI and the CLI's runtime commands. MCP is intentionally not configured for this demo.

> **Version note:** This demo targets .NET 10 and Aspire packages `13.5.3`. The SDK pin, AppHost SDK, and hosting packages are all stored in this repository, so upgrading this project does not change other projects on the machine.

```text
AI Agent -> Aspire Skills -> Aspire CLI -> Running Aspire application
                                      -> resources, health, logs, traces
```

The responsibilities stay separate:

- **AI Agent:** reasons about evidence, chooses what to investigate, proposes changes, and verifies results.
- **Aspire Skills:** give the agent instructions for AppHost lifecycle, monitoring, deployment, and project workflows.
- **Aspire CLI:** gives the agent command-line access to resource state and logs.
- **Aspire:** runs the application and provides the runtime context.
- **MCP:** an optional standardized tool interface; it is not required for this walkthrough.

### One-time setup

The Aspire CLI is installed globally through npm in this environment:

```powershell
npm install -g @microsoft/aspire-cli
```

The official workflow skills were initialized with:

```powershell
aspire agent init `
  --workspace-root . `
  --non-interactive `
  --skills 'aspire,aspire-init,aspire-orchestration,aspire-monitoring,aspire-deployment,aspireify' `
  --skill-locations standard
```

The resulting workspace skills are under `.agents/skills/`. The selected workflow skills are `aspire`, `aspire-init`, `aspire-orchestration`, `aspire-monitoring`, `aspire-deployment`, and `aspireify`.

The command may offer optional companion tools. They are not needed for this demo. Do not enable the Aspire MCP server for the no-MCP walkthrough.

### Start and inspect this demo

Use the CLI-managed workflow for the current repository:

```powershell
aspire start `
  --apphost .\AppHost\AppHost.csproj `
  --non-interactive `
  --format Json
```

The Aspire Dashboard and CLI are the supported runtime inspection surfaces for this Aspire 13 demo. An AI agent can investigate by reading resource state, application logs, Docker container logs, and source files. For example:

```powershell
docker ps
docker logs --tail 120 <resource-container-name>
```

### CLI inspection workflow

After the AppHost is running, an agent-led terminal workflow can use:

```powershell
aspire start `
  --apphost .\AppHost\AppHost.csproj `
  --non-interactive `
  --format Json

aspire describe `
  --apphost .\AppHost\AppHost.csproj `
  --format Json

aspire wait orderapi `
  --apphost .\AppHost\AppHost.csproj `
  --status healthy `
  --timeout 120 `
  --non-interactive

aspire logs orderapi `
  --apphost .\AppHost\AppHost.csproj `
  --tail 100 `
  --format Json
```

Use one startup method at a time. If `dotnet run` is already running the AppHost, do not also run `aspire start` against the same AppHost.

### Live AI investigation

For the failure scenario, temporarily change `AppHost/appsettings.json`:

```json
{
  "Demo": {
    "FrontendEnabled": true,
    "NotificationsEnabled": true,
    "WorkerEnabled": false,
    "FrontendPort": 4200
  }
}
```

Restart the AppHost, create an order from the frontend, and ask the coding agent:

```text
Start by describing the Aspire resources and their current health. Then investigate why the newest order is stuck in Pending. Inspect resource status and recent logs before answering. Do not modify files or restart resources yet. Explain the evidence and your most likely cause.
```

The expected diagnosis is that the Order API created the order and RabbitMQ is available, but the Order Worker is disabled, so no consumer changes the order from `Pending` to `Processing`.

Useful follow-up prompts:

```text
Show the current Aspire resource status as JSON and identify any resource that is down or not running.
```

```text
Read the recent Order API and RabbitMQ-related logs. Is there evidence that the order was created and published but not consumed?
```

```text
Suggest the smallest configuration change that would restore order processing. Ask for approval before changing any file or resource.
```

Restore `WorkerEnabled` to `true`, restart the AppHost, and ask:

```text
Verify the fix by checking the worker and API resource states, then place or inspect a test order and confirm the lifecycle reaches Shipped. Report the evidence used.
```

The audience should see:

```text
Understand -> Observe -> Diagnose -> Change -> Run/Test -> Verify
```

This is an AI-assisted development workflow, not autonomous debugging. The agent reasons over Aspire evidence and may suggest an action; the developer controls changes and confirms the result.

### Why MCP is not in this demo

Aspire Skills plus the Aspire CLI already provide enough runtime access for this presentation. MCP would expose similar Aspire capabilities as standardized tools that an MCP-capable client can discover and call directly.

The distinction for the slide is:

```text
Skills = instructions for the agent
CLI = command-line runtime access
MCP = optional standardized tool interface
Aspire = running application and observability data
AI Agent = reasoning and action
```

Do not claim that this repository has an Aspire MCP server configured. MCP can be added later through the Aspire agent setup flow if the presentation specifically needs the direct tool-calling experience.

## Five-minute presentation script

### 1. Start with the AppHost

Open `AppHost/Program.cs` and point out:

- `DistributedApplication.CreateBuilder(args)` creates the application model.
- PostgreSQL, Redis, and RabbitMQ are declared once.
- `AddProject` starts the .NET services.
- `AddDockerfile` starts the Java worker without requiring Java on the presenter laptop.
- `AddViteApp` starts the Angular frontend through the JavaScript hosting integration.
- `WithReference` and `WithEnvironment` connect resources.
- `WaitFor` describes startup order.
- `WithHttpHealthCheck` and `WithOtlpExporter` make health and telemetry visible.

Suggested sentence:

> This file is the local system map. It tells Aspire what exists, what depends on what, how resources connect, and which endpoints should be shown to us.

### 2. Open the dashboard

Show the resource list and point out:

- `postgres`
- `cache`
- `rabbitmq`
- `orderapi`
- `notifications`
- `orderworker`
- `frontend`

Show that the dashboard provides one view for a system made from .NET, Java, Angular, databases, cache, and messaging.

### 3. Show health and endpoints

Open the `orderapi` resource and show:

- its healthy state
- the HTTP endpoint
- the `/health` check
- the logs and traces links

Then open the frontend resource and show that its URL was supplied by the AppHost.

Suggested sentence:

> The developer does not need to remember every port. Aspire publishes the current endpoints in the dashboard.

### 4. Place an order

In the Angular page:

1. Enter an item, for example `stainless steel valve`.
2. Enter quantity `3`.
3. Select **Send through the system**.
4. Watch the order appear as `Pending`.
5. Wait for the worker to change it to `Processing` and then `Shipped`.

The page also shows:

- whether the Order API is healthy
- the API endpoint injected by the AppHost
- the connected API/database/cache/queue/worker flow
- the last successful health refresh

### 5. Follow the request in the dashboard

Use the dashboard logs and traces to show the path:

1. The Angular frontend calls `POST /orders`.
2. Order API stores the order in PostgreSQL.
3. Order API writes a cache entry to Redis.
4. Order API publishes an `orders` message to RabbitMQ.
5. The Spring Boot worker consumes the message.
6. The worker calls `PATCH /orders/{id}/status` with `Processing`.
7. The worker waits briefly to simulate work.
8. The worker calls the API again with `Shipped`.
9. The Notification Service logs both status notifications.

Suggested sentence:

> One button click becomes a distributed workflow, but Aspire lets us start and inspect the whole workflow from one place.

## The ten Aspire features demonstrated

### 1. One application model

The AppHost describes the complete local system instead of requiring separate startup instructions for every service.

**Show:** `AppHost/Program.cs` and the dashboard resource list.

### 2. Mixed resource types

This sample combines .NET projects, a Dockerfile-built Java service, an Angular NPM app, and containerized infrastructure.

**Show:** `AddProject`, `AddDockerfile`, `AddViteApp`, `AddPostgres`, `AddRedis`, and `AddRabbitMQ`.

### 3. Local infrastructure

PostgreSQL, Redis, and RabbitMQ run locally in containers. PostgreSQL uses a data volume, so the local database can survive a restart.

**Show:** the infrastructure resources in the dashboard and the PostgreSQL volume behavior.

### 4. Dependency ordering

The API waits for PostgreSQL, Redis, and RabbitMQ. The worker waits for RabbitMQ and the API. The frontend waits for the API.

**Show:** the `WaitFor(...)` calls in `AppHost/Program.cs` and the startup state in the dashboard.

### 5. Configuration and references

The AppHost supplies connection strings, endpoint URLs, and the notification URL to the resources that need them.

**Show:** `WithReference(...)`, `WithEnvironment(...)`, and the frontend endpoint environment variable.

### 6. Health checks and dashboard

The API and notification service expose `/health`, and Aspire monitors those endpoints as resource health checks.

**Show:** the green healthy resource state, then stop or restart a service if you want to demonstrate a health change.

### 7. Logs, traces, and telemetry

The API and notification service emit OpenTelemetry traces. Aspire adds its OTLP exporter so the dashboard can show request activity.

**Show:** the Order API request trace and notification logs after placing an order.

### 8. Local secrets

The AppHost creates a persisted secret parameter named `demo-secret` and passes it to the API and notification service without putting the generated value in source code.

**Show:** the `AddParameter(...)` call and explain that the value is local configuration, not a value committed to Git.

### 9. Flexible resource selection

The AppHost can enable or disable the frontend, worker, and notification service through `AppHost/appsettings.json`.

**Show:** these settings:

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

For a short API-only demonstration, set `FrontendEnabled` to `false`. To show a degraded workflow, set `WorkerEnabled` to `false`, restart Aspire, and explain that orders remain `Pending` because no consumer is processing the RabbitMQ message.

Restore all three values to `true` before the main presentation.

Keep `WorkerEnabled` and `NotificationsEnabled` enabled together for the complete order lifecycle. The worker sends status notifications while it processes an order; if notifications are disabled while the worker remains enabled, the worker falls back to its local default URL and the notification calls will fail.

### 10. Endpoint and port management

The frontend port is configured through the AppHost, and the Order API endpoint is injected into the frontend at startup. Aspire publishes the actual endpoint values in the dashboard.

**Show:** the `Live endpoint` card in the Angular page and compare it with the `orderapi` endpoint in the dashboard.

## Optional audience questions

### Why not start each service separately?

You can, but the AppHost keeps startup order, connection settings, health, endpoints, and diagnostics together. That reduces setup differences between team members.

### Is Aspire replacing PostgreSQL, Redis, or RabbitMQ?

No. It starts and connects local instances of those technologies. The application still uses the real client libraries and the real resource behavior.

### Is the Java worker a .NET project?

No. Aspire can still manage it as a Dockerfile-built resource. This is a useful example of coordinating different technologies in one local model.

### Is this production orchestration?

This sample demonstrates local orchestration and developer experience. It is not a production deployment design. Production concerns such as scaling, security, high availability, and deployment policy need separate decisions.

## Useful API checks

With the API endpoint shown in the dashboard, these requests can be used during a presentation:

```powershell
# Health
curl http://localhost:5100/health

# List orders
curl http://localhost:5100/orders

# Create an order
curl -X POST http://localhost:5100/orders `
  -H "Content-Type: application/json" `
  -d '{"item":"demo valve","quantity":2}'
```

The port may change. Use the `orderapi` endpoint from the Aspire Dashboard instead of assuming `5100`.

## Stop and reset

Stop the running AppHost with `Ctrl+C`.

To start again, run:

```powershell
dotnet run --project .\AppHost\AppHost.csproj
```

The PostgreSQL data volume is intentionally persistent. To reset the database completely, stop Aspire and remove the volume from Docker Desktop or use the Docker CLI after confirming the volume name:

```powershell
docker volume ls
```

Do not remove volumes during a live presentation unless you want to demonstrate a clean first run.

## Troubleshooting

### Docker is not running

Start Docker Desktop and verify:

```powershell
docker info
```

### The frontend is not ready yet

The first start may be building the Angular dependencies. Check the `frontend` logs in the Aspire Dashboard and wait for the endpoint to become healthy.

If the browser is completely blank while `http://localhost:4200` returns HTML, open the browser developer console. Angular bootstrap errors such as `NG0908` indicate a client-side startup problem rather than an Aspire resource problem. This demo requires the `zone.js` import in `frontend/src/main.ts`; after changing frontend source, restart the frontend resource or reload the page without using a stale bundle.

### Orders stay Pending

Check the `orderworker` resource and RabbitMQ in the dashboard. If the worker is disabled, set `WorkerEnabled` back to `true` and restart the AppHost.

For this demo, inspect the worker with the Docker container log shown in the dashboard or with `docker logs <orderworker-container-name>`. A worker that is running but cannot parse an order message will also leave orders in `Pending`; the worker log is the authoritative evidence for that case.

### The API is unhealthy

Open the `orderapi` logs. PostgreSQL, Redis, or RabbitMQ may still be starting. The AppHost waits for those dependencies before starting the API.

### NuGet or Docker downloads time out

The first run needs network access. Retry after confirming that Docker Desktop and the NuGet feed are reachable. Later starts should use the local package and image caches.

## Files worth showing

- `AppHost/Program.cs` - the Aspire application model
- `AppHost/appsettings.json` - demo toggles and frontend port
- `OrderApi/Program.cs` - database, cache, queue, health, and telemetry behavior
- `OrderWorker/src/main/java/com/trivium/orderworker/OrderConsumer.java` - the cross-language worker flow
- `frontend/src/app/app.component.html` - the visible showcase flow
- `frontend/src/app/order.service.ts` - the AppHost-injected API endpoint
