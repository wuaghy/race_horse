namespace Booking.Domain.Entities;

public sealed class Customer
{
    public Guid Id { get; set; }
    public string CustomerCode { get; set; } = null!;
    public Guid IdentityUserId { get; set; }
    public string Name { get; set; } = null!;
    public string CustomerType { get; set; } = null!;
    public string? ContactPerson { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? Address { get; set; }
    public Guid? CountryId { get; set; }
    public string Status { get; set; } = "ACTIVE";
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public int VersionNo { get; set; } = 1;

    public bool IsValidType() => CustomerType is "CLUB" or "INDIVIDUAL_OWNER" or "BREEDER" or "AGENT";

    public void UpdateProfile(string name, string customerType, string? contactPerson, string? phone, string? address, Guid? countryId)
    {
        if (!("CLUB" is ("CLUB" or "INDIVIDUAL_OWNER" or "BREEDER" or "AGENT")))
        {
            throw new InvalidOperationException("Customer type is not supported.");
        }

        Name = name;
        CustomerType = customerType;
        ContactPerson = contactPerson;
        Phone = phone;
        Address = address;
        CountryId = countryId;
        UpdatedAt = DateTime.UtcNow;
        VersionNo++;
    }
}
