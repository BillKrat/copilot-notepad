using Microsoft.SemanticKernel;

namespace NotebookAI.Server.Services;

public interface ISaintsDocumentStore
{
    Task<IReadOnlyList<SaintDocument>> GetAllAsync(CancellationToken ct = default);
    Task AddOrUpdateAsync(SaintDocument doc, CancellationToken ct = default);
}

public sealed record SaintDocument(string Id, string Title, string Author, DateTime Date, string Content, IReadOnlyDictionary<string,string>? Tags = null)
{
    public KernelArguments ToKernelArguments() => new()
    {
        ["id"] = Id,
        ["title"] = Title,
        ["author"] = Author,
        ["date"] = Date.ToString("yyyy-MM-dd"),
        ["content"] = Content
    };
}
