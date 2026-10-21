using System.Text;
using BuildingBlocks.Api;
using BuildingBlocks.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Planning.Api;
using Planning.Api.Grpc;
using Planning.Application.Abstractions;
using Planning.Application.Services;
using Planning.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new() { Title = "Racehorse Planning API", Version = "v1" });
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.ParameterLocation.Header,
        Description = "Paste the accessToken from Identity /api/v1/auth/login."
    });
    options.AddSecurityRequirement(document => new Microsoft.OpenApi.OpenApiSecurityRequirement
    {
        [new Microsoft.OpenApi.OpenApiSecuritySchemeReference("Bearer", document)] = []
    });
});
builder.Services.AddGrpc();

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
    options.AddPolicy("PlanningAccess", policy => policy.RequireRole("TRANSPORT_SPECIALIST", "FLEET_ROUTE_COORDINATOR", "LOGISTICS_MANAGER", "ADMIN"));
    options.AddPolicy("PlanningAssignedTrips", policy => policy.RequireRole("DRIVER", "ESCORT", "DRIVER_ESCORT"));
    options.AddPolicy("PlanningLocationRead", policy => policy.RequireRole("CUSTOMER", "TRANSPORT_SPECIALIST", "FLEET_ROUTE_COORDINATOR", "LOGISTICS_MANAGER", "ADMIN"));
    options.AddPolicy("PlanningApproval", policy => policy.RequireRole("LOGISTICS_MANAGER", "ADMIN"));
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
    app.UseSwagger();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "Racehorse Planning API v1"));
}

var api = app.MapGroup("/api/v1");
api.MapGroup("").RequireAuthorization("PlanningAccess").MapPlanningMasterData();
api.MapGroup("").RequireAuthorization("PlanningAccess").MapPlanningCommands();
api.MapGroup("").RequireAuthorization("PlanningAssignedTrips").MapAssignedTripEndpoints();
api.MapGroup("").RequireAuthorization("PlanningLocationRead").MapBookingLocationRead();

app.MapServiceHealth("planning");
app.MapGrpcService<PlanningQueryGrpcService>();

// Auto initialize and verify database schema
await DatabaseInitializer.InitializeDatabaseAsync(app.Services, app.Logger);

app.Run();

// Marker for integration testing
public partial class Program { }
