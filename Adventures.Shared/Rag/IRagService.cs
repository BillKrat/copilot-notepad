using Adventures.Shared.Documents;

namespace Adventures.Shared.Rag;

public interface IRagService<TDocument> where TDocument : IDocument
{
    Task<int> EnsureIndexedAsync(CancellationToken ct = default);
    Task<string> AskAsync(string question, int top = 4, CancellationToken ct = default);
}
