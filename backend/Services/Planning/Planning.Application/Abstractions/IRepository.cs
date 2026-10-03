namespace Planning.Application.Abstractions;

public interface IRepository<TEntity> where TEntity : class
{
    IQueryable<TEntity> Query();
    ValueTask<TEntity?> FindAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(TEntity entity, CancellationToken cancellationToken = default);
    void Remove(TEntity entity);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
