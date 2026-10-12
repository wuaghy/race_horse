using Incident.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Incident.Application.Abstractions;

public interface IIncidentDbContext
{
    DbSet<Incident.Domain.Entities.Incident> Incidents { get; }
    DbSet<IncidentAttachment> IncidentAttachments { get; }
    DbSet<OutboxMessage> OutboxMessages { get; }
    DbSet<AuditLog> AuditLogs { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
