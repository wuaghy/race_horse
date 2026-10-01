using Microsoft.EntityFrameworkCore;
using Planning.Application.Abstractions;

namespace Planning.Infrastructure.Persistence;

public sealed class EfRepository<TEntity>(PlanningDbContext db) : IRepository<TEntity> where TEntity : class
{
    public IQueryable<TEntity> Query() => db.Set<TEntity>();
    public ValueTask<TEntity?> FindAsync(Guid id, CancellationToken cancellationToken = default) => db.Set<TEntity>().FindAsync([id], cancellationToken);
    public Task AddAsync(TEntity entity, CancellationToken cancellationToken = default) => db.Set<TEntity>().AddAsync(entity, cancellationToken).AsTask();
    public void Remove(TEntity entity) => db.Set<TEntity>().Remove(entity);
    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) => db.SaveChangesAsync(cancellationToken);
}
