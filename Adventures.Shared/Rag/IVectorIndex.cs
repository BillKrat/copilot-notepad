namespace Adventures.Shared.Rag;

public sealed record VectorMatch(string Id, float Score, IReadOnlyDictionary<string,object>? Metadata);

public interface IVectorIndex
{
    Task UpsertAsync(string id, ReadOnlyMemory<float> vector, IReadOnlyDictionary<string,object>? metadata = null, CancellationToken ct = default);
    Task<IReadOnlyList<VectorMatch>> SimilaritySearchAsync(ReadOnlyMemory<float> queryVector, int topK, float? minScore = null, CancellationToken ct = default);
}
