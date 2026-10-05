using System.Data;
using Booking.Application.Customers;
using BuildingBlocks.Exceptions;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Configuration;

namespace Booking.Infrastructure.Customers;

public sealed class SqlCustomerHorseService : ICustomerHorseService
{
    private readonly string _connectionString;

    public SqlCustomerHorseService(IConfiguration configuration)
    {
        _connectionString = configuration.GetConnectionString("BookingDb")
            ?? throw new InvalidOperationException("ConnectionStrings:BookingDb must be configured.");
    }

    public async Task<CustomerProfile?> GetProfileAsync(Guid identityUserId, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(
            "SELECT Id, CustomerCode, Name, CustomerType, ContactPerson, Phone, Email, Address, CountryId, VersionNo " +
            "FROM dbo.Customers WHERE IdentityUserId = @IdentityUserId;",
            connection);
        command.Parameters.Add("@IdentityUserId", SqlDbType.UniqueIdentifier).Value = identityUserId;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadProfile(reader) : null;
    }

    public async Task<CustomerProfile> SaveProfileAsync(
        Guid identityUserId,
        string email,
        CustomerProfileInput input,
        CancellationToken cancellationToken)
    {
        if (input.CustomerType is not ("CLUB" or "INDIVIDUAL_OWNER" or "BREEDER" or "AGENT"))
        {
            throw new BusinessRuleException("Customer type is not supported.", "CUSTOMER_TYPE_INVALID", "customerType");
        }

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

        await using (var command = new SqlCommand(
            "UPDATE dbo.Customers WITH (UPDLOCK, HOLDLOCK) " +
            "SET Name = @Name, CustomerType = @CustomerType, ContactPerson = @ContactPerson, Phone = @Phone, " +
            "Email = @Email, Address = @Address, CountryId = @CountryId, UpdatedAt = SYSUTCDATETIME(), VersionNo = VersionNo + 1 " +
            "WHERE IdentityUserId = @IdentityUserId AND VersionNo = @VersionNo;",
            connection,
            transaction))
        {
            AddProfileParameters(command, identityUserId, email, input);
            command.Parameters.Add("@VersionNo", SqlDbType.Int).Value = (object?)input.VersionNo ?? DBNull.Value;
            var rows = await command.ExecuteNonQueryAsync(cancellationToken);

            if (rows == 0)
            {
                var exists = await CustomerExistsAsync(connection, transaction, identityUserId, cancellationToken);
                if (exists)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    throw new ConcurrencyException("Customer profile changed. Reload it and retry.");
                }

                if (input.VersionNo is not null)
                {
                    await transaction.RollbackAsync(cancellationToken);
                    throw new ConcurrencyException("Customer profile changed. Reload it and retry.");
                }

                await using var insert = new SqlCommand(
                    "INSERT INTO dbo.Customers (CustomerCode, IdentityUserId, Name, CustomerType, ContactPerson, Phone, Email, Address, CountryId, Status) " +
                    "VALUES (@CustomerCode, @IdentityUserId, @Name, @CustomerType, @ContactPerson, @Phone, @Email, @Address, @CountryId, 'ACTIVE');",
                    connection,
                    transaction);
                AddProfileParameters(insert, identityUserId, email, input);
                insert.Parameters.Add("@CustomerCode", SqlDbType.VarChar, 50).Value = "CUS-" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();
                await insert.ExecuteNonQueryAsync(cancellationToken);
            }
        }

