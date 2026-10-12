using System.Text;
using BuildingBlocks.Api;
using BuildingBlocks.Middleware;
using Compliance.Api.Endpoints;
using Compliance.Infrastructure;
using Compliance.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new() { Title = "Racehorse Compliance API", Version = "v1" });
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

builder.Services.AddValidation();

// JWT Authentication configuration
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "racehorse-identity";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "racehorse-api";
var jwtSigningKey = builder.Configuration["Jwt:SigningKey"];

if (!string.IsNullOrWhiteSpace(jwtSigningKey) && Encoding.UTF8.GetByteCount(jwtSigningKey) >= 32)
{
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
}
else
{
    throw new InvalidOperationException("Jwt:SigningKey must be provided through a secret configuration source and contain at least 32 bytes.");
}

builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("TransportSpecialist", policy => policy.RequireRole("TRANSPORT_SPECIALIST", "LOGISTICS_MANAGER", "ADMIN"));
    options.AddPolicy("ComplianceOfficer", policy => policy.RequireRole("COMPLIANCE_OFFICER", "ADMIN"));
    options.AddPolicy("ComplianceAccess", policy => policy.RequireRole("CUSTOMER", "TRANSPORT_SPECIALIST", "LOGISTICS_MANAGER", "COMPLIANCE_OFFICER", "ADMIN"));
});

// Compliance Infrastructure & Persistence
builder.Services.AddComplianceInfrastructure(builder.Configuration);

var app = builder.Build();

app.UseMiddleware<GlobalExceptionHandlerMiddleware>();
app.UseMiddleware<CorrelationIdMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "Racehorse Compliance API v1"));
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

// Health check endpoint
app.MapServiceHealth("compliance");

// Map Domain Endpoints
app.MapComplianceMasterDataEndpoints();
app.MapDocumentEndpoints();
app.MapClearanceEndpoints();
app.MapReadinessEndpoints();

// Initialize database & seed defaults
await DatabaseInitializer.InitializeDatabaseAsync(app.Services, app.Logger);

app.Run();

// Marker for testing
public partial class Program { }
