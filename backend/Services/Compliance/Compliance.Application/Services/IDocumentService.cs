using Compliance.Application.DTOs.Documents;

namespace Compliance.Application.Services;

public interface IDocumentService
{
    Task<IReadOnlyList<DocumentDto>> GetDocumentsAsync(DocumentFilter filter, CancellationToken cancellationToken = default);
    Task<DocumentDto?> GetDocumentByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<DocumentDto> UploadDocumentAsync(Guid uploadedByUserId, UploadDocumentRequest request, CancellationToken cancellationToken = default);
    Task<DocumentDto?> ReviewDocumentAsync(Guid documentId, Guid reviewerUserId, ReviewDocumentRequest request, CancellationToken cancellationToken = default);
}
