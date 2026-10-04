using System.Data;
using Identity.Application.Authentication;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace Identity.Infrastructure.Authentication;

public sealed class SqlIdentityStore : IIdentityStore
{
    private readonly string _connectionString;

    public SqlIdentityStore(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("IdentityDb")
            ?? throw new InvalidOperationException("ConnectionStrings:IdentityDb must be configured.");
    }

    public async Task<AuthUser?> FindByEmailAsync(string email, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return await FindUserAsync(connection, null, "u.Email = @Email", command =>
            command.Parameters.Add("@Email", SqlDbType.VarChar, 255).Value = email, cancellationToken);
    }

    public async Task<AuthUser?> FindByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        return await FindUserAsync(connection, null, "u.Id = @Id", command =>
            command.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = id, cancellationToken);
    }

    public async Task EnsureDevelopmentUserAsync(
        Guid id,
        string email,
        string fullName,
        string roleCode,
        string passwordHash,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            await using var command = new SqlCommand(
                "IF NOT EXISTS (SELECT 1 FROM dbo.Roles WHERE Code = @RoleCode) " +
                "THROW 51003, 'Demo role is missing from IdentityDb.', 1; " +
                "IF EXISTS (SELECT 1 FROM dbo.Users WHERE Id = @Id AND Email <> @Email) " +
                "THROW 51001, 'Demo user ID is already assigned to another email.', 1; " +
                "IF EXISTS (SELECT 1 FROM dbo.Users WHERE Email = @Email AND Id <> @Id) " +
                "THROW 51002, 'Demo email is already assigned to another user.', 1; " +
                "IF EXISTS (SELECT 1 FROM dbo.Users WHERE Id = @Id) " +
                "UPDATE u SET RoleId = r.Id, PasswordHash = @PasswordHash, Status = 'ACTIVE', " +
                "UpdatedAt = SYSUTCDATETIME(), VersionNo = VersionNo + 1 " +
                "FROM dbo.Users u CROSS JOIN dbo.Roles r WHERE u.Id = @Id AND r.Code = @RoleCode; " +
                "ELSE INSERT INTO dbo.Users (Id, RoleId, Email, FullName, PasswordHash, Status) " +
                "SELECT @Id, Id, @Email, @FullName, @PasswordHash, 'ACTIVE' FROM dbo.Roles WHERE Code = @RoleCode;",
                connection,
                transaction);
            command.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = id;
            command.Parameters.Add("@Email", SqlDbType.VarChar, 255).Value = email;
            command.Parameters.Add("@FullName", SqlDbType.NVarChar, 200).Value = fullName;
            command.Parameters.Add("@RoleCode", SqlDbType.VarChar, 50).Value = roleCode;
            command.Parameters.Add("@PasswordHash", SqlDbType.VarChar, 500).Value = passwordHash;
            await command.ExecuteNonQueryAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task<AuthUser> CreateCustomerAsync(
        string email,
        string fullName,
        string? phone,
        string passwordHash,
        string refreshTokenHash,
        DateTimeOffset refreshTokenExpiresAt,
        string? deviceInfo,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            await using var insertUser = new SqlCommand(
                "INSERT INTO dbo.Users (RoleId, Email, FullName, Phone, PasswordHash, Status) " +
                "OUTPUT INSERTED.Id " +
                "SELECT Id, @Email, @FullName, @Phone, @PasswordHash, 'ACTIVE' FROM dbo.Roles WHERE Code = 'CUSTOMER';",
                connection,
                transaction);
            insertUser.Parameters.Add("@Email", SqlDbType.VarChar, 255).Value = email;
            insertUser.Parameters.Add("@FullName", SqlDbType.NVarChar, 200).Value = fullName;
            insertUser.Parameters.Add("@Phone", SqlDbType.VarChar, 50).Value = (object?)phone ?? DBNull.Value;
            insertUser.Parameters.Add("@PasswordHash", SqlDbType.VarChar, 500).Value = passwordHash;

            var userIdValue = await insertUser.ExecuteScalarAsync(cancellationToken);
            if (userIdValue is not Guid userId)
            {
                throw new InvalidOperationException("The CUSTOMER role has not been seeded in IdentityDb.");
            }

            await InsertRefreshTokenAsync(
                connection,
                transaction,
                userId,
                refreshTokenHash,
                refreshTokenExpiresAt,
                deviceInfo,
                cancellationToken);

            await transaction.CommitAsync(cancellationToken);
            return new AuthUser(userId, email, fullName, phone, "CUSTOMER", passwordHash, "ACTIVE");
        }
        catch (SqlException exception) when (exception.Number is 2601 or 2627)
        {
            await transaction.RollbackAsync(cancellationToken);
            throw new DuplicateIdentityEmailException();
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task AddRefreshTokenAsync(
        Guid userId,
        string tokenHash,
        DateTimeOffset expiresAt,
        string? deviceInfo,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await InsertRefreshTokenAsync(connection, null, userId, tokenHash, expiresAt, deviceInfo, cancellationToken);
    }

    public async Task<AuthUser?> RotateRefreshTokenAsync(
        string currentTokenHash,
        string replacementTokenHash,
        DateTimeOffset now,
        DateTimeOffset replacementExpiresAt,
        string? deviceInfo,
        CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(cancellationToken);

        try
        {
            await using var update = new SqlCommand(
                "UPDATE dbo.RefreshTokens WITH (UPDLOCK, ROWLOCK) " +
                "SET RevokedAt = @Now, ReplacedByTokenHash = @ReplacementHash " +
                "OUTPUT INSERTED.UserId " +
                "WHERE TokenHash = @CurrentHash AND RevokedAt IS NULL AND ExpiresAt > @Now;",
                connection,
                transaction);
            update.Parameters.Add("@Now", SqlDbType.DateTime2).Value = now.UtcDateTime;
            update.Parameters.Add("@ReplacementHash", SqlDbType.VarChar, 500).Value = replacementTokenHash;
            update.Parameters.Add("@CurrentHash", SqlDbType.VarChar, 500).Value = currentTokenHash;

            var userIdValue = await update.ExecuteScalarAsync(cancellationToken);
            if (userIdValue is not Guid userId)
            {
                await transaction.RollbackAsync(cancellationToken);
                return null;
            }

            await InsertRefreshTokenAsync(
                connection,
                transaction,
                userId,
                replacementTokenHash,
                replacementExpiresAt,
                deviceInfo,
                cancellationToken);

            var user = await FindUserAsync(connection, transaction, "u.Id = @Id", command =>
                command.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = userId, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return user;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }

    public async Task RevokeRefreshTokenAsync(string tokenHash, DateTimeOffset revokedAt, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(
            "UPDATE dbo.RefreshTokens SET RevokedAt = @RevokedAt WHERE TokenHash = @TokenHash AND RevokedAt IS NULL;",
            connection);
        command.Parameters.Add("@RevokedAt", SqlDbType.DateTime2).Value = revokedAt.UtcDateTime;
        command.Parameters.Add("@TokenHash", SqlDbType.VarChar, 500).Value = tokenHash;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertRefreshTokenAsync(
        SqlConnection connection,
        SqlTransaction? transaction,
        Guid userId,
        string tokenHash,
        DateTimeOffset expiresAt,
        string? deviceInfo,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(
            "INSERT INTO dbo.RefreshTokens (UserId, TokenHash, DeviceInfo, ExpiresAt) " +
            "VALUES (@UserId, @TokenHash, @DeviceInfo, @ExpiresAt);",
            connection,
            transaction);
        command.Parameters.Add("@UserId", SqlDbType.UniqueIdentifier).Value = userId;
        command.Parameters.Add("@TokenHash", SqlDbType.VarChar, 500).Value = tokenHash;
        command.Parameters.Add("@DeviceInfo", SqlDbType.NVarChar, 300).Value = (object?)deviceInfo ?? DBNull.Value;
        command.Parameters.Add("@ExpiresAt", SqlDbType.DateTime2).Value = expiresAt.UtcDateTime;
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<AuthUser?> FindUserAsync(
        SqlConnection connection,
        SqlTransaction? transaction,
        string predicate,
        Action<SqlCommand> addParameter,
        CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand(
            "SELECT u.Id, u.Email, u.FullName, u.Phone, r.Code, u.PasswordHash, u.Status " +
            "FROM dbo.Users AS u INNER JOIN dbo.Roles AS r ON r.Id = u.RoleId WHERE " + predicate + ";",
            connection,
            transaction);
        addParameter(command);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new AuthUser(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.IsDBNull(3) ? null : reader.GetString(3),
            reader.GetString(4),
            reader.IsDBNull(5) ? null : reader.GetString(5),
            reader.GetString(6));
    }
}