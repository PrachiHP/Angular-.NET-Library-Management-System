namespace BackendAPI.Repositories;

/// <summary>
/// The contract every repository satisfies. Generic so one interface covers all
/// 11 entities; the 'where T : class' constraint is required because EF Core's
/// Set&lt;T&gt;() only accepts reference types.
/// </summary>
public interface IRepository<T> where T : class
{
    Task<IReadOnlyList<T>> GetAllAsync(CancellationToken ct = default);

    Task<T?> GetByIdAsync(int id, CancellationToken ct = default);

    Task<T> AddAsync(T entity, CancellationToken ct = default);

    Task UpdateAsync(T entity, CancellationToken ct = default);

    Task DeleteAsync(int id, CancellationToken ct = default);

    Task<bool> ExistsAsync(int id, CancellationToken ct = default);
}
