using Identity.Application.Authentication;
using Microsoft.AspNetCore.Identity;

namespace Identity.Infrastructure.Authentication;

public sealed class AspNetPasswordHashService(PasswordHasher<AuthUser> hasher) : IPasswordHashService
{
    public string Hash(AuthUser user, string password) => hasher.HashPassword(user, password);

    public bool Verify(AuthUser user, string passwordHash, string password) =>
        hasher.VerifyHashedPassword(user, passwordHash, password) != PasswordVerificationResult.Failed;
}