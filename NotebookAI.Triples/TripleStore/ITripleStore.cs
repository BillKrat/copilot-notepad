namespace NotebookAI.Triples.TripleStore;

public interface ITripleStore
{
    Task<TripleEntity> CreateAsync(string subject, string? predicate, string? obj, string? data = null, string? dataType = null, CancellationToken ct = default);
    Task<TripleEntity?> GetByIdAsync(string id, CancellationToken ct = default);
    Task<IReadOnlyList<TripleEntity>> QueryAsync(string? subject = null, string? predicate = null, string? @object = null, CancellationToken ct = default);
    Task<bool> UpdateAsync(string id, string? subject = null, string? predicate = null, string? @object = null, string? data = null, string? dataType = null, CancellationToken ct = default);
    Task<bool> DeleteAsync(string id, CancellationToken ct = default);
    Task<long> CountAsync(CancellationToken ct = default);
}
