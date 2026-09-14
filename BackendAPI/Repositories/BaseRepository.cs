using BackendAPI.Data;
using Microsoft.EntityFrameworkCore;

namespace BackendAPI.Repositories;

/// <summary>
/// One implementation of IRepository&lt;T&gt; shared by every entity, so the same
/// CRUD code is not written 11 times. Methods are virtual: a derived repository
/// overrides only the behaviour that differs (see BookRepository.DeleteAsync).
/// </summary>
public class BaseRepository<T> : IRepository<T> where T : class
{
    // protected, not private: derived classes need these, callers do not.
    protected readonly AppDbContext Context;
    protected readonly DbSet<T> DbSet;

    public BaseRepository(AppDbContext context)
    {
        Context = context;
        DbSet = context.Set<T>();
    }

    // AsNoTracking skips change-tracker setup. Correct for read-only queries
    // and measurably cheaper on large result sets.
    public virtual async Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default)
        => await DbSet.AsNoTracking().ToListAsync(ct);

    // FindAsync checks the change tracker before hitting the database, so a
    // second lookup in the same request costs nothing.
    public virtual async Task<T?> GetByIdAsync(int id, CancellationToken ct = default)
        => await DbSet.FindAsync(new object?[] { id }, ct);

    public virtual async Task<T> AddAsync(T entity, CancellationToken ct = default)
    {
        await DbSet.AddAsync(entity, ct);
        await Context.SaveChangesAsync(ct);
        return entity;
    }

    public virtual async Task UpdateAsync(T entity, CancellationToken ct = default)
    {
        DbSet.Update(entity);
        await Context.SaveChangesAsync(ct);
    }

    public virtual async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var entity = await GetByIdAsync(id, ct);
        if (entity is null) return;

        DbSet.Remove(entity);
        await Context.SaveChangesAsync(ct);
    }

    public virtual async Task<bool> ExistsAsync(int id, CancellationToken ct = default)
        => await GetByIdAsync(id, ct) is not null;
}
