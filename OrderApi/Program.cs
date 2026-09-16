using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using OrderApi.Data;
using OrderApi.Models;
using RabbitMQ.Client;
using StackExchange.Redis;
using OrderModel = OrderApi.Models.Order;
using OpenTelemetry.Logs;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing
        .AddAspNetCoreInstrumentation()
        .AddHttpClientInstrumentation()
        .AddOtlpExporter())
    .WithLogging(logging => logging
        .AddOtlpExporter());

builder.Services.AddDbContext<OrderDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("orders")));

builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    ConnectionMultiplexer.Connect(builder.Configuration.GetConnectionString("cache")!));

builder.Services.AddSingleton<IConnection>(_ =>
{
    var factory = new ConnectionFactory { Uri = new Uri(builder.Configuration.GetConnectionString("rabbitmq")!) };
    return factory.CreateConnection();
});

var notificationsUrl = builder.Configuration["NOTIFICATIONS_URL"];

builder.Services.AddHttpClient();

var app = builder.Build();

app.UseCors();

using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<OrderDbContext>().Database.EnsureCreated();
}

app.MapPost("/orders", async (CreateOrderRequest req, OrderDbContext db, IConnectionMultiplexer redis, IConnection rabbit, ILogger<Program> logger) =>
{
    var order = new OrderModel { Item = req.Item, Quantity = req.Quantity, Status = "Pending" };
    db.Orders.Add(order);
    await db.SaveChangesAsync();
    logger.LogInformation("Order created: {OrderId} item={Item} quantity={Quantity} status={Status}",
        order.Id, order.Item, order.Quantity, order.Status);

    var cacheDb = redis.GetDatabase();
    await cacheDb.StringSetAsync($"order:{order.Id}", JsonSerializer.Serialize(order), TimeSpan.FromMinutes(10));
    logger.LogInformation("Order cached: {OrderId} status={Status}", order.Id, order.Status);

    using var channel = rabbit.CreateModel();
    channel.QueueDeclare("orders", durable: true, exclusive: false, autoDelete: false);
    channel.BasicPublish("", "orders", body: Encoding.UTF8.GetBytes(JsonSerializer.Serialize(order)));
    logger.LogInformation("Order published: {OrderId} queue=orders status={Status}", order.Id, order.Status);

    return Results.Created($"/orders/{order.Id}", order);
});

app.MapGet("/orders", async (OrderDbContext db) =>
    await db.Orders.OrderByDescending(o => o.Id).ToListAsync());

app.MapGet("/orders/{id:int}", async (int id, OrderDbContext db, IConnectionMultiplexer redis) =>
{
    var cacheDb = redis.GetDatabase();
    var cached = await cacheDb.StringGetAsync($"order:{id}");
    if (cached.HasValue) return Results.Ok(JsonSerializer.Deserialize<OrderModel>(cached!));

    var order = await db.Orders.FindAsync(id);
    if (order is null) return Results.NotFound();

    await cacheDb.StringSetAsync($"order:{id}", JsonSerializer.Serialize(order), TimeSpan.FromMinutes(10));
    return Results.Ok(order);
});

app.MapPatch("/orders/{id:int}/status", async (int id, UpdateStatusRequest req, OrderDbContext db, IConnectionMultiplexer redis, IHttpClientFactory httpFactory, ILogger<Program> logger) =>
{
    var order = await db.Orders.FindAsync(id);
    if (order is null) return Results.NotFound();

    order.Status = req.Status;
    await db.SaveChangesAsync();
    await redis.GetDatabase().StringSetAsync($"order:{id}", JsonSerializer.Serialize(order), TimeSpan.FromMinutes(10));
    logger.LogInformation("Order status updated: {OrderId} status={Status}", id, req.Status);

    if (!string.IsNullOrEmpty(notificationsUrl))
    {
        try
        {
            var client = httpFactory.CreateClient();
            await client.PostAsJsonAsync($"{notificationsUrl}/notify", new { orderId = id, status = req.Status });
            logger.LogInformation("Order notification sent: {OrderId} status={Status}", id, req.Status);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Order notification failed: {OrderId} status={Status}", id, req.Status);
        }
    }

    return Results.Ok(order);
});

app.MapGet("/health", () => Results.Ok(new
{
    status = "Healthy",
    service = "orderapi",
    timestamp = DateTimeOffset.UtcNow
}));

app.Run();
