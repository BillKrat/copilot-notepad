using System.Collections.Concurrent;

namespace Adventures.Shared.Rag;

public sealed class InMemoryVectorIndex : IVectorIndex
{
    private readonly ConcurrentDictionary<string, (float[] Vector, IReadOnlyDictionary<string,object>? Meta)> _store = new();

    public Task UpsertAsync(string id, ReadOnlyMemory<float> vector, IReadOnlyDictionary<string, object>? metadata = null, CancellationToken ct = default)
    {
        _store[id] = (vector.ToArray(), metadata);
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<VectorMatch>> SimilaritySearchAsync(ReadOnlyMemory<float> queryVector, int topK, float? minScore = null, CancellationToken ct = default)
    {
        var queryCopy = queryVector.ToArray();
        static double Cos(IReadOnlyList<float> a, IReadOnlyList<float> b)
        {
            double dot = 0, na = 0, nb = 0;
            int len = Math.Min(a.Count, b.Count);
            for (int i = 0; i < len; i++) { var av = a[i]; var bv = b[i]; dot += av * bv; na += av * av; nb += bv * bv; }
            return dot / (Math.Sqrt(na) * Math.Sqrt(nb) + 1e-9);
        }
        var ranked = _store
            .Select(kv => new VectorMatch(kv.Key, (float)Cos(queryCopy, kv.Value.Vector), kv.Value.Meta))
            .Where(m => !minScore.HasValue || m.Score >= minScore.Value)
            .OrderByDescending(m => m.Score)
            .Take(Math.Max(1, topK))
            .ToList();
        return Task.FromResult<IReadOnlyList<VectorMatch>>(ranked);
    }
}
