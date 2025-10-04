using System.Text.Json;
using Adventures.Shared.Documents;
using Microsoft.EntityFrameworkCore;
using NotebookAI.Services.Documents;

namespace NotebookAI.Services.Persistence;

/// <summary>
/// EF backed implementation for BookDocument storage.
/// </summary>
public sealed class EfBookDocumentStore : IBookDocumentStore
{
    private readonly IDbContextFactory<NotebookDbContext> _factory;

    public EfBookDocumentStore(IDbContextFactory<NotebookDbContext> factory)
    {
        _factory = factory;
    }

    public async Task<IReadOnlyList<BookDocument>> GetAllAsync(CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var entities = await db.BookDocuments.AsNoTracking().OrderBy(d => d.Date).ThenBy(d => d.Title).ToListAsync(ct);
        return entities.Select(Map).ToList();
    }

    public async Task AddOrUpdateAsync(BookDocument doc, CancellationToken ct = default)
    {
        await using var db = await _factory.CreateDbContextAsync(ct);
        var existing = await db.BookDocuments.FirstOrDefaultAsync(d => d.Id == doc.Id, ct);
        if (existing == null)
        {
            db.BookDocuments.Add(Map(doc));
        }
        else
        {
            existing.Title = doc.Title;
            existing.Author = doc.Author;
            existing.Date = doc.Date;
            existing.Content = doc.Content;
            existing.TagsJson = doc.Tags is null ? null : JsonSerializer.Serialize(doc.Tags);
        }
        await db.SaveChangesAsync(ct);
    }

    private static BookDocument Map(BookDocumentEntity e)
        => new(e.Id, e.Title, e.Author, e.Date, e.Content, string.IsNullOrWhiteSpace(e.TagsJson) ? null : JsonSerializer.Deserialize<Dictionary<string,string>>(e.TagsJson));

    private static BookDocumentEntity Map(BookDocument d)
        => new() { Id = d.Id, Title = d.Title, Author = d.Author, Date = d.Date, Content = d.Content, TagsJson = d.Tags is null ? null : JsonSerializer.Serialize(d.Tags) };
}
