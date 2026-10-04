namespace Compliance.Domain.Entities;

public class RegulationRule
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string RuleCode { get; set; } = string.Empty;
    public Guid OriginCountryId { get; set; }
    public Guid DestinationCountryId { get; set; }
    public Guid? BorderPointId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool QuarantineRequired { get; set; }
    public bool CustomsRequired { get; set; }
    public bool InspectionRequired { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }
    public bool Active { get; set; } = true;

    public ICollection<RegulationDocumentRequirement> Requirements { get; set; } = new List<RegulationDocumentRequirement>();

    public bool IsEffective(DateOnly? asOfDate = null)
    {
        if (!Active) return false;
        var checkDate = asOfDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        if (checkDate < EffectiveFrom) return false;
        if (EffectiveTo.HasValue && checkDate > EffectiveTo.Value) return false;
        return true;
    }
}
