using Compliance.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Compliance.Application.Abstractions;

public interface IComplianceDbContext
{
    DbSet<DocumentType> DocumentTypes { get; }
    DbSet<Document> Documents { get; }
    DbSet<DocumentReview> DocumentReviews { get; }
    DbSet<RegulationRule> RegulationRules { get; }
    DbSet<RegulationDocumentRequirement> RegulationDocumentRequirements { get; }
    DbSet<ClearanceCase> ClearanceCases { get; }
    DbSet<ClearanceCaseDocument> ClearanceCaseDocuments { get; }
    DbSet<OutboxMessage> OutboxMessages { get; }
    DbSet<AuditLog> AuditLogs { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
