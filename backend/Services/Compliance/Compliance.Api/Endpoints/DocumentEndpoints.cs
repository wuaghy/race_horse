using System.Security.Claims;
using BuildingBlocks.Api;
using Compliance.Application.Abstractions;
using Compliance.Application.DTOs.Documents;
using Compliance.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace Compliance.Api.Endpoints;

public static class DocumentEndpoints
{
    public static IEndpointRouteBuilder MapDocumentEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/compliance/documents")
            .WithTags("Compliance Documents");

        group.MapGet("/", async (
            [FromQuery] Guid? horseId,
            [FromQuery] Guid? requestId,
            [FromQuery] Guid? tripId,
            [FromQuery] Guid? documentTypeId,
            [FromQuery] string? status,
            IDocumentService service,
            CancellationToken ct) =>
        {
            var filter = new DocumentFilter(horseId, requestId, tripId, documentTypeId, status);
            var result = await service.GetDocumentsAsync(filter, ct);
            return Results.Ok(ApiResponse<IReadOnlyList<DocumentDto>>.Success(result));
        })
        .WithName("GetDocuments");

        group.MapGet("/{id:guid}", async (
            Guid id,
            IDocumentService service,
            CancellationToken ct) =>
        {
            var result = await service.GetDocumentByIdAsync(id, ct);
            return result is not null
                ? Results.Ok(ApiResponse<DocumentDto>.Success(result))
                : Results.NotFound(ApiResponse<object>.Failure(404, "Document not found."));
        })
        .WithName("GetDocumentById");

        group.MapPost("/upload", async (
            [FromBody] UploadDocumentRequest request,
            ClaimsPrincipal user,
            IDocumentService service,
            CancellationToken ct) =>
        {
            var userId = TryGetUserId(user) ?? Guid.Parse("00000000-0000-0000-0000-000000000001");
            var result = await service.UploadDocumentAsync(userId, request, ct);
            return Results.Created($"/api/v1/compliance/documents/{result.Id}", ApiResponse<DocumentDto>.Success(result));
        })
        .WithName("UploadDocument");

        group.MapPost("/upload-file", async (
            IFormFile file,
            [FromForm] Guid documentTypeId,
            [FromForm] Guid? horseId,
            [FromForm] Guid? requestId,
            [FromForm] Guid? tripId,
            [FromForm] DateOnly? issueDate,
            [FromForm] DateOnly? expiryDate,
            [FromForm] string? issuingAuthority,
            ClaimsPrincipal user,
            IFileStorageService fileStorage,
            IDocumentService service,
            CancellationToken ct) =>
        {
            if (file == null || file.Length == 0)
            {
                return Results.BadRequest(ApiResponse<object>.Failure(400, "No file uploaded or file is empty."));
            }

            var userId = TryGetUserId(user) ?? Guid.Parse("00000000-0000-0000-0000-000000000001");

            await using var stream = file.OpenReadStream();
            var fileUrl = await fileStorage.UploadFileAsync(stream, file.FileName, file.ContentType, ct);

            var uploadRequest = new UploadDocumentRequest(
                DocumentTypeId: documentTypeId,
                HorseId: horseId,
                RequestId: requestId,
                TripId: tripId,
                FileName: file.FileName,
                FileUrl: fileUrl,
                FileSize: file.Length,
                MimeType: file.ContentType,
                IssueDate: issueDate,
                ExpiryDate: expiryDate,
                IssuingAuthority: issuingAuthority
            );

            var result = await service.UploadDocumentAsync(userId, uploadRequest, ct);
            return Results.Created($"/api/v1/compliance/documents/{result.Id}", ApiResponse<DocumentDto>.Success(result));
        })
        .DisableAntiforgery()
        .WithName("UploadDocumentFile");

        group.MapPost("/{id:guid}/review", async (
            Guid id,
            [FromBody] ReviewDocumentRequest request,
            ClaimsPrincipal user,
            IDocumentService service,
            CancellationToken ct) =>
        {
            var reviewerId = TryGetUserId(user) ?? Guid.Parse("00000000-0000-0000-0000-000000000002");
            var result = await service.ReviewDocumentAsync(id, reviewerId, request, ct);

            return result is not null
                ? Results.Ok(ApiResponse<DocumentDto>.Success(result))
                : Results.NotFound(ApiResponse<object>.Failure(404, "Document not found."));
        })
        .WithName("ReviewDocument");

        return app;
    }

    private static Guid? TryGetUserId(ClaimsPrincipal principal)
    {
        var claim = principal.FindFirst("sub")
            ?? principal.FindFirst(ClaimTypes.NameIdentifier)
            ?? principal.FindFirst("userId")
            ?? principal.FindFirst("id");

        return claim is not null && Guid.TryParse(claim.Value, out var id) ? id : null;
    }
}
