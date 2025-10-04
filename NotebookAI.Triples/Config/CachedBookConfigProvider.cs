using Microsoft.Extensions.Caching.Memory;

namespace NotebookAI.Triples.Config;

public interface IBookConfigCacheInvalidator
{
    void Invalidate(string? id = null);
}

/// <summary>
/// Adds in-memory caching on top of another IBookConfigProvider.
/// Cache invalidates after the configured TTL (default 60s).
/// </summary>
public sealed class CachedBookConfigProvider : IBookConfigProvider, IBookConfigCacheInvalidator
{
    private readonly IBookConfigProvider _inner;
    private readonly IMemoryCache _cache;
    private readonly TimeSpan _ttl;

    private static readonly string AllBooksKey = "__all_books__";

    public CachedBookConfigProvider(IBookConfigProvider inner, IMemoryCache cache, TimeSpan? ttl = null)
    {
        _inner = inner;
        _cache = cache;
        _ttl = ttl ?? TimeSpan.FromSeconds(60);
    }

    public async Task<IReadOnlyList<BookConfig>> GetBooksAsync(CancellationToken ct = default)
    {
        if (_cache.TryGetValue(AllBooksKey, out IReadOnlyList<BookConfig>? cached) && cached != null)
            return cached;
        var books = await _inner.GetBooksAsync(ct);
        _cache.Set(AllBooksKey, books, _ttl);
        // Also prime individual entries
        foreach (var b in books)
        {
            _cache.Set(BookKey(b.Id), b, _ttl);
        }
        return books;
    }

    public async Task<BookConfig?> GetBookAsync(string id, CancellationToken ct = default)
    {
        if (_cache.TryGetValue(BookKey(id), out BookConfig? cached) && cached != null)
            return cached;
        var book = await _inner.GetBookAsync(id, ct);
        if (book != null)
            _cache.Set(BookKey(id), book, _ttl);
        return book;
    }

    public void Invalidate(string? id = null)
    {
        if (string.IsNullOrEmpty(id))
        {
            _cache.Remove(AllBooksKey);
            // No enumeration API without tracking keys; rely on TTL for individual book entries.
        }
        else
        {
            _cache.Remove(BookKey(id));
        }
    }

    private static string BookKey(string id) => $"book::{id}";
}
