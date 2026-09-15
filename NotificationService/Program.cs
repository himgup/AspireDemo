using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
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
app.MapPost("/notify", async (HttpContext ctx) =>
{
    using var reader = new StreamReader(ctx.Request.Body);
    var body = await reader.ReadToEndAsync();
    Console.WriteLine($"[notification] {DateTime.UtcNow:O} {body}");
    return Results.Ok(new { received = true });
});

app.Run();
