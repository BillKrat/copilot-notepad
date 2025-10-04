using System.Text;
using System.Linq;
using NotebookAI.Triples.TripleStore;

namespace NotebookAI.Triples.Files;

/// <summary>
/// Stores file metadata + content in triples. Content kept in Data field of a single triple per path.
/// subject = file:{normalizedPath} predicate = hasContent, Object = (optional filename), Data = content (Base64 for binary), DataType = mime.
/// Additional triples:
///  - length (Object = length string)
///  - lastModified (Object = ISO 8601 timestamp)
/// NOTE: This is a simple implementation; large binary blobs will not be efficient.
/// </summary>
public sealed class TripleStoreFileStore : IFileStore
{
    private readonly ITripleStore _triples;

    public TripleStoreFileStore(ITripleStore triples) => _triples = triples;

    private static string Subject(string path) => $"file:{path.Replace('\\','/').TrimStart('/')}";

    public async Task<FileEntry> CreateAsync(string path, Stream content, string contentType, CancellationToken ct = default)
    {
        var existing = await GetAsync(path, ct);
        if (existing != null) throw new InvalidOperationException("File exists");
        return await UpsertAsync(path, content, contentType, ct);
    }

    public async Task<FileEntry?> GetAsync(string path, CancellationToken ct = default)
    {
        var sub = Subject(path);
        var triples = await _triples.QueryAsync(subject: sub, ct: ct);
        if (triples.Count == 0) return null;
        long len = 0; DateTimeOffset? lm = null; string? ctType = null;
        foreach (var t in triples)
        {
            if (t.Predicate == "length" && long.TryParse(t.Object, out var l)) len = l;
            else if (t.Predicate == "lastModified" && DateTimeOffset.TryParse(t.Object, out var dto)) lm = dto;
            else if (t.Predicate == "hasContent" && !string.IsNullOrWhiteSpace(t.DataType)) ctType = t.DataType;
        }
        return new FileEntry(path, ctType ?? "application/octet-stream", len, lm, null);
    }

    public async Task<bool> DeleteAsync(string path, CancellationToken ct = default)
    {
        var sub = Subject(path);
        var triples = await _triples.QueryAsync(subject: sub, ct: ct);
        bool any = false;
        foreach (var t in triples)
        {
            await _triples.DeleteAsync(t.Id, ct);
            any = true;
        }
        return any;
    }

    public async Task<IReadOnlyList<FileEntry>> ListAsync(string prefix, CancellationToken ct = default)
    {
        var pfx = Subject(prefix);
        // naive full scan (optimize with dedicated index if needed)
        var all = await _triples.QueryAsync(ct: ct);
        var grouped = all.Where(t => t.Subject.StartsWith(pfx, StringComparison.OrdinalIgnoreCase))
            .GroupBy(t => t.Subject);
        var list = new List<FileEntry>();
        foreach (var g in grouped)
        {
            string path = g.Key.Substring("file:".Length);
            long len = 0; DateTimeOffset? lm = null; string ctType = "application/octet-stream";
            foreach (var t in g)
            {
                if (t.Predicate == "length" && long.TryParse(t.Object, out var l)) len = l;
                else if (t.Predicate == "lastModified" && DateTimeOffset.TryParse(t.Object, out var dto)) lm = dto;
                else if (t.Predicate == "hasContent" && !string.IsNullOrWhiteSpace(t.DataType)) ctType = t.DataType;
            }
            list.Add(new FileEntry(path, ctType, len, lm, null));
        }
        return list;
    }

    public async Task<FileEntry> UpsertAsync(string path, Stream content, string contentType, CancellationToken ct = default)
    {
        var sub = Subject(path);
        using var ms = new MemoryStream();
        await content.CopyToAsync(ms, ct);
        var bytes = ms.ToArray();
        var b64 = Convert.ToBase64String(bytes);

        // Replace existing content triple(s)
        foreach (var e in await _triples.QueryAsync(subject: sub, predicate: "hasContent", ct: ct))
            await _triples.DeleteAsync(e.Id, ct);
        await _triples.CreateAsync(sub, "hasContent", null, b64, contentType, ct);

        foreach (var e in await _triples.QueryAsync(subject: sub, predicate: "length", ct: ct))
            await _triples.DeleteAsync(e.Id, ct);
        await _triples.CreateAsync(sub, "length", bytes.Length.ToString(), null, null, ct);

        foreach (var e in await _triples.QueryAsync(subject: sub, predicate: "lastModified", ct: ct))
            await _triples.DeleteAsync(e.Id, ct);
        var now = DateTimeOffset.UtcNow;
        await _triples.CreateAsync(sub, "lastModified", now.ToString("o"), null, null, ct);

        return new FileEntry(path, contentType, bytes.Length, now, null);
    }
}
