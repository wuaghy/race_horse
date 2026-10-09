using System.Text;
using BuildingBlocks.Api;
using BuildingBlocks.Middleware;
using Handover.Api.Endpoints;
using Handover.Infrastructure;
using Handover.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new() { Title = "Racehorse Handover API", Version = "v1" });
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.ParameterLocation.Header,
        Description = "Use the access token issued by Identity."
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
    throw new InvalidOperationException("Jwt:SigningKey must be provided through a secret configuration source and contain at least 32 bytes.");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
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
    options.AddPolicy("HandoverRead", policy => policy.RequireRole("CUSTOMER", "DRIVER_ESCORT", "FLEET_ROUTE_COORDINATOR", "LOGISTICS_MANAGER", "ADMIN"));
    options.AddPolicy("HandoverPrepare", policy => policy.RequireRole("DRIVER_ESCORT", "FLEET_ROUTE_COORDINATOR", "LOGISTICS_MANAGER", "ADMIN"));
    options.AddPolicy("HandoverWrite", policy => policy.RequireRole("CUSTOMER", "DRIVER_ESCORT", "FLEET_ROUTE_COORDINATOR", "LOGISTICS_MANAGER", "ADMIN"));
    options.AddPolicy("HandoverAccept", policy => policy.RequireRole("CUSTOMER", "LOGISTICS_MANAGER", "ADMIN"));
    options.AddPolicy("HandoverManage", policy => policy.RequireRole("FLEET_ROUTE_COORDINATOR", "LOGISTICS_MANAGER", "ADMIN"));
    options.AddPolicy("HandoverComplete", policy => policy.RequireRole("CUSTOMER", "DRIVER_ESCORT", "FLEET_ROUTE_COORDINATOR", "LOGISTICS_MANAGER", "ADMIN"));
});

builder.Services.AddHandoverInfrastructure(builder.Configuration);

var app = builder.Build();
app.UseMiddleware<GlobalExceptionHandlerMiddleware>();
app.UseMiddleware<CorrelationIdMiddleware>();
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "Racehorse Handover API v1"));
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();
app.MapServiceHealth("handover");
app.MapHandoverEndpoints();

await DatabaseInitializer.InitializeDatabaseAsync(app.Services, app.Logger);
app.Run();

public partial class Program { }
