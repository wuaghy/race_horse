namespace Compliance.Application.DTOs.MasterData;

public sealed record RegulationDocumentRequirementDto(
    Guid Id,
    Guid RegulationRuleId,
    Guid DocumentTypeId,
    string? DocumentTypeCode,
    string? DocumentTypeName,
    bool Required,
    int? MinValidityDays,
    string? AuthorityName,
    string? Notes);

public sealed record RegulationRuleDto(
    Guid Id,
    string RuleCode,
    Guid OriginCountryId,
    Guid DestinationCountryId,
    Guid? BorderPointId,
    string Title,
    string? Description,
    bool QuarantineRequired,
    bool CustomsRequired,
    bool InspectionRequired,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    bool Active,
    IReadOnlyList<RegulationDocumentRequirementDto> Requirements);

public sealed record CreateRegulationRequirementInput(
    Guid DocumentTypeId,
    bool Required = true,
    int? MinValidityDays = null,
    string? AuthorityName = null,
    string? Notes = null);

public sealed record CreateRegulationRuleRequest(
    string RuleCode,
    Guid OriginCountryId,
    Guid DestinationCountryId,
    Guid? BorderPointId,
    string Title,
    string? Description,
    bool QuarantineRequired,
    bool CustomsRequired,
    bool InspectionRequired,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo = null,
    IReadOnlyList<CreateRegulationRequirementInput>? Requirements = null);

public sealed record UpdateRegulationRuleRequest(
    string Title,
    string? Description,
    bool QuarantineRequired,
    bool CustomsRequired,
    bool InspectionRequired,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo = null,
    bool Active = true,
    IReadOnlyList<CreateRegulationRequirementInput>? Requirements = null);
