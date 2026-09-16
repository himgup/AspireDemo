using OpenTelemetry.Logs;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddOtlpExporter())
    .WithLogging(logging => logging
        .AddOtlpExporter());

var app = builder.Build();

app.MapGet("/", () => "notification service up");

app.MapGet("/health", () => Results.Ok(new
{
    status = "Healthy",
    service = "notifications",
    timestamp = DateTimeOffset.UtcNow
}));

// Mock webhook: logs whatever it receives instead of sending real email/SMS.
app.MapPost("/notify", (NotificationRequest request, ILogger<Program> logger) =>
{
    logger.LogInformation("Order notification received: {OrderId} status={Status}",
        request.OrderId, request.Status);
    return Results.Ok(new { received = true });
});

app.Run();

record NotificationRequest(int OrderId, string Status);
