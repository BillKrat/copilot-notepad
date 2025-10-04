using NotebookAI.Triples.TripleStore;

namespace NotebookAI.Triples.Config;

/// <summary>
/// Reads book configuration (books, chapters etc.) from the triple store.
/// Books are triples matching (book:*, hasTitle, ?title) and optional publication date + other metadata.
/// </summary>
public sealed class BookConfigFromTriplesProvider : IBookConfigProvider
{
    private readonly ITripleStore _store;

    public BookConfigFromTriplesProvider(ITripleStore store) => _store = store;

    public async Task<IReadOnlyList<BookConfig>> GetBooksAsync(CancellationToken ct = default)
    {
        // Find all book instances: (?book, rdf:type, Book)
        var typeTriples = await _store.QueryAsync(predicate: "rdf:type", @object: "Book", ct: ct);
        var list = new List<BookConfig>();
        foreach (var t in typeTriples)
        {
            var id = t.Subject; // e.g., book:lotr
            list.Add(await BuildAsync(id, ct));
        }
        return list;
    }

    public async Task<BookConfig?> GetBookAsync(string id, CancellationToken ct = default)
    {
        // Ensure it exists as a book
        var type = await _store.QueryAsync(subject: id, predicate: "rdf:type", @object: "Book", ct: ct);
        if (type.Count == 0) return null;
        return await BuildAsync(id, ct);
    }

    private async Task<BookConfig> BuildAsync(string id, CancellationToken ct)
    {
        string? title = null; DateTime? pubDate = null; Dictionary<string,string>? meta = null;
        var triples = await _store.QueryAsync(subject: id, ct: ct);
        foreach (var tr in triples)
        {
            if (tr.Predicate == "hasTitle" && !string.IsNullOrWhiteSpace(tr.Object))
                title = tr.Object;
            else if (tr.Predicate == "hasPublicationDate" && !string.IsNullOrWhiteSpace(tr.Object))
            {
                if (DateTime.TryParse(tr.Object, out var dt)) pubDate = dt;
            }
            else if (!string.IsNullOrWhiteSpace(tr.Predicate) && !string.IsNullOrWhiteSpace(tr.Object))
            {
                meta ??= new(StringComparer.OrdinalIgnoreCase);
                // Avoid overwriting primary fields
                if (tr.Predicate is not ("rdf:type" or "hasTitle" or "hasPublicationDate"))
                    meta[tr.Predicate] = tr.Object;
            }
        }
        return new BookConfig(id, title, pubDate, meta);
    }
}
