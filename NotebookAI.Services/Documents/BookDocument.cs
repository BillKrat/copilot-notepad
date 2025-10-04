using Adventures.Shared.Documents;

namespace NotebookAI.Services.Documents;

public sealed record BookDocument(
    string Id,
    string Title,
    string Author,
    DateTime Date,
    string Content,
    IReadOnlyDictionary<string,string>? Tags = null
) : BasicDocument(Id, Title, Author, Date, Content, Tags);
