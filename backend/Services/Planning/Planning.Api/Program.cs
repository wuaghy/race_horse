using BuildingBlocks.Api;
using BuildingBlocks.Middleware;
using Microsoft.EntityFrameworkCore;
using Planning.Api;
using Planning.Application.Abstractions;
using Planning.Application.Services;
using Planning.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOpenApi();

var planningConnection = builder.Configuration.GetConnectionString("PlanningDb");
if (!string.IsNullOrWhiteSpace(planningConnection))
{
    builder.Services.AddPlanningPersistence(planningConnection);
}
else
{
    // Local memory fallback if SQL Server is not configured
    builder.Services.AddDbContext<PlanningDbContext>(o => o.UseInMemoryDatabase("PlanningDb_LocalFallback"));
    builder.Services.AddScoped<IPlanningDbContext>(sp => sp.GetRequiredService<PlanningDbContext>());
    builder.Services.AddScoped(typeof(IRepository<>), typeof(EfRepository<>));
    builder.Services.AddScoped<IFleetMasterDataService, FleetMasterDataService>();
    builder.Services.AddScoped<IAvailabilityService, AvailabilityService>();
    builder.Services.AddScoped<ITripPlanningService, TripPlanningService>();
    builder.Services.AddScoped<ITripReadinessService, TripReadinessService>();
}

// RabbitMQ messaging, Outbox Dispatcher & Event Consumers
builder.Services.AddPlanningMessaging(builder.Configuration);

var app = builder.Build();
app.UseMiddleware<GlobalExceptionHandlerMiddleware>();
app.UseMiddleware<CorrelationIdMiddleware>();
app.UseHttpsRedirection();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

var api = app.MapGroup("/api/v1");
api.MapPlanningMasterData();
api.MapPlanningCommands();

app.MapServiceHealth("planning");

// Auto initialize and verify database schema
await DatabaseInitializer.InitializeDatabaseAsync(app.Services, app.Logger);

app.Run();

// Marker for integration testing
public partial class Program { }
