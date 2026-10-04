namespace Compliance.Domain.Entities;

public class DocumentType
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Scope { get; set; } = "HORSE"; // HORSE, REQUEST, TRIP
    public string? Description { get; set; }
    public int? DefaultValidityDays { get; set; }
    public bool Active { get; set; } = true;

    public ICollection<Document> Documents { get; set; } = new List<Document>();
    public ICollection<RegulationDocumentRequirement> Requirements { get; set; } = new List<RegulationDocumentRequirement>();
}

public static class DocumentScope
{
    public const string Horse = "HORSE";
    public const string Request = "REQUEST";
    public const string Trip = "TRIP";

    public static readonly string[] All = [Horse, Request, Trip];
}
