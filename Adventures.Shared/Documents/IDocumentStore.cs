namespace Adventures.Shared.Documents;

public interface IDocumentStore<TDocument> where TDocument : IDocument
{
    Task<IReadOnlyList<TDocument>> GetAllAsync(CancellationToken ct = default);
    Task AddOrUpdateAsync(TDocument doc, CancellationToken ct = default);
}
