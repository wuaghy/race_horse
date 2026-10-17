using BuildingBlocks.Api;
using BuildingBlocks.Middleware;
using Identity.Api.Authentication;
using Identity.Api.Dashboard;
using Identity.Application;
using Identity.Infrastructure;
using Identity.Infrastructure.Authentication;
using Contracts.Grpc.CustomerProfile;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new() { Title = "Racehorse Identity API", Version = "v1" });
    options.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = Microsoft.OpenApi.SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = Microsoft.OpenApi.ParameterLocation.Header,
        Description = "Paste the accessToken from /api/v1/auth/login."
    });
    options.AddSecurityRequirement(document => new Microsoft.OpenApi.OpenApiSecurityRequirement
    {
        [new Microsoft.OpenApi.OpenApiSecuritySchemeReference("Bearer", document)] = []
    });
});
builder.Services.AddValidation();
builder.Services.AddGrpcClient<CustomerProfileProvisioning.CustomerProfileProvisioningClient>(options =>
{
    options.Address = new Uri(builder.Configuration["BookingGrpc:Address"] ?? "http://localhost:8082");
});
builder.Services.AddScoped<Identity.Application.Authentication.ICustomerProfileProvisioner, BookingCustomerProfileProvisioner>();

var jwtSettings = JwtSettings.FromConfiguration(builder.Configuration);
var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSettings.SigningKey));
builder.Services.AddIdentityApplication();
builder.Services.AddIdentityInfrastructure(jwtSettings);
builder.Services.AddHostedService<DemoIdentitySeeder>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtSettings.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtSettings.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = signingKey,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = "name",
            RoleClaimType = "role"
        };
    });
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("LogisticsManager", policy => policy.RequireRole("LOGISTICS_MANAGER", "ADMIN"));
});

var app = builder.Build();

app.UseMiddleware<GlobalExceptionHandlerMiddleware>();
app.UseMiddleware<CorrelationIdMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseSwagger();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/swagger/v1/swagger.json", "Racehorse Identity API v1"));
}

app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapGet("/health", (HttpContext context) =>
{
    var requestId = Guid.TryParse(context.Items["X-Request-ID"]?.ToString(), out var parsedRequestId)
        ? parsedRequestId
        : Guid.NewGuid();
    return Results.Ok(ApiResponse<object>.Success(new
    {
        status = "healthy",
        service = "identity",
        environment = app.Environment.EnvironmentName
    }, requestId: requestId));
})
.WithName("GetIdentityHealth");

app.MapAuthEndpoints();
app.MapIdentityDashboardEndpoints();

app.Run();
