using Compliance.Application.Abstractions;
using Compliance.Application.DTOs.MasterData;
using Compliance.Application.Services;
using Compliance.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Compliance.Infrastructure.Services;

public class ComplianceMasterDataService : IComplianceMasterDataService
{
    private readonly IComplianceDbContext _db;

    public ComplianceMasterDataService(IComplianceDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<DocumentTypeDto>> GetDocumentTypesAsync(string? scope = null, bool? active = null, CancellationToken cancellationToken = default)
    {
        var query = _db.DocumentTypes.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(scope))
        {
            query = query.Where(dt => dt.Scope == scope.ToUpperInvariant());
        }

        if (active.HasValue)
        {
            query = query.Where(dt => dt.Active == active.Value);
        }

        var list = await query.OrderBy(dt => dt.Name).ToListAsync(cancellationToken);
        return list.Select(MapDocumentTypeToDto).ToList();
    }

    public async Task<DocumentTypeDto?> GetDocumentTypeByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var item = await _db.DocumentTypes.AsNoTracking().FirstOrDefaultAsync(dt => dt.Id == id, cancellationToken);
        return item is null ? null : MapDocumentTypeToDto(item);
    }

    public async Task<DocumentTypeDto> CreateDocumentTypeAsync(CreateDocumentTypeRequest request, CancellationToken cancellationToken = default)
    {
        var code = request.Code.Trim().ToUpperInvariant();
        var exists = await _db.DocumentTypes.AnyAsync(dt => dt.Code == code, cancellationToken);
        if (exists)
        {
            throw new InvalidOperationException($"Document type with code '{code}' already exists.");
        }

        var entity = new DocumentType
        {
            Id = Guid.NewGuid(),
            Code = code,
            Name = request.Name.Trim(),
            Scope = request.Scope.Trim().ToUpperInvariant(),
            Description = request.Description,
            DefaultValidityDays = request.DefaultValidityDays,
            Active = true
        };

        _db.DocumentTypes.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        return MapDocumentTypeToDto(entity);
    }

    public async Task<DocumentTypeDto?> UpdateDocumentTypeAsync(Guid id, UpdateDocumentTypeRequest request, CancellationToken cancellationToken = default)
    {
        var entity = await _db.DocumentTypes.FirstOrDefaultAsync(dt => dt.Id == id, cancellationToken);
        if (entity is null) return null;

        entity.Name = request.Name.Trim();
        entity.Scope = request.Scope.Trim().ToUpperInvariant();
        entity.Description = request.Description;
        entity.DefaultValidityDays = request.DefaultValidityDays;
        entity.Active = request.Active;

        await _db.SaveChangesAsync(cancellationToken);
        return MapDocumentTypeToDto(entity);
    }

    public async Task<IReadOnlyList<RegulationRuleDto>> GetRegulationRulesAsync(Guid? originCountryId = null, Guid? destinationCountryId = null, bool? active = null, CancellationToken cancellationToken = default)
    {
        var query = _db.RegulationRules
            .Include(r => r.Requirements)
                .ThenInclude(req => req.DocumentType)
            .AsNoTracking()
            .AsQueryable();

        if (originCountryId.HasValue) query = query.Where(r => r.OriginCountryId == originCountryId.Value);
        if (destinationCountryId.HasValue) query = query.Where(r => r.DestinationCountryId == destinationCountryId.Value);
        if (active.HasValue) query = query.Where(r => r.Active == active.Value);

        var list = await query.OrderBy(r => r.RuleCode).ToListAsync(cancellationToken);
        return list.Select(MapRegulationRuleToDto).ToList();
    }

    public async Task<RegulationRuleDto?> GetRegulationRuleByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var item = await _db.RegulationRules
            .Include(r => r.Requirements)
                .ThenInclude(req => req.DocumentType)
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        return item is null ? null : MapRegulationRuleToDto(item);
    }

    public async Task<RegulationRuleDto> CreateRegulationRuleAsync(CreateRegulationRuleRequest request, CancellationToken cancellationToken = default)
    {
        var ruleCode = request.RuleCode.Trim().ToUpperInvariant();
        var exists = await _db.RegulationRules.AnyAsync(r => r.RuleCode == ruleCode, cancellationToken);
        if (exists)
        {
            throw new InvalidOperationException($"Regulation rule with code '{ruleCode}' already exists.");
        }

        var rule = new RegulationRule
        {
            Id = Guid.NewGuid(),
            RuleCode = ruleCode,
            OriginCountryId = request.OriginCountryId,
            DestinationCountryId = request.DestinationCountryId,
            BorderPointId = request.BorderPointId,
            Title = request.Title.Trim(),
            Description = request.Description,
            QuarantineRequired = request.QuarantineRequired,
            CustomsRequired = request.CustomsRequired,
            InspectionRequired = request.InspectionRequired,
            EffectiveFrom = request.EffectiveFrom,
            EffectiveTo = request.EffectiveTo,
            Active = true
        };

        if (request.Requirements != null)
        {
            foreach (var req in request.Requirements)
            {
                rule.Requirements.Add(new RegulationDocumentRequirement
                {
                    Id = Guid.NewGuid(),
                    RegulationRuleId = rule.Id,
                    DocumentTypeId = req.DocumentTypeId,
                    Required = req.Required,
                    MinValidityDays = req.MinValidityDays,
                    AuthorityName = req.AuthorityName,
                    Notes = req.Notes
                });
            }
        }

        _db.RegulationRules.Add(rule);
        await _db.SaveChangesAsync(cancellationToken);

        return (await GetRegulationRuleByIdAsync(rule.Id, cancellationToken))!;
    }

    public async Task<RegulationRuleDto?> UpdateRegulationRuleAsync(Guid id, UpdateRegulationRuleRequest request, CancellationToken cancellationToken = default)
    {
        var rule = await _db.RegulationRules
            .Include(r => r.Requirements)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        if (rule is null) return null;

        rule.Title = request.Title.Trim();
        rule.Description = request.Description;
        rule.QuarantineRequired = request.QuarantineRequired;
        rule.CustomsRequired = request.CustomsRequired;
        rule.InspectionRequired = request.InspectionRequired;
        rule.EffectiveFrom = request.EffectiveFrom;
        rule.EffectiveTo = request.EffectiveTo;
        rule.Active = request.Active;

        if (request.Requirements != null)
        {
            rule.Requirements.Clear();
            foreach (var req in request.Requirements)
            {
                rule.Requirements.Add(new RegulationDocumentRequirement
                {
                    Id = Guid.NewGuid(),
                    RegulationRuleId = rule.Id,
                    DocumentTypeId = req.DocumentTypeId,
                    Required = req.Required,
                    MinValidityDays = req.MinValidityDays,
                    AuthorityName = req.AuthorityName,
                    Notes = req.Notes
                });
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
        return (await GetRegulationRuleByIdAsync(rule.Id, cancellationToken))!;
    }

    public async Task<IReadOnlyList<RegulationRuleDto>> LookupRulesAsync(Guid originCountryId, Guid destinationCountryId, DateOnly? effectiveDate = null, CancellationToken cancellationToken = default)
    {
        var targetDate = effectiveDate ?? DateOnly.FromDateTime(DateTime.UtcNow);

        var query = _db.RegulationRules
            .Include(r => r.Requirements)
                .ThenInclude(req => req.DocumentType)
            .AsNoTracking()
            .Where(r => r.Active &&
                        r.OriginCountryId == originCountryId &&
                        r.DestinationCountryId == destinationCountryId &&
                        r.EffectiveFrom <= targetDate &&
                        (!r.EffectiveTo.HasValue || r.EffectiveTo.Value >= targetDate));

        var list = await query.ToListAsync(cancellationToken);
        return list.Select(MapRegulationRuleToDto).ToList();
    }

    private static DocumentTypeDto MapDocumentTypeToDto(DocumentType dt) =>
        new(dt.Id, dt.Code, dt.Name, dt.Scope, dt.Description, dt.DefaultValidityDays, dt.Active);

    private static RegulationRuleDto MapRegulationRuleToDto(RegulationRule r) =>
        new(
            r.Id,
            r.RuleCode,
            r.OriginCountryId,
            r.DestinationCountryId,
            r.BorderPointId,
            r.Title,
            r.Description,
            r.QuarantineRequired,
            r.CustomsRequired,
            r.InspectionRequired,
            r.EffectiveFrom,
            r.EffectiveTo,
            r.Active,
            r.Requirements.Select(req => new RegulationDocumentRequirementDto(
                req.Id,
                req.RegulationRuleId,
                req.DocumentTypeId,
                req.DocumentType?.Code,
                req.DocumentType?.Name,
                req.Required,
                req.MinValidityDays,
                req.AuthorityName,
                req.Notes)).ToList()
        );
}
