namespace NotebookAI.Triples.Config;

public interface IBookConfigProvider
{
    Task<IReadOnlyList<BookConfig>> GetBooksAsync(CancellationToken ct = default);
    Task<BookConfig?> GetBookAsync(string id, CancellationToken ct = default);
}

public sealed record BookConfig(
    string Id,
    string? Title,
    DateTime? PublicationDate,
    IReadOnlyDictionary<string,string>? Metadata);
