var builder = DistributedApplication.CreateBuilder(args);

var demoConfiguration = builder.Configuration.GetSection("Demo");
var frontendEnabled = ReadBoolean(demoConfiguration["FrontendEnabled"], true);
var notificationsEnabled = ReadBoolean(demoConfiguration["NotificationsEnabled"], true);
var workerEnabled = ReadBoolean(demoConfiguration["WorkerEnabled"], true);
var frontendPort = ReadInteger(demoConfiguration["FrontendPort"], 4200);

var demoSecret = builder.AddParameter(
    "demo-secret",
    new GenerateParameterDefault { MinLength = 24, Special = false },
    secret: true,
    persist: true);

var postgresUsername = builder.AddParameter(
    "postgres-username",
    "postgres",
    false,
    true);
var postgresPassword = builder.AddParameter(
    "postgres-password",
    new GenerateParameterDefault { MinLength = 24, Special = false },
    secret: true,
    persist: true);
var postgres = builder.AddPostgres("postgres", postgresUsername, postgresPassword).WithDataVolume();
var ordersDb = postgres.AddDatabase("orders");

var cache = builder.AddRedis("cache");

var rabbitmq = builder.AddRabbitMQ("rabbitmq").WithManagementPlugin();

var notifications = notificationsEnabled
    ? builder.AddProject<Projects.NotificationService>("notifications")
        .WithEnvironment("DEMO_SECRET", demoSecret)
        .WithHttpEndpoint(targetPort: 5200)
        .WithOtlpExporter()
        .WithHttpHealthCheck("/health")
    : null;

var orderApi = builder.AddProject<Projects.OrderApi>("orderapi")
    .WithReference(ordersDb)
    .WithReference(cache)
    .WithReference(rabbitmq)
    .WithEnvironment("DEMO_SECRET", demoSecret)
    .WithHttpEndpoint(targetPort: 5100)
    .WaitFor(ordersDb)
    .WaitFor(cache)
    .WaitFor(rabbitmq)
    .WithOtlpExporter()
    .WithHttpHealthCheck("/health");

if (notifications is not null)
{
    orderApi
        .WithReference(notifications)
        .WithEnvironment("NOTIFICATIONS_URL", notifications.GetEndpoint("http"))
        .WaitFor(notifications);
}

// Java Spring Boot worker: not a native .NET project, so it runs as a Dockerfile-built resource.
if (workerEnabled)
{
    var orderWorker = builder.AddDockerfile("orderworker", "../OrderWorker")
        .WithEnvironment("SPRING_RABBITMQ_ADDRESSES", rabbitmq.Resource.ConnectionStringExpression)
        .WithEnvironment("ORDER_API_URL", orderApi.GetEndpoint("http"))
        .WithEnvironment("DEMO_SECRET", demoSecret)
        .WaitFor(rabbitmq)
        .WaitFor(orderApi);

    if (notifications is not null)
    {
        orderWorker
            .WithEnvironment("NOTIFICATIONS_URL", notifications.GetEndpoint("http"))
            .WaitFor(notifications);
    }
}

if (frontendEnabled)
{
    builder.AddViteApp("frontend", "../frontend", "start")
        .WithEnvironment("services__orderapi__http__0", orderApi.GetEndpoint("http"))
        .WithEndpoint("http", endpoint => endpoint.Port = frontendPort)
        .WithExternalHttpEndpoints()
        .WaitFor(orderApi);
}

builder.Build().Run();

static bool ReadBoolean(string? value, bool fallback) =>
    bool.TryParse(value, out var parsed) ? parsed : fallback;

static int ReadInteger(string? value, int fallback) =>
    int.TryParse(value, out var parsed) ? parsed : fallback;
