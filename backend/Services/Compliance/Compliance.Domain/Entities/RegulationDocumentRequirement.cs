namespace Compliance.Domain.Entities;

public class RegulationDocumentRequirement
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid RegulationRuleId { get; set; }
    public Guid DocumentTypeId { get; set; }
    public bool Required { get; set; } = true;
    public int? MinValidityDays { get; set; }
    public string? AuthorityName { get; set; }
    public string? Notes { get; set; }

    public RegulationRule? RegulationRule { get; set; }
    public DocumentType? DocumentType { get; set; }
}
