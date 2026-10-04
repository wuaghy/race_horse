using BuildingBlocks.Api;
using Compliance.Application.DTOs.MasterData;
using Compliance.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace Compliance.Api.Endpoints;

public static class ComplianceMasterDataEndpoints
{
    public static IEndpointRouteBuilder MapComplianceMasterDataEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/compliance")
            .WithTags("Compliance Master Data");

        // Document Types
        group.MapGet("/document-types", async (
            [FromQuery] string? scope,
            [FromQuery] bool? active,
            IComplianceMasterDataService service,
            CancellationToken ct) =>
        {
            var result = await service.GetDocumentTypesAsync(scope, active, ct);
            return Results.Ok(ApiResponse<IReadOnlyList<DocumentTypeDto>>.Success(result));
        })
        .WithName("GetDocumentTypes")
        .WithSummary("List all document types with optional scope filter");

        group.MapGet("/document-types/{id:guid}", async (
            Guid id,
            IComplianceMasterDataService service,
            CancellationToken ct) =>
        {
            var result = await service.GetDocumentTypeByIdAsync(id, ct);
            return result is not null
                ? Results.Ok(ApiResponse<DocumentTypeDto>.Success(result))
                : Results.NotFound(ApiResponse<object>.Failure(404, "Document type not found."));
        })
        .WithName("GetDocumentTypeById");

        group.MapPost("/document-types", async (
            [FromBody] CreateDocumentTypeRequest request,
            IComplianceMasterDataService service,
            CancellationToken ct) =>
        {
            var result = await service.CreateDocumentTypeAsync(request, ct);
            return Results.Created($"/api/v1/compliance/document-types/{result.Id}", ApiResponse<DocumentTypeDto>.Success(result));
        })
        .WithName("CreateDocumentType");

        group.MapPut("/document-types/{id:guid}", async (
            Guid id,
            [FromBody] UpdateDocumentTypeRequest request,
            IComplianceMasterDataService service,
            CancellationToken ct) =>
        {
            var result = await service.UpdateDocumentTypeAsync(id, request, ct);
            return result is not null
                ? Results.Ok(ApiResponse<DocumentTypeDto>.Success(result))
                : Results.NotFound(ApiResponse<object>.Failure(404, "Document type not found."));
        })
        .WithName("UpdateDocumentType");

        // Regulations
        group.MapGet("/regulations", async (
            [FromQuery] Guid? originCountryId,
            [FromQuery] Guid? destinationCountryId,
            [FromQuery] bool? active,
            IComplianceMasterDataService service,
            CancellationToken ct) =>
        {
            var result = await service.GetRegulationRulesAsync(originCountryId, destinationCountryId, active, ct);
            return Results.Ok(ApiResponse<IReadOnlyList<RegulationRuleDto>>.Success(result));
        })
        .WithName("GetRegulationRules");

        group.MapGet("/regulations/{id:guid}", async (
            Guid id,
            IComplianceMasterDataService service,
            CancellationToken ct) =>
        {
            var result = await service.GetRegulationRuleByIdAsync(id, ct);
            return result is not null
                ? Results.Ok(ApiResponse<RegulationRuleDto>.Success(result))
                : Results.NotFound(ApiResponse<object>.Failure(404, "Regulation rule not found."));
        })
        .WithName("GetRegulationRuleById");

        group.MapPost("/regulations", async (
            [FromBody] CreateRegulationRuleRequest request,
            IComplianceMasterDataService service,
            CancellationToken ct) =>
        {
            var result = await service.CreateRegulationRuleAsync(request, ct);
            return Results.Created($"/api/v1/compliance/regulations/{result.Id}", ApiResponse<RegulationRuleDto>.Success(result));
        })
        .WithName("CreateRegulationRule");

        group.MapPut("/regulations/{id:guid}", async (
            Guid id,
            [FromBody] UpdateRegulationRuleRequest request,
            IComplianceMasterDataService service,
            CancellationToken ct) =>
        {
            var result = await service.UpdateRegulationRuleAsync(id, request, ct);
            return result is not null
                ? Results.Ok(ApiResponse<RegulationRuleDto>.Success(result))
                : Results.NotFound(ApiResponse<object>.Failure(404, "Regulation rule not found."));
        })
        .WithName("UpdateRegulationRule");

        group.MapGet("/regulations/lookup", async (
            [FromQuery] Guid originCountryId,
            [FromQuery] Guid destinationCountryId,
            [FromQuery] DateOnly? effectiveDate,
            IComplianceMasterDataService service,
            CancellationToken ct) =>
        {
            var result = await service.LookupRulesAsync(originCountryId, destinationCountryId, effectiveDate, ct);
            return Results.Ok(ApiResponse<IReadOnlyList<RegulationRuleDto>>.Success(result));
        })
        .WithName("LookupRegulationRules");

        return app;
    }
}
