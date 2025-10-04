using System.Collections.Concurrent;

namespace Adventures.Shared.Documents;

// Made non-sealed to allow specialized variations if needed
public class InMemoryDocumentStore<TDocument> : IDocumentStore<TDocument> where TDocument : IDocument
{
    private readonly ConcurrentDictionary<string, TDocument> _docs = new(StringComparer.OrdinalIgnoreCase);

    public Task<IReadOnlyList<TDocument>> GetAllAsync(CancellationToken ct = default)
    {
        IReadOnlyList<TDocument> snapshot = _docs.Values
            .OrderBy(d => d.Date)
            .ThenBy(d => d.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();
        return Task.FromResult(snapshot);
    }

    public Task AddOrUpdateAsync(TDocument doc, CancellationToken ct = default)
    {
        _docs[doc.Id] = doc;
        return Task.CompletedTask;
    }
}
