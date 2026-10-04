namespace Compliance.Application.DTOs.MasterData;

public sealed record DocumentTypeDto(
    Guid Id,
    string Code,
    string Name,
    string Scope,
    string? Description,
    int? DefaultValidityDays,
    bool Active);

public sealed record CreateDocumentTypeRequest(
    string Code,
    string Name,
    string Scope,
    string? Description = null,
    int? DefaultValidityDays = null);

public sealed record UpdateDocumentTypeRequest(
    string Name,
    string Scope,
    string? Description = null,
    int? DefaultValidityDays = null,
    bool Active = true);
