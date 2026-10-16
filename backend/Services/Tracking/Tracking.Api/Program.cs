using BuildingBlocks.Api;
using BuildingBlocks.Middleware;
using Tracking.Api.Endpoints;
using Tracking.Application.Services;
using Tracking.Infrastructure.Repositories;
using Tracking.Infrastructure.Messaging;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new() { Title = "Racehorse Tracking API", Version = "v1" });
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
    options.AddPolicy("TrackingRead", policy => policy.RequireRole(
        "CUSTOMER", "DRIVER_ESCORT", "FLEET_ROUTE_COORDINATOR", "LOGISTICS_MANAGER", "ADMIN"));
    options.AddPolicy("TrackingUpdate", policy => policy.RequireRole(
        "DRIVER_ESCORT", "FLEET_ROUTE_COORDINATOR", "LOGISTICS_MANAGER", "ADMIN"));
});

// Register Trip Execution services
builder.Services.AddScoped<ITripExecutionService, TripExecutionService>();
builder.Services.AddScoped<ITripExecutionRepository, InMemoryTripExecutionRepository>();
builder.Services.AddScoped<ICheckpointEventRepository, InMemoryCheckpointEventRepository>();
builder.Services.AddScoped<IOutboxMessageRepository, InMemoryOutboxMessageRepository>();

// Register GPS Tracking services
builder.Services.AddScoped<IGpsTrackingService, GpsTrackingService>();
builder.Services.AddScoped<IGpsPointRepository, InMemoryGpsPointRepository>();
builder.Services.AddScoped<ILocationHistoryRepository, InMemoryLocationHistoryRepository>();
builder.Services.AddScoped<IEtaRepository, InMemoryEtaRepository>();

// Register Horse Health services
builder.Services.AddScoped<IHorseHealthService, HorseHealthService>();
builder.Services.AddScoped<IHealthLogRepository, InMemoryHealthLogRepository>();
builder.Services.AddScoped<IHealthAlertRepository, InMemoryHealthAlertRepository>();
builder.Services.AddScoped<IHealthStatusRepository, InMemoryHealthStatusRepository>();
builder.Services.Configure<RabbitMqOptions>(builder.Configuration.GetSection(RabbitMqOptions.SectionName));
builder.Services.AddSingleton<TrackingMessageBus>();
builder.Services.AddSingleton<ITrackingMessageBus>(sp => sp.GetRequiredService<TrackingMessageBus>());
builder.Services.AddHostedService<TrackingOutboxDispatcher>();

var app = builder.Build();
app.UseMiddleware<GlobalExceptionHandlerMiddleware>();
app.UseMiddleware<CorrelationIdMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger(options =>
    {
        options.RouteTemplate = "swagger/{documentName}/swagger.json";
    });
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Racehorse Tracking API v1");
        options.RoutePrefix = "swagger";
    });
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapServiceHealth("tracking");
app.MapTripExecutionEndpoints();
app.MapGpsTrackingEndpoints();
app.MapHorseHealthEndpoints();
app.MapSeedingEndpoints();
app.Run();
