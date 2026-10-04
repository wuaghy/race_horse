using Compliance.Application.DTOs.MasterData;

namespace Compliance.Application.Services;

public interface IComplianceMasterDataService
{
    Task<IReadOnlyList<DocumentTypeDto>> GetDocumentTypesAsync(string? scope = null, bool? active = null, CancellationToken cancellationToken = default);
    Task<DocumentTypeDto?> GetDocumentTypeByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<DocumentTypeDto> CreateDocumentTypeAsync(CreateDocumentTypeRequest request, CancellationToken cancellationToken = default);
    Task<DocumentTypeDto?> UpdateDocumentTypeAsync(Guid id, UpdateDocumentTypeRequest request, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<RegulationRuleDto>> GetRegulationRulesAsync(Guid? originCountryId = null, Guid? destinationCountryId = null, bool? active = null, CancellationToken cancellationToken = default);
    Task<RegulationRuleDto?> GetRegulationRuleByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<RegulationRuleDto> CreateRegulationRuleAsync(CreateRegulationRuleRequest request, CancellationToken cancellationToken = default);
    Task<RegulationRuleDto?> UpdateRegulationRuleAsync(Guid id, UpdateRegulationRuleRequest request, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<RegulationRuleDto>> LookupRulesAsync(Guid originCountryId, Guid destinationCountryId, DateOnly? effectiveDate = null, CancellationToken cancellationToken = default);
}
