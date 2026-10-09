using Handover.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace Handover.Application.Abstractions;

public interface IHandoverDbContext
{
    DbSet<HandoverRecord> HandoverRecords { get; }
    DbSet<HandoverHorse> HandoverHorses { get; }
    DbSet<TripCostItem> TripCostItems { get; }
    DbSet<TripRevenueItem> TripRevenueItems { get; }
    DbSet<HandoverOutboxMessage> OutboxMessages { get; }
    DbSet<HandoverAuditLog> AuditLogs { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task<IDbContextTransaction?> BeginTransactionAsync(CancellationToken cancellationToken = default);
}