        await transaction.CommitAsync(cancellationToken);
        return (await GetProfileAsync(identityUserId, cancellationToken))!;
    }

    public async Task<PageResult<HorseRecord>> GetHorsesAsync(
        Guid identityUserId,
        int page,
        int pageSize,
        string? search,
        CancellationToken cancellationToken)
    {
        ValidatePage(page, pageSize);
        search = string.IsNullOrWhiteSpace(search) ? null : search.Trim();
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        var customerId = await GetCustomerIdAsync(connection, identityUserId, cancellationToken);
        if (customerId is null)
        {
            return new PageResult<HorseRecord>([], page, pageSize, 0);
        }

        const string filter = "OwnerCustomerId = @CustomerId AND (@Search IS NULL OR HorseName LIKE '%' + @Search + '%' OR HorseCode LIKE '%' + @Search + '%' OR RegistrationNumber LIKE '%' + @Search + '%' OR PassportNumber LIKE '%' + @Search + '%')";
        await using var count = new SqlCommand($"SELECT COUNT_BIG(*) FROM dbo.Horses WHERE {filter};", connection);
        count.Parameters.Add("@CustomerId", SqlDbType.UniqueIdentifier).Value = customerId.Value;
        count.Parameters.Add("@Search", SqlDbType.NVarChar, 200).Value = (object?)search ?? DBNull.Value;
        var total = (long)(await count.ExecuteScalarAsync(cancellationToken) ?? 0L);

        await using var command = new SqlCommand(
            "SELECT Id, HorseCode, HorseName, RegistrationNumber, PassportNumber, Breed, Sex, DateOfBirth, Color, CountryOfOriginId, SpecialRequirements, Status, VersionNo " +
            $"FROM dbo.Horses WHERE {filter} ORDER BY HorseName, Id OFFSET @Offset ROWS FETCH NEXT @PageSize ROWS ONLY;",
            connection);
        command.Parameters.Add("@CustomerId", SqlDbType.UniqueIdentifier).Value = customerId.Value;
        command.Parameters.Add("@Search", SqlDbType.NVarChar, 200).Value = (object?)search ?? DBNull.Value;
        command.Parameters.Add("@Offset", SqlDbType.Int).Value = page * pageSize;
        command.Parameters.Add("@PageSize", SqlDbType.Int).Value = pageSize;

        var horses = new List<HorseRecord>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            horses.Add(ReadHorse(reader));
        }

        return new PageResult<HorseRecord>(horses, page, pageSize, total);
    }

    public async Task<HorseRecord?> GetHorseAsync(Guid identityUserId, Guid horseId, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(
            "SELECT h.Id, h.HorseCode, h.HorseName, h.RegistrationNumber, h.PassportNumber, h.Breed, h.Sex, h.DateOfBirth, h.Color, h.CountryOfOriginId, h.SpecialRequirements, h.Status, h.VersionNo " +
            "FROM dbo.Horses h INNER JOIN dbo.Customers c ON c.Id = h.OwnerCustomerId " +
            "WHERE h.Id = @HorseId AND c.IdentityUserId = @IdentityUserId;",
            connection);
        command.Parameters.Add("@HorseId", SqlDbType.UniqueIdentifier).Value = horseId;
        command.Parameters.Add("@IdentityUserId", SqlDbType.UniqueIdentifier).Value = identityUserId;
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken) ? ReadHorse(reader) : null;
    }

    public async Task<HorseRecord> CreateHorseAsync(Guid identityUserId, HorseInput input, CancellationToken cancellationToken)
    {
        ValidateHorse(input);
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        var customerId = await RequireCustomerIdAsync(connection, identityUserId, cancellationToken);
        var horseId = Guid.NewGuid();
        await using var command = new SqlCommand(
            "INSERT INTO dbo.Horses (Id, HorseCode, OwnerCustomerId, HorseName, RegistrationNumber, PassportNumber, Breed, Sex, DateOfBirth, Color, CountryOfOriginId, SpecialRequirements, Status) " +
            "VALUES (@Id, @HorseCode, @CustomerId, @HorseName, @RegistrationNumber, @PassportNumber, @Breed, @Sex, @DateOfBirth, @Color, @CountryId, @SpecialRequirements, 'ACTIVE');",
            connection);
        AddHorseParameters(command, input);
        command.Parameters.Add("@Id", SqlDbType.UniqueIdentifier).Value = horseId;
        command.Parameters.Add("@HorseCode", SqlDbType.VarChar, 50).Value = "HRS-" + Guid.NewGuid().ToString("N")[..12].ToUpperInvariant();
        command.Parameters.Add("@CustomerId", SqlDbType.UniqueIdentifier).Value = customerId;
        await command.ExecuteNonQueryAsync(cancellationToken);
        return (await GetHorseAsync(identityUserId, horseId, cancellationToken))!;
    }

    public async Task<HorseRecord> UpdateHorseAsync(Guid identityUserId, Guid horseId, HorseInput input, CancellationToken cancellationToken)
    {
        ValidateHorse(input);
        if (input.VersionNo is null)
        {
            throw new BusinessRuleException("VersionNo is required when updating a horse.", "VERSION_REQUIRED", "versionNo");
        }

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(
            "UPDATE h SET HorseName = @HorseName, RegistrationNumber = @RegistrationNumber, PassportNumber = @PassportNumber, " +
            "Breed = @Breed, Sex = @Sex, DateOfBirth = @DateOfBirth, Color = @Color, CountryOfOriginId = @CountryId, " +
            "SpecialRequirements = @SpecialRequirements, UpdatedAt = SYSUTCDATETIME(), VersionNo = VersionNo + 1 " +
            "FROM dbo.Horses h INNER JOIN dbo.Customers c ON c.Id = h.OwnerCustomerId " +
            "WHERE h.Id = @HorseId AND c.IdentityUserId = @IdentityUserId AND h.VersionNo = @VersionNo;",
            connection);
        AddHorseParameters(command, input);
        command.Parameters.Add("@HorseId", SqlDbType.UniqueIdentifier).Value = horseId;
        command.Parameters.Add("@IdentityUserId", SqlDbType.UniqueIdentifier).Value = identityUserId;
        command.Parameters.Add("@VersionNo", SqlDbType.Int).Value = input.VersionNo.Value;
        if (await command.ExecuteNonQueryAsync(cancellationToken) == 0)
        {
            if (await GetHorseAsync(identityUserId, horseId, cancellationToken) is null)
            {
                throw new NotFoundException("Horse", horseId);
            }

            throw new ConcurrencyException("Horse changed. Reload it and retry.");
        }

        return (await GetHorseAsync(identityUserId, horseId, cancellationToken))!;
    }

    public async Task DeleteHorseAsync(Guid identityUserId, Guid horseId, CancellationToken cancellationToken)
    {
        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync(cancellationToken);
        await using var command = new SqlCommand(
            "DELETE h FROM dbo.Horses h INNER JOIN dbo.Customers c ON c.Id = h.OwnerCustomerId " +
            "WHERE h.Id = @HorseId AND c.IdentityUserId = @IdentityUserId;",
            connection);
        command.Parameters.Add("@HorseId", SqlDbType.UniqueIdentifier).Value = horseId;
        command.Parameters.Add("@IdentityUserId", SqlDbType.UniqueIdentifier).Value = identityUserId;
        try
        {
            if (await command.ExecuteNonQueryAsync(cancellationToken) == 0)
            {
                throw new NotFoundException("Horse", horseId);
            }
        }
        catch (SqlException exception) when (exception.Number == 547)
        {
            throw new AppException("A horse referenced by a transport request cannot be deleted.", 409, 40902);
        }
    }

    private static CustomerProfile ReadProfile(SqlDataReader reader) => new(
        reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetString(3),
        reader.IsDBNull(4) ? null : reader.GetString(4),
        reader.IsDBNull(5) ? null : reader.GetString(5),
        reader.IsDBNull(6) ? null : reader.GetString(6),
        reader.IsDBNull(7) ? null : reader.GetString(7),
        reader.IsDBNull(8) ? null : reader.GetGuid(8),
        reader.GetInt32(9));

    private static HorseRecord ReadHorse(SqlDataReader reader) => new(
        reader.GetGuid(0), reader.GetString(1), reader.GetString(2),
        reader.IsDBNull(3) ? null : reader.GetString(3),
        reader.IsDBNull(4) ? null : reader.GetString(4),
        reader.IsDBNull(5) ? null : reader.GetString(5),
        reader.IsDBNull(6) ? null : reader.GetString(6),
        reader.IsDBNull(7) ? null : DateOnly.FromDateTime(reader.GetDateTime(7)),
        reader.IsDBNull(8) ? null : reader.GetString(8),
        reader.IsDBNull(9) ? null : reader.GetGuid(9),
        reader.IsDBNull(10) ? null : reader.GetString(10),
        reader.GetString(11), reader.GetInt32(12));

    private static void AddProfileParameters(SqlCommand command, Guid identityUserId, string email, CustomerProfileInput input)
    {
        command.Parameters.Add("@IdentityUserId", SqlDbType.UniqueIdentifier).Value = identityUserId;
        command.Parameters.Add("@Name", SqlDbType.NVarChar, 255).Value = input.Name.Trim();
        command.Parameters.Add("@CustomerType", SqlDbType.VarChar, 30).Value = input.CustomerType;
        command.Parameters.Add("@ContactPerson", SqlDbType.NVarChar, 200).Value = (object?)input.ContactPerson ?? DBNull.Value;
        command.Parameters.Add("@Phone", SqlDbType.VarChar, 50).Value = (object?)input.Phone ?? DBNull.Value;
        command.Parameters.Add("@Email", SqlDbType.VarChar, 255).Value = email;
        command.Parameters.Add("@Address", SqlDbType.NVarChar, 500).Value = (object?)input.Address ?? DBNull.Value;
        command.Parameters.Add("@CountryId", SqlDbType.UniqueIdentifier).Value = (object?)input.CountryId ?? DBNull.Value;
    }

    private static void AddHorseParameters(SqlCommand command, HorseInput input)
    {
        command.Parameters.Add("@HorseName", SqlDbType.NVarChar, 200).Value = input.HorseName.Trim();
        command.Parameters.Add("@RegistrationNumber", SqlDbType.VarChar, 100).Value = (object?)input.RegistrationNumber ?? DBNull.Value;
        command.Parameters.Add("@PassportNumber", SqlDbType.VarChar, 100).Value = (object?)input.PassportNumber ?? DBNull.Value;
        command.Parameters.Add("@Breed", SqlDbType.NVarChar, 100).Value = (object?)input.Breed ?? DBNull.Value;
        command.Parameters.Add("@Sex", SqlDbType.VarChar, 30).Value = (object?)input.Sex ?? DBNull.Value;
        command.Parameters.Add("@DateOfBirth", SqlDbType.Date).Value = input.DateOfBirth is null ? DBNull.Value : input.DateOfBirth.Value.ToDateTime(TimeOnly.MinValue);
        command.Parameters.Add("@Color", SqlDbType.NVarChar, 100).Value = (object?)input.Color ?? DBNull.Value;
        command.Parameters.Add("@CountryId", SqlDbType.UniqueIdentifier).Value = (object?)input.CountryOfOriginId ?? DBNull.Value;
        command.Parameters.Add("@SpecialRequirements", SqlDbType.NVarChar, 1000).Value = (object?)input.SpecialRequirements ?? DBNull.Value;
    }

    private static void ValidateHorse(HorseInput input)
    {
        if (string.IsNullOrWhiteSpace(input.HorseName))
        {
            throw new BusinessRuleException("Horse name is required.", "HORSE_NAME_REQUIRED", "horseName");
        }

        if (input.DateOfBirth > DateOnly.FromDateTime(DateTime.UtcNow))
        {
            throw new BusinessRuleException("Date of birth cannot be in the future.", "HORSE_DATE_OF_BIRTH_INVALID", "dateOfBirth");
        }

        if (input.Sex is not null && input.Sex is not ("STALLION" or "MARE" or "GELDING"))
        {
            throw new BusinessRuleException("Horse sex is not supported.", "HORSE_SEX_INVALID", "sex");
        }
    }

    private static void ValidatePage(int page, int pageSize)
    {
        if (page < 0 || pageSize is < 1 or > 100)
        {
            throw new BusinessRuleException("Page must be non-negative and pageSize must be between 1 and 100.", "PAGINATION_INVALID");
        }
    }

    private static async Task<bool> CustomerExistsAsync(SqlConnection connection, SqlTransaction transaction, Guid identityUserId, CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand("SELECT 1 FROM dbo.Customers WHERE IdentityUserId = @IdentityUserId;", connection, transaction);
        command.Parameters.Add("@IdentityUserId", SqlDbType.UniqueIdentifier).Value = identityUserId;
        return await command.ExecuteScalarAsync(cancellationToken) is not null;
    }

    private static async Task<Guid?> GetCustomerIdAsync(SqlConnection connection, Guid identityUserId, CancellationToken cancellationToken)
    {
        await using var command = new SqlCommand("SELECT Id FROM dbo.Customers WHERE IdentityUserId = @IdentityUserId;", connection);
        command.Parameters.Add("@IdentityUserId", SqlDbType.UniqueIdentifier).Value = identityUserId;
        var value = await command.ExecuteScalarAsync(cancellationToken);
        return value is Guid customerId ? customerId : null;
    }

    private static async Task<Guid> RequireCustomerIdAsync(SqlConnection connection, Guid identityUserId, CancellationToken cancellationToken) =>
        await GetCustomerIdAsync(connection, identityUserId, cancellationToken)
        ?? throw new NotFoundException("Customer profile", identityUserId);
}
