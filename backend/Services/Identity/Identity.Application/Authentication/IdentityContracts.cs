namespace Identity.Application.Authentication;

public sealed record AuthUser(
    Guid Id,
    string Email,
    string FullName,
    string? Phone,
    string RoleCode,
    string? PasswordHash,
    string Status);

public sealed record AuthenticatedIdentity(Guid Id, string Email, string FullName, string Role);

public sealed record IssuedAccessToken(string Value, DateTimeOffset ExpiresAt);

public sealed record AuthSessionResult(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset ExpiresAt,
    DateTimeOffset RefreshExpiresAt,
    AuthenticatedIdentity User);

public sealed record RegisterCustomerCommand(string Email, string Password, string FullName, string? Phone, string? DeviceInfo);

public sealed record LoginCommand(string Email, string Password, string? DeviceInfo);

public sealed record RefreshCommand(string RefreshToken, string? DeviceInfo);

public interface IPasswordHashService
{
    string Hash(AuthUser user, string password);

    bool Verify(AuthUser user, string passwordHash, string password);
}

public interface IIdentityTokenService
{
    DateTimeOffset RefreshTokenExpiry { get; }

    IssuedAccessToken IssueAccessToken(AuthUser user);

    string CreateRefreshToken();

    string HashRefreshToken(string token);
}

public interface IIdentityAuthService
{
    Task<AuthSessionResult> RegisterCustomerAsync(RegisterCustomerCommand command, CancellationToken cancellationToken);

    Task<AuthSessionResult> LoginAsync(LoginCommand command, CancellationToken cancellationToken);

    Task<AuthSessionResult> RefreshAsync(RefreshCommand command, CancellationToken cancellationToken);

    Task LogoutAsync(string refreshToken, CancellationToken cancellationToken);
}

public interface IIdentityStore
{
    Task<AuthUser?> FindByEmailAsync(string email, CancellationToken cancellationToken);

    Task<AuthUser?> FindByIdAsync(Guid id, CancellationToken cancellationToken);

    Task EnsureDevelopmentUserAsync(
        Guid id,
        string email,
        string fullName,
        string roleCode,
        string passwordHash,
        CancellationToken cancellationToken);

    Task<AuthUser> CreateCustomerAsync(
        string email,
        string fullName,
        string? phone,
        string passwordHash,
        string refreshTokenHash,
        DateTimeOffset refreshTokenExpiresAt,
        string? deviceInfo,
        CancellationToken cancellationToken);

    Task AddRefreshTokenAsync(
        Guid userId,
        string tokenHash,
        DateTimeOffset expiresAt,
        string? deviceInfo,
        CancellationToken cancellationToken);

    Task<AuthUser?> RotateRefreshTokenAsync(
        string currentTokenHash,
        string replacementTokenHash,
        DateTimeOffset now,
        DateTimeOffset replacementExpiresAt,
        string? deviceInfo,
        CancellationToken cancellationToken);

    Task RevokeRefreshTokenAsync(string tokenHash, DateTimeOffset revokedAt, CancellationToken cancellationToken);
}

public sealed class DuplicateIdentityEmailException : Exception;