using Microsoft.EntityFrameworkCore;
using MotorValley.Backend.Data;
using MotorValley.Backend.Hubs;
using MotorValley.Backend.Services;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

// Services
builder.Services.AddControllers();

// SignalR runs in-process locally; in Azure it scales out through the
// Azure SignalR Service backplane the moment a connection string is present,
// so the WebSocket transport survives multiple backend replicas.
var signalR = builder.Services.AddSignalR();
var azureSignalR = builder.Configuration.GetConnectionString("AzureSignalR");
if (!string.IsNullOrWhiteSpace(azureSignalR))
    signalR.AddAzureSignalR(azureSignalR);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Relational store — provider is configurable. PostgreSQL is the default that ships and
// runs in Docker; Oracle is wired and selectable via Database:Provider, since the JD-side
// estate is Oracle; SQLite is the zero-install default for the no-Docker path (a single
// file, no server to run). EF Core keeps the repository code identical across all three —
// only the provider and connection string change.
var dbProvider = builder.Configuration["Database:Provider"] ?? "Postgres";
var usingSqlite = string.Equals(dbProvider, "Sqlite", StringComparison.OrdinalIgnoreCase);
builder.Services.AddDbContext<AppDbContext>(options =>
{
    if (string.Equals(dbProvider, "Oracle", StringComparison.OrdinalIgnoreCase))
        options.UseOracle(builder.Configuration.GetConnectionString("Oracle"));
    else if (usingSqlite)
        options.UseSqlite(builder.Configuration.GetConnectionString("Sqlite") ?? "Data Source=motorvalley.db");
    else
        options.UseNpgsql(builder.Configuration.GetConnectionString("PostgreSQL"));
});

// Cache — Redis when a connection string is configured, otherwise a process-local
// in-memory stand-in so the stack runs with no external cache (the no-Docker path).
// Setting Cache:Provider=Memory forces the in-memory path even when Redis is configured.
var cacheProvider = builder.Configuration["Cache:Provider"];
var redisConn = builder.Configuration.GetConnectionString("Redis");
var useRedis = string.Equals(cacheProvider, "Redis", StringComparison.OrdinalIgnoreCase)
    || (string.IsNullOrWhiteSpace(cacheProvider) && !string.IsNullOrWhiteSpace(redisConn));
if (useRedis)
{
    builder.Services.AddSingleton<IConnectionMultiplexer>(_ =>
    {
        var config = ConfigurationOptions.Parse(redisConn ?? "localhost:6379");
        return ConnectionMultiplexer.Connect(config);
    });
    builder.Services.AddScoped<ICacheService, RedisCacheService>();
}
else
{
    builder.Services.AddSingleton<ICacheService, InMemoryCacheService>();
}

// Alert ingestion pipeline (persist → fan-out → cache), shared by both transports.
builder.Services.AddScoped<IAlertRepository, AlertRepository>();
builder.Services.AddScoped<IAlertIngestionService, AlertIngestionService>();
builder.Services.AddScoped<IMachineNoteRepository, MachineNoteRepository>();


// Kafka consumer — the default transport. Disabled (Kafka:Enabled=false) in the no-Docker
// path, where the processor delivers alerts over HTTP to the IngestController instead.
var kafkaEnabled = builder.Configuration.GetValue("Kafka:Enabled", true);
if (kafkaEnabled)
{
    builder.Services.Configure<HostOptions>(opts => opts.BackgroundServiceExceptionBehavior = BackgroundServiceExceptionBehavior.Ignore);
    builder.Services.AddHostedService<AlertConsumerWorker>();
}

var app = builder.Build();

// Swagger in any non-Production environment (Development, and the no-Docker "Local").
if (!app.Environment.IsProduction())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors(policy =>
{
    policy.AllowAnyOrigin()
          .AllowAnyMethod()
          .AllowAnyHeader();
});

app.MapControllers();
app.MapHub<AlertHub>("/hubs/alerts");

// Bring the schema up on startup. The relational providers that ship migrations
// (Postgres, Oracle) apply them for versioned, evolvable schema; SQLite — used only for
// the throwaway no-Docker database — has no provider-specific migrations, so its schema
// (including the unique index defined in AppDbContext) is created directly from the model.
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (usingSqlite)
        db.Database.EnsureCreated();
    else
        db.Database.Migrate();
}

app.Run();
