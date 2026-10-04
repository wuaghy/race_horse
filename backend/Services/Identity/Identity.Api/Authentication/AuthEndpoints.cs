using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using BuildingBlocks.Api;
using BuildingBlocks.Exceptions;
using Identity.Application.Authentication;

namespace Identity.Api.Authentication;

public static class AuthEndpoints
{
    public static RouteGroupBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/v1/auth");

        group.MapPost("/register", RegisterAsync)
            .AllowAnonymous()
            .WithName("RegisterCustomer");
        group.MapPost("/login", LoginAsync)
            .AllowAnonymous()
            .WithName("Login");
        group.MapPost("/refresh", RefreshAsync)
            .AllowAnonymous()
            .WithName("RefreshToken");
        group.MapPost("/logout", LogoutAsync)
            .AllowAnonymous()
            .WithName("Logout");
        group.MapGet("/me", GetCurrentUser)
            .RequireAuthorization()
            .WithName("GetCurrentUser");

        return group;
    }

    private static async Task<IResult> RegisterAsync(
        RegisterRequest request,
        IIdentityAuthService authService,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await authService.RegisterCustomerAsync(
            new RegisterCustomerCommand(request.Email, request.Password, request.FullName, request.Phone, request.DeviceInfo),
            cancellationToken);
        var response = ToResponse(result);
        return Results.Created("/api/v1/auth/me", ApiResponse<AuthTokensResponse>.Success(response, "Account created.", GetRequestId(context)));
    }

    private static async Task<IResult> LoginAsync(
        LoginRequest request,
        IIdentityAuthService authService,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await authService.LoginAsync(new LoginCommand(request.Email, request.Password, request.DeviceInfo), cancellationToken);

        return Results.Ok(ApiResponse<AuthTokensResponse>.Success(
            ToResponse(result),
            "Login successful.",
            GetRequestId(context)));
    }

    private static async Task<IResult> RefreshAsync(
        RefreshTokenRequest request,
        IIdentityAuthService authService,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        var result = await authService.RefreshAsync(new RefreshCommand(request.RefreshToken, request.DeviceInfo), cancellationToken);

        return Results.Ok(ApiResponse<AuthTokensResponse>.Success(
            ToResponse(result),
            "Token refreshed.",
            GetRequestId(context)));
    }

    private static async Task<IResult> LogoutAsync(
        RefreshTokenRequest request,
        IIdentityAuthService authService,
        HttpContext context,
        CancellationToken cancellationToken)
    {
        await authService.LogoutAsync(request.RefreshToken, cancellationToken);
        return Results.Ok(ApiResponse<object>.Success(new { loggedOut = true }, "Logout successful.", GetRequestId(context)));
    }

    private static IResult GetCurrentUser(ClaimsPrincipal principal, HttpContext context)
    {
        var id = Guid.Parse(principal.FindFirstValue("sub")!);
        var user = new AuthUserResponse(
            id,
            principal.FindFirstValue("email")!,
            principal.FindFirstValue("name")!,
            principal.FindFirstValue("role")!);
        return Results.Ok(ApiResponse<AuthUserResponse>.Success(user, requestId: GetRequestId(context)));
    }

    private static AuthTokensResponse ToResponse(AuthSessionResult result) => new(
        result.AccessToken,
        result.RefreshToken,
        result.ExpiresAt,
        result.RefreshExpiresAt,
        new AuthUserResponse(result.User.Id, result.User.Email, result.User.FullName, result.User.Role));

    private static Guid GetRequestId(HttpContext context) =>
        Guid.TryParse(context.Items["X-Request-ID"]?.ToString(), out var requestId) ? requestId : Guid.NewGuid();
}

public sealed record RegisterRequest
{
    [Required, EmailAddress, MaxLength(255)]
    public required string Email { get; init; }

    [Required, MinLength(12), MaxLength(128)]
    public required string Password { get; init; }

    [Required, MaxLength(200)]
    public required string FullName { get; init; }

    [MaxLength(50)]
    public string? Phone { get; init; }

    [MaxLength(300)]
    public string? DeviceInfo { get; init; }
}

public sealed record LoginRequest
{
    [Required, EmailAddress, MaxLength(255)]
    public required string Email { get; init; }

    [Required, MaxLength(128)]
    public required string Password { get; init; }

    [MaxLength(300)]
    public string? DeviceInfo { get; init; }
}

public sealed record RefreshTokenRequest
{
    [Required, MaxLength(200)]
    public required string RefreshToken { get; init; }

    [MaxLength(300)]
    public string? DeviceInfo { get; init; }
}

public sealed record AuthUserResponse(Guid Id, string Email, string FullName, string Role);

public sealed record AuthTokensResponse(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset ExpiresAt,
    DateTimeOffset RefreshExpiresAt,
    AuthUserResponse User);