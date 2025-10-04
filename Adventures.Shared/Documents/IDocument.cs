namespace Adventures.Shared.Documents;

public interface IDocument
{
    string Id { get; }
    string Title { get; }
    string Author { get; }
    DateTime Date { get; }
    string Content { get; }
    IReadOnlyDictionary<string,string>? Tags { get; }
}

// Made non-sealed to allow domain-specific derivations (e.g., BookDocument)
public record BasicDocument(
    string Id,
    string Title,
    string Author,
    DateTime Date,
    string Content,
    IReadOnlyDictionary<string,string>? Tags = null) : IDocument;
