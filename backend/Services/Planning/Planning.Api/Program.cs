using System.Text;
using BuildingBlocks.Api;
using BuildingBlocks.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Planning.Api;
using Planning.Application.Abstractions;
using Planning.Application.Services;
using Planning.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOpenApi();

var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "racehorse-identity";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "racehorse-api";
var jwtSigningKey = builder.Configuration["Jwt:SigningKey"];
if (string.IsNullOrWhiteSpace(jwtSigningKey) || Encoding.UTF8.GetByteCount(jwtSigningKey) < 32)
{
    throw new InvalidOperationException("Jwt:SigningKey must be provided through a secret configuration source and contain at least 32 bytes.");
}

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtIssuer,
            ValidateAudience = true,
            ValidAudience = jwtAudience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSigningKey)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = "name",
            RoleClaimType = "role"
        };
    });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("PlanningAccess", policy => policy.RequireRole("TRANSPORT_SPECIALIST", "LOGISTICS_MANAGER", "ADMIN"));
});

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
app.UseAuthentication();
app.UseAuthorization();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

var api = app.MapGroup("/api/v1");
api.MapGroup("").RequireAuthorization("PlanningAccess").MapPlanningMasterData();
api.MapGroup("").RequireAuthorization("PlanningAccess").MapPlanningCommands();

app.MapServiceHealth("planning");

// Auto initialize and verify database schema
await DatabaseInitializer.InitializeDatabaseAsync(app.Services, app.Logger);

app.Run();

// Marker for integration testing
public partial class Program { }
