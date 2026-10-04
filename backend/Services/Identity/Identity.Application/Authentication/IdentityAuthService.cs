using BuildingBlocks.Exceptions;

namespace Identity.Application.Authentication;

public sealed class IdentityAuthService(
    IIdentityStore store,
    IPasswordHashService passwordHashService,
    IIdentityTokenService tokenService) : IIdentityAuthService
{
    public async Task<AuthSessionResult> RegisterCustomerAsync(RegisterCustomerCommand command, CancellationToken cancellationToken)
    {
        var email = command.Email.Trim().ToLowerInvariant();
        var fullName = command.FullName.Trim();
        var userForHash = new AuthUser(Guid.Empty, email, fullName, command.Phone, "CUSTOMER", null, "ACTIVE");
        var passwordHash = passwordHashService.Hash(userForHash, command.Password);
        var refreshToken = tokenService.CreateRefreshToken();
        var refreshExpiresAt = tokenService.RefreshTokenExpiry;

        AuthUser user;
        try
        {
            user = await store.CreateCustomerAsync(
                email,
                fullName,
                command.Phone,
                passwordHash,
                tokenService.HashRefreshToken(refreshToken),
                refreshExpiresAt,
                command.DeviceInfo,
                cancellationToken);
        }
        catch (DuplicateIdentityEmailException)
        {
            throw new AppException("An account with this email already exists.", 409, 40901);
        }

        return CreateSession(user, refreshToken, refreshExpiresAt);
    }

    public async Task<AuthSessionResult> LoginAsync(LoginCommand command, CancellationToken cancellationToken)
    {
        var user = await store.FindByEmailAsync(command.Email.Trim().ToLowerInvariant(), cancellationToken);
        if (user?.PasswordHash is null || user.Status != "ACTIVE" ||
            !passwordHashService.Verify(user, user.PasswordHash, command.Password))
        {
            throw new UnauthorizedException("Email or password is incorrect.");
        }

        var refreshToken = tokenService.CreateRefreshToken();
        var refreshExpiresAt = tokenService.RefreshTokenExpiry;
        await store.AddRefreshTokenAsync(
            user.Id,
            tokenService.HashRefreshToken(refreshToken),
            refreshExpiresAt,
            command.DeviceInfo,
            cancellationToken);

        return CreateSession(user, refreshToken, refreshExpiresAt);
    }

    public async Task<AuthSessionResult> RefreshAsync(RefreshCommand command, CancellationToken cancellationToken)
    {
        var replacementToken = tokenService.CreateRefreshToken();
        var replacementExpiresAt = tokenService.RefreshTokenExpiry;
        var user = await store.RotateRefreshTokenAsync(
            tokenService.HashRefreshToken(command.RefreshToken),
            tokenService.HashRefreshToken(replacementToken),
            DateTimeOffset.UtcNow,
            replacementExpiresAt,
            command.DeviceInfo,
            cancellationToken);

        if (user is null || user.Status != "ACTIVE")
        {
            throw new UnauthorizedException("Refresh token is invalid, expired, or revoked.");
        }

        return CreateSession(user, replacementToken, replacementExpiresAt);
    }

    public Task LogoutAsync(string refreshToken, CancellationToken cancellationToken) =>
        store.RevokeRefreshTokenAsync(tokenService.HashRefreshToken(refreshToken), DateTimeOffset.UtcNow, cancellationToken);

    private AuthSessionResult CreateSession(AuthUser user, string refreshToken, DateTimeOffset refreshExpiresAt)
    {
        var accessToken = tokenService.IssueAccessToken(user);
        return new AuthSessionResult(
            accessToken.Value,
            refreshToken,
            accessToken.ExpiresAt,
            refreshExpiresAt,
            new AuthenticatedIdentity(user.Id, user.Email, user.FullName, user.RoleCode));
    }
}