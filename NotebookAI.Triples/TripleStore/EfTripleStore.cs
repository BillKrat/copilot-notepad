using Microsoft.EntityFrameworkCore;

namespace NotebookAI.Triples.TripleStore;

public class EfTripleStore : ITripleStore
{
    private readonly IDbContextFactory<TripleDbContext> _factory;

    public EfTripleStore(IDbContextFactory<TripleDbContext> factory)
    {
        _factory = factory;
    }

    public async Task<TripleEntity> CreateAsync(string subject, string? predicate, string? obj, string? data = null, string? dataType = null, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);

        var e = await db.TripleEntities.FirstOrDefaultAsync(t => t.Subject == subject && t.Predicate == predicate && t.Object == obj, ct);
        
        // If already exists, update it instead of creating a duplicate
        if (e != null) return await UpdateAsync(e.Id, subject, predicate, obj, data, dataType, ct) ? e : e;

        var entity = new TripleEntity
        {
            Id = Guid.NewGuid().ToString("N"),
            Subject = subject,
            Predicate = predicate,
            Object = obj,
            GraphContext = data,
            AnnotationMetadata = dataType,
            CreatedUtc = DateTime.UtcNow.ToString()
        };
        db.TripleEntities.Add(entity);
        await db.SaveChangesAsync(ct);
        return entity;
    }

    public async Task<TripleEntity?> GetByIdAsync(string id, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        return await db.TripleEntities.FindAsync(new object?[] { id }, ct);
    }

    public async Task<IReadOnlyList<TripleEntity>> QueryAsync(string? subject = null, string? predicate = null, string? @object = null, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        IQueryable<TripleEntity> q = db.TripleEntities.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(subject)) q = q.Where(t => t.Subject == subject);
        if (!string.IsNullOrWhiteSpace(predicate)) q = q.Where(t => t.Predicate == predicate);
        if (!string.IsNullOrWhiteSpace(@object)) q = q.Where(t => t.Object == @object);
        return await q.OrderBy(t => t.Subject).ThenBy(t => t.Predicate).ThenBy(t => t.Object).ToListAsync(ct);
    }

    public async Task<bool> UpdateAsync(string id, string? subject = null, string? predicate = null, string? @object = null, string? data = null, string? dataType = null, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var e = await db.TripleEntities.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (e == null) return false;
        if (subject != null) e.Subject = subject;
        if (predicate != null) e.Predicate = predicate;
        if (@object != null) e.Object = @object;
        if (data != null) e.GraphContext = data;
        if (dataType != null) e.AnnotationMetadata = dataType;
        e.UpdatedUtc = DateTime.UtcNow.ToString();
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var e = await db.TripleEntities.FirstOrDefaultAsync(t => t.Id == id, ct);
        if (e == null) return false;
        db.TripleEntities.Remove(e);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<long> CountAsync(CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        return await db.TripleEntities.LongCountAsync(ct);
    }
}
