namespace NotebookAI.Server.Services;

public sealed class InMemorySaintsDocumentStore : ISaintsDocumentStore
{
    private readonly Dictionary<string, SaintDocument> _docs = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _gate = new(1,1);

    public async Task<IReadOnlyList<SaintDocument>> GetAllAsync(CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            return _docs.Values.OrderBy(d => d.Date).ThenBy(d => d.Title).ToList();
        }
        finally { _gate.Release(); }
    }

    public async Task AddOrUpdateAsync(SaintDocument doc, CancellationToken ct = default)
    {
        await _gate.WaitAsync(ct);
        try
        {
            _docs[doc.Id] = doc;
        }
        finally { _gate.Release(); }
    }
}
