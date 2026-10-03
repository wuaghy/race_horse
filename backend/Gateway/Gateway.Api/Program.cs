using BuildingBlocks.Api;
using BuildingBlocks.Middleware;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

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
    options.AddPolicy("authenticated", policy => policy.RequireAuthenticatedUser()));
builder.Services.AddReverseProxy().LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"));

var app = builder.Build();

app.UseMiddleware<GlobalExceptionHandlerMiddleware>();
app.UseMiddleware<CorrelationIdMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
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
        service = "gateway",
        environment = app.Environment.EnvironmentName
    }, requestId: requestId));
})
.WithName("GetGatewayHealth")
.AllowAnonymous();

app.MapReverseProxy();

app.Run();
