namespace Booking.Domain.Entities;

public sealed class Horse
{
    public Guid Id { get; set; }
    public string HorseCode { get; set; } = null!;
    public Guid OwnerCustomerId { get; set; }
    public string HorseName { get; set; } = null!;
    public string? RegistrationNumber { get; set; }
    public string? PassportNumber { get; set; }
    public string? Breed { get; set; }
    public string? Sex { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public string? Color { get; set; }
    public Guid? CountryOfOriginId { get; set; }
    public string? SpecialRequirements { get; set; }
    public string Status { get; set; } = "ACTIVE";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int VersionNo { get; set; } = 1;

    public void ValidateName()
    {
        if (string.IsNullOrWhiteSpace(HorseName))
        {
            throw new InvalidOperationException("Horse name is required.");
        }
    }

    public void ValidateDateOfBirth()
    {
        if (DateOfBirth > DateOnly.FromDateTime(DateTime.UtcNow))
        {
            throw new InvalidOperationException("Date of birth cannot be in the future.");
        }
    }

    public void ValidateSex()
    {
        if (Sex is not null && Sex is not ("STALLION" or "MARE" or "GELDING"))
        {
            throw new InvalidOperationException("Horse sex is not supported.");
        }
    }

    public void Update(string horseName, string? registrationNumber, string? passportNumber, string? breed, string? sex,
        DateOnly? dateOfBirth, string? color, Guid? countryOfOriginId, string? specialRequirements)
    {
        HorseName = horseName;
        RegistrationNumber = registrationNumber;
        PassportNumber = passportNumber;
        Breed = breed;
        Sex = sex;
        DateOfBirth = dateOfBirth;
        Color = color;
        CountryOfOriginId = countryOfOriginId;
        SpecialRequirements = specialRequirements;
        UpdatedAt = DateTime.UtcNow;
        VersionNo++;
    }
}
