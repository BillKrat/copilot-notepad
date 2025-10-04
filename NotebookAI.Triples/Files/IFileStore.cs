namespace NotebookAI.Triples.Files;

public interface IFileStore
{
    Task<FileEntry> CreateAsync(string path, Stream content, string contentType, CancellationToken ct = default);
    Task<FileEntry?> GetAsync(string path, CancellationToken ct = default);
    Task<bool> DeleteAsync(string path, CancellationToken ct = default);
    Task<IReadOnlyList<FileEntry>> ListAsync(string prefix, CancellationToken ct = default);
    Task<FileEntry> UpsertAsync(string path, Stream content, string contentType, CancellationToken ct = default);
}

public sealed record FileEntry(
    string Path,
    string ContentType,
    long Length,
    DateTimeOffset? LastModified,
    Uri? Uri);
